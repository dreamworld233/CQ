using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CQ.Runtime
{
    /// <summary>
    /// 场景驱动桥接：加载配置 → 建 BattleEngine → 生成世界单位 →
    /// 点选交互（点角色→弧形技能→点目标）+ uGUI HUD（战力条/结束回合）。
    /// BattleEngine 是唯一战斗状态源；表现层只读、只发指令。
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private long seed = 12345;
        [SerializeField, Min(0f)] private float actionDuration = 0.25f;
        [SerializeField, Min(0f)] private float actionGap = 0.25f;
        private Coroutine _driver;
        private int _generation;

        [Header("阵型布局（Play 中可实时调，调好抄回 PositioningView 默认值）")]
        [SerializeField] private BattleLayout layout = new BattleLayout();

        private readonly List<LevelConfig> _levels = new List<LevelConfig>();
        private int _levelIndex = 0;

        private BattleEngine _engine;
        private readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();
        private string _selectedActor;
        private SkillSpec _pendingSkill;   // 待选目标的技能
        private readonly List<string> _log = new List<string>();   // 左栏战报（逐条伤害/治疗）
        private long _logCursor;                                    // 已播报的引擎事件下标
        private Transform _worldRoot;
        private Camera _cam;
        private BattleUi _ui;
        private RadialSkillMenu _radial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureControllerInScene()
        {
            // 真机引导兜底：场景磁盘版若没存 BattleController，运行时自动建，
            // 不依赖编辑器菜单保存（候选根因 2 修复）。
            if (Object.FindFirstObjectByType<BattleController>() != null) return;
            new GameObject("BattleController").AddComponent<BattleController>();
        }

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            _cam = EnsureCamera();
            BuildUi();
            NewBattle();
        }

        private void Update()
        {
            // Android 返回键退出（演示 MVP）
            if (Input.GetKeyDown(KeyCode.Escape))
                Application.Quit();

            if (_engine == null) return;
            HandleUnitClick();
        }

        // ---------- 相机 ----------

        private Camera EnsureCamera()
        {
            // 场景里若已有 MainCamera（默认 SampleScene 是透视相机），必须强制成正交并归位：
            // 透视相机的 ScreenToWorldPoint 落点不在世界 z=0 平面 → 点选必然打空。
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.14f);
            Debug.Log("[CQ.Cam] ortho=" + cam.orthographic + " size=" + cam.orthographicSize + " pos=" + cam.transform.position);
            return cam;
        }

        // ---------- UI 构建 ----------

        private void BuildUi()
        {
            _ui = BattleUi.Build(transform);
            _ui.OnEndRound = BeginActionsAction;
            _ui.OnRestart = () => NewBattle();
            _ui.OnNextLevel = NextLevelAction;
            _ui.OnRetry = () => NewBattle();

            _radial = RadialSkillMenu.Build(transform);
            _radial.Bind(_ui.CanvasRect, _cam);
            _radial.OnPick = OnRadialPicked;
            _radial.OnCancel = () =>
            {
                if (_engine != null && _engine.CancelUltimate(_selectedActor))
                {
                    RefreshWorld();
                    RefreshHud();
                }
            };
            _radial.Hide();
        }

        // ---------- 点选输入 ----------

        private const float TapRadius = 0.9f;   // 触控容错半径（世界单位）
        [SerializeField] private bool verboseClick = false;   // 诊断日志（点选打空时打开看 screen/world/overUi/hit）

        private void HandleUnitClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            Vector2 screen = Input.mousePosition;
            Vector3 world = _cam.ScreenToWorldPoint(screen);
            var point = new Vector2(world.x, world.y);
            bool overUi = PointerOverUi();

            // 不用默认 layerMask（默认排除 Ignore Raycast 层，会静默打空）
            // 必须取「离点击点最近」的单位，不能取查询数组的第一个：多个碰撞体同时命中时
            // 数组顺序固定，会导致某个角色永远选不中。
            Collider2D hit = PickUnitCollider(Physics2D.OverlapPointAll(point, Physics2D.AllLayers), point);
            if (hit == null)
                hit = PickUnitCollider(Physics2D.OverlapCircleAll(point, TapRadius, Physics2D.AllLayers), point);

            if (verboseClick)
            {
                var probe = hit != null ? hit.GetComponent<UnitView>() : null;
                Debug.Log("[CQ.Click] screen=" + screen + " world=" + point.ToString("F2")
                    + " overUi=" + overUi + " hit=" + (hit != null ? hit.name : "null")
                    + " unit=" + (probe != null ? probe.UnitId : "null"));
            }

            if (overUi) return;

            if (hit == null)
            {
                // 点空白：取消待选目标
                if (_pendingSkill != null) { _pendingSkill = null; RefreshSelection(); }
                return;
            }

            var uv = hit.GetComponent<UnitView>();
            if (uv == null) return;
            OnUnitTapped(_engine.Ctx.GetUnit(uv.UnitId));
        }

        /// <summary>取离点击点最近、且带 UnitView 的碰撞体。多个命中时按到点击点的距离决胜。</summary>
        private static Collider2D PickUnitCollider(Collider2D[] hits, Vector2 point)
        {
            if (hits == null) return null;
            Collider2D best = null;
            float bestSqr = float.MaxValue;
            foreach (var h in hits)
            {
                if (h == null || h.GetComponent<UnitView>() == null) continue;
                float d = (h.bounds.center - (Vector3)point).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = h; }
            }
            return best;
        }

        private bool PointerOverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (Input.touchCount > 0) return es.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return es.IsPointerOverGameObject();
        }

        private void OnUnitTapped(Unit unit)
        {
            if (unit == null || unit.IsDead || _engine.IsFinished) return;

            if (unit.Team == Team.Player)
            {
                // 待选目标的单体治疗：点友方 = 定目标，不能把选中角色顶掉
                if (_pendingSkill != null && NeedsSingleAlly(_pendingSkill))
                {
                    TryRequestUltimate(_selectedActor, _pendingSkill.id, unit.Id);
                    return;   // TryRequestUltimate 内部已刷选择态与 HUD
                }

                _selectedActor = unit.Id;
                _pendingSkill = null;
                ShowRadial(unit.Id);
            }
            else
            {
                if (_pendingSkill == null) return;
                else if (NeedsSingleAlly(_pendingSkill))
                {
                    Log("此技能点友方");
                }
                else
                {
                    TryRequestUltimate(_selectedActor, _pendingSkill.id, unit.Id);
                }
            }
            RefreshSelection();
        }

        private void ShowRadial(string actorId)
        {
            if (!_views.TryGetValue(actorId, out var view)) return;
            var ult = _engine.UltimateOf(actorId);
            var skills = ult == null ? new List<SkillSpec>() : new List<SkillSpec> { ult };
            _radial.Show(view.transform.position, skills, s => _engine != null && _engine.CanRequestUltimate(actorId),
                () => _engine != null && _engine.CanCancelUltimate(actorId),
                () => _engine != null && _engine.HasUltimateRequest(actorId));
        }

        private void OnRadialPicked(SkillSpec s)
        {
            if (s == null || string.IsNullOrEmpty(_selectedActor) || !_engine.CanRequestUltimate(_selectedActor)) return;
            if (NeedsTarget(s))
            {
                _pendingSkill = s;
                Log(s.name + "：请点目标");
                RefreshSelection();
            }
            else
            {
                TryRequestUltimate(_selectedActor, s.id, null);
            }
        }

        private static bool NeedsSingleEnemy(SkillSpec s) => s.damage != null && s.damage.target == "snipe";
        private static bool NeedsSingleAlly(SkillSpec s) => s.heal != null && s.heal.target == "single";
        private static bool NeedsTarget(SkillSpec s) => NeedsSingleEnemy(s) || NeedsSingleAlly(s);

        private void TryRequestUltimate(string actorId, string skillId, string targetId)
        {
            _pendingSkill = null;
            if (_engine.RequestUltimate(actorId, targetId))
            {
                Log(UnitName(actorId) + " " + SkillName(actorId, skillId) + "（已排队）");
                EnsureDriver();
            }
            else
            {
                Log("终结技失败（充能/目标不符）");
            }
            ShowRadial(actorId);
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
        }

        // ---------- 建局 ----------

        private void StopDriver()
        {
            _generation++;
            if (_driver != null) StopCoroutine(_driver);
            _driver = null;
        }

        private void OnDestroy() => StopDriver();

        private void NewBattle()
        {
            StopDriver();
            _engine = null;
            _log.Clear();
            _logCursor = 0;
            _pendingSkill = null;
            if (_radial != null) _radial.Hide();

            var chars = ConfigLoader.LoadCharacters();
            var enemies = ConfigLoader.LoadEnemies();
            var cards = ConfigLoader.LoadCards();
            var levels = ConfigLoader.LoadLevels();
            var formation = ConfigLoader.LoadFormation();

            int errors = chars.errors.Count + enemies.errors.Count + cards.errors.Count + levels.errors.Count + formation.errors.Count;
            if (errors > 0)
            {
                DumpErrors("角色", chars.errors);
                DumpErrors("敌人", enemies.errors);
                DumpErrors("卡", cards.errors);
                DumpErrors("关卡", levels.errors);
                DumpErrors("阵型", formation.errors);
                _ui.ShowError("配置错误，查看 Console");
                return;
            }

            _ui.ClearError();

            _levels.Clear();
            _levels.AddRange(levels.items.OrderBy(l => l.id));
            if (_levels.Count == 0)
            {
                _ui.ShowError("无可用关卡");
                return;
            }
            if (_levelIndex < 0) _levelIndex = 0;
            if (_levelIndex >= _levels.Count) _levelIndex = _levels.Count - 1;
            var level = _levels[_levelIndex];

            var catalog = enemies.items.ToDictionary(e => e.id);
            var wave = level.waves[0];
            _engine = BattleEngine.Create(seed + _levelIndex, chars.items, wave.enemies, catalog, cards.items,
                wave.slots, formation.items[0]);
            _engine.StartBattle();

            _selectedActor = _engine.Players.FirstOrDefault(p => !p.IsDead)?.Id;

            SpawnWorld();
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
            Log("开战 " + (_levelIndex + 1) + "/" + _levels.Count + "：" + level.name + "，普攻自动执行，点我方角色释放终结技");
            if (!string.IsNullOrEmpty(_selectedActor)) ShowRadial(_selectedActor);   // 开局即亮当前角色技能
        }

        private void SpawnWorld()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            _views.Clear();
            _worldRoot = new GameObject("BattleWorld").transform;
            _worldRoot.SetParent(transform, false);

            foreach (var p in _engine.Players) SpawnUnit(p);
            foreach (var e in _engine.Enemies) SpawnUnit(e);
        }

        private void SpawnUnit(Unit unit)
        {
            var go = new GameObject("Unit_" + unit.Id);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = PositioningView.Position(unit, layout);
            // 不缩放根节点：缩放会同时放大碰撞体，造成相邻单位碰撞体重叠、选不中
            go.transform.localScale = Vector3.one;

            var view = go.AddComponent<UnitView>();
            view.Setup(unit, UnitColor(unit, unit.IsDead));
            _views[unit.Id] = view;
        }

        private static Color UnitColor(Unit unit, bool dead)
        {
            if (dead) return Color.gray;
            Color c = unit.Team == Team.Player ? new Color(0.30f, 0.85f, 0.90f) : new Color(0.90f, 0.35f, 0.35f);
            if (unit.Row == "back") c *= 0.75f;
            return c;
        }

        private void RefreshWorld()
        {
            if (_engine == null) return;
            foreach (var u in _engine.Ctx.Units)
            {
                if (!_views.TryGetValue(u.Id, out var v)) continue;
                IntentConfig it = (u.Team == Team.Enemy && !u.IsDead) ? _engine.CurrentIntentOf(u.Id) : null;
                v.Refresh(u, it != null ? IntentText(it) : "", it != null ? it.type : "");
                v.SetAppearance(UnitColor(u, u.IsDead), u.IsDead);
            }
        }

        private void RefreshSelection()
        {
            if (_engine == null) return;
            bool needEnemy = _pendingSkill != null && NeedsSingleEnemy(_pendingSkill);
            bool needAlly = _pendingSkill != null && NeedsSingleAlly(_pendingSkill);
            foreach (var u in _engine.Ctx.Units)
            {
                if (!_views.TryGetValue(u.Id, out var v)) continue;
                if (u.IsDead) { v.SetHighlight(0); continue; }
                if (u.Id == _selectedActor && _pendingSkill == null) { v.SetHighlight(1); continue; }
                if (needEnemy && u.Team == Team.Enemy) { v.SetHighlight(2); continue; }
                if (needAlly && u.Team == Team.Player) { v.SetHighlight(2); continue; }
                v.SetHighlight(0);
            }
        }

        private void RefreshHud()
        {
            if (_ui == null) return;
            string order = "";
            string winner = "";
            if (_engine != null)
            {
                var cur = _engine.CurrentOrder;
                order = (cur == null || cur.Count == 0)
                    ? "(尚未出手)"
                    : string.Join("\n", cur.Select(u =>
                    {
                        string tag = (u.Team == Team.Player ? "我 " : "敌 ") + u.Name + (u.IsDead ? "(亡)" : "");
                        return tag;
                    }));
                if (_engine.IsFinished && !_engine.IsActionOccupied)
                    winner = _engine.Winner == Team.Player ? "我方胜利" : "我方失败";
            }
            bool hasNext = HasNextLevel();
            _ui.Refresh(_engine != null ? _engine.Round : 1, order, winner, hasNext ? "下一关 → " + _levels[_levelIndex + 1].name : "", hasNext);
            _ui.EndRoundButton.interactable = _engine != null && _engine.CanPlayCards;
            _ui.SetEvent(LogText());
        }

        // ---------- 指令 ----------

        private void BeginActionsAction()
        {
            if (_engine == null || !_engine.BeginActions()) return;
            RefreshHud();
            EnsureDriver();
        }

        private void EnsureDriver()
        {
            if (_driver == null && _engine != null) _driver = StartCoroutine(Drive(_engine, _generation));
        }

        private bool IsCurrent(BattleEngine engine, int generation) =>
            generation == _generation && ReferenceEquals(engine, _engine);

        private IEnumerator Drive(BattleEngine engine, int generation)
        {
            yield return null;
            if (!IsCurrent(engine, generation)) yield break;
            while (true)
            {
                var result = engine.Advance();
                if (result == null) break;
                RefreshWorld();
                ConsumeAction(result);
                RefreshSelection();
                RefreshHud();
                yield return new WaitForSeconds(Mathf.Max(0f, actionDuration));
                if (!IsCurrent(engine, generation)) yield break;
                while (_views.Values.Any(view => view != null && !view.FeedbackComplete))
                {
                    yield return null;
                    if (!IsCurrent(engine, generation)) yield break;
                }
                yield return new WaitForSeconds(Mathf.Max(0f, actionGap));
                if (!IsCurrent(engine, generation)) yield break;
                engine.Confirm(engine.BattleId, result.Sequence);
            }
            if (!IsCurrent(engine, generation)) yield break;
            _driver = null;
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
        }

        private void ConsumeAction(ActionResult result)
        {
            if (result.Sequence <= _logCursor) return;
            bool any = false;
            foreach (var change in result.UnitChanges)
            {
                if (change.HpDelta == 0) continue;
                any = true;
                PushLog(FormatEvent(new BattleEvent { Kind = result.Kind, ActorId = result.ActorId,
                    ActionId = result.ActionId, TargetId = change.UnitId, HpDelta = change.HpDelta,
                    TargetDead = change.After.IsDead }));
                if (_views.TryGetValue(change.UnitId, out var view))
                {
                    view.FloatText((change.HpDelta > 0 ? "+" : "") + change.HpDelta,
                        change.HpDelta > 0 ? Color.green : Color.red, change.HpDelta);
                    view.Flash(change.HpDelta);
                }
            }
            if (!any) PushLog(FormatEvent(new BattleEvent { Kind = result.Kind, ActorId = result.ActorId,
                ActionId = result.ActionId }));
            foreach (var hit in result.Hits)
            {
                if (hit.ShieldAbsorbed <= 0) continue;
                PushLog(UnitName(hit.TargetUnitId) + " 护盾吸收 " + hit.ShieldAbsorbed);
                if (hit.HpDamage == 0 && _views.TryGetValue(hit.TargetUnitId, out var view))
                {
                    view.FloatText("护盾 " + hit.ShieldAbsorbed, Color.cyan);
                    view.Flash(-1);
                }
            }
            _logCursor = result.Sequence;
        }

        private string FormatEvent(BattleEvent ev)
        {
            string action = ActionLabel(ev);
            if (ev.Kind == BattleEventKind.Card)
                return "卡" + action + " → " + UnitName(ev.TargetId);
            string actor = UnitName(ev.ActorId);
            if (ev.HpDelta == 0) return actor + " " + action;
            return actor + " " + action + " → " + UnitName(ev.TargetId)
                + " " + (ev.HpDelta > 0 ? "+" : "") + ev.HpDelta + (ev.TargetDead ? "（倒）" : "");
        }

        private string ActionLabel(BattleEvent ev)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Skill:
                    return "〔" + SkillName(ev.ActorId, ev.ActionId) + "〕";
                case BattleEventKind.Card:
                    var card = _engine.GetCard(ev.ActionId);
                    return "〔" + (card != null ? card.name : ev.ActionId) + "〕";
                default:
                    return "〔" + IntentLabel(ev.ActionId) + "〕";
            }
        }

        private static string IntentLabel(string type)
        {
            switch (type)
            {
                case "attack": return "攻击";
                case "multi": return "连击";
                case "charge": return "蓄力";
                case "attackUp": return "攻增";
                case "debuff": return "减益";
                default: return type;
            }
        }

        private void NextLevelAction()
        {
            if (!HasNextLevel()) return;
            _levelIndex++;
            NewBattle();
        }

        private bool HasNextLevel() =>
            _engine != null && _engine.IsFinished && !_engine.IsActionOccupied &&
            _engine.Winner == Team.Player && _levelIndex + 1 < _levels.Count;

        // ---------- 意图文本 ----------

        private static string IntentText(IntentConfig it)
        {
            if (it == null) return "";
            switch (it.type)
            {
                case "attack":
                    return it.target == "all" ? "全体 -" + it.damage : "单体 -" + it.damage;
                case "multi":
                    return "单体 -" + (it.damage * it.hits);
                case "charge":
                    return "蓄 单体 -" + it.nextDamage;
                case "attackUp":
                    return "↑";
                case "debuff":
                    return "↓";
                default:
                    return it.type;
            }
        }

        // ---------- 辅助 ----------

        private void Log(string msg)
        {
            Debug.Log("[CQ.Battle] " + msg);
            PushLog(msg);
        }

        private const int LogMax = 7;   // 左栏战报最多保留行数（更多会顶掉最旧一条；行长会折行，故不宜多）

        private void PushLog(string msg)
        {
            _log.Add(msg);
            while (_log.Count > LogMax) _log.RemoveAt(0);
            RefreshHud();
        }

        private string LogText() => string.Join("\n", _log);

        private void DumpErrors(string label, List<string> errors)
        {
            foreach (var e in errors) Debug.LogError("[" + label + "] " + e);
        }

        private string UnitName(string id)
        {
            var u = _engine != null ? _engine.Ctx.GetUnit(id) : null;
            return u != null ? u.Name : (id ?? "无");
        }

        private string SkillName(string charId, string skillId)
        {
            var skills = _engine.SkillsOf(charId);
            if (skills == null) return skillId;
            foreach (var s in skills) if (s.id == skillId) return s.name;
            return skillId;
        }
    }
}