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

        [Header("阵型布局（Play 中可实时调，调好抄回 PositioningView 默认值）")]
        [SerializeField] private BattleLayout layout = new BattleLayout();

        private readonly List<LevelConfig> _levels = new List<LevelConfig>();
        private int _levelIndex = 0;

        private BattleEngine _engine;
        private readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();
        private readonly Dictionary<string, string> _queued = new Dictionary<string, string>();   // 本回合已登记技能
        private string _selectedActor;
        private string _selectedTarget;
        private SkillSpec _pendingSkill;   // 待选目标的技能
        private readonly List<string> _log = new List<string>();   // 左栏战报（逐条伤害/治疗）
        private int _logCursor;                                    // 已播报的引擎事件下标
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
            _ui.OnEndRound = EndRoundAction;
            _ui.OnRestart = () => NewBattle();
            _ui.OnNextLevel = NextLevelAction;
            _ui.OnRetry = () => NewBattle();

            _radial = RadialSkillMenu.Build(transform);
            _radial.Bind(_ui.CanvasRect, _cam);
            _radial.OnPick = OnRadialPicked;
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
            if (unit == null || unit.IsDead) return;

            if (unit.Team == Team.Player)
            {
                // 待选目标的单体治疗：点友方 = 定目标，不能把选中角色顶掉
                if (_pendingSkill != null && NeedsSingleAlly(_pendingSkill))
                {
                    TryChooseSkill(_selectedActor, _pendingSkill.id, unit.Id);
                    return;   // TryChooseSkill 内部已刷选择态与 HUD
                }

                _selectedActor = unit.Id;
                _pendingSkill = null;
                ShowRadial(unit.Id);
            }
            else
            {
                if (_pendingSkill == null)
                {
                    _selectedTarget = unit.Id;
                    Log("锁定 " + unit.Name);
                }
                else if (NeedsSingleAlly(_pendingSkill))
                {
                    Log("此技能点友方");
                }
                else
                {
                    TryChooseSkill(_selectedActor, _pendingSkill.id, unit.Id);
                }
            }
            RefreshSelection();
        }

        private void ShowRadial(string actorId)
        {
            if (!_views.TryGetValue(actorId, out var view)) return;
            var skills = _engine.SkillsOf(actorId);
            _radial.Show(view.transform.position, skills, s => _engine.CanUseSkill(actorId, s.id));
        }

        private void OnRadialPicked(SkillSpec s)
        {
            if (s == null || string.IsNullOrEmpty(_selectedActor)) return;
            if (NeedsTarget(s))
            {
                _pendingSkill = s;
                Log(s.name + "：请点目标");
                RefreshSelection();
            }
            else
            {
                TryChooseSkill(_selectedActor, s.id, _selectedTarget ?? _selectedActor);
            }
        }

        private static bool NeedsSingleEnemy(SkillSpec s) => s.damage != null && s.damage.target == "single";
        private static bool NeedsSingleAlly(SkillSpec s) => s.heal != null && s.heal.target == "single";
        private static bool NeedsTarget(SkillSpec s) => NeedsSingleEnemy(s) || NeedsSingleAlly(s);

        private void TryChooseSkill(string actorId, string skillId, string targetId)
        {
            _pendingSkill = null;
            if (_engine.ChooseSkill(actorId, skillId, targetId))
            {
                _queued[actorId] = SkillName(actorId, skillId);
                Log(UnitName(actorId) + " " + SkillName(actorId, skillId) + " → " + UnitName(targetId) + "（已登记，结束回合结算）");
            }
            else
            {
                Log("出招失败（能量/目标不符）");
            }
            RefreshSelection();
            RefreshHud();
        }

        // ---------- 建局 ----------

        private void NewBattle()
        {
            _log.Clear();
            _logCursor = 0;
            _pendingSkill = null;
            _queued.Clear();
            if (_radial != null) _radial.Hide();

            var chars = ConfigLoader.LoadCharacters();
            var enemies = ConfigLoader.LoadEnemies();
            var cards = ConfigLoader.LoadCards();
            var levels = ConfigLoader.LoadLevels();

            int errors = chars.errors.Count + enemies.errors.Count + cards.errors.Count + levels.errors.Count;
            if (errors > 0)
            {
                DumpErrors("角色", chars.errors);
                DumpErrors("敌人", enemies.errors);
                DumpErrors("卡", cards.errors);
                DumpErrors("关卡", levels.errors);
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
            _engine = BattleEngine.Create(seed + _levelIndex, chars.items, level.waves[0].enemies, catalog, cards.items);
            _engine.StartBattle();

            _selectedActor = _engine.Players.FirstOrDefault(p => !p.IsDead)?.Id;
            _selectedTarget = _engine.Enemies.FirstOrDefault(e => !e.IsDead)?.Id;

            SpawnWorld();
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
            Log("开战 " + (_levelIndex + 1) + "/" + _levels.Count + "：" + level.name + "，点我方角色选招");
            if (!string.IsNullOrEmpty(_selectedActor)) ShowRadial(_selectedActor);   // 开局即亮当前角色技能
        }

        private void SpawnWorld()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            _views.Clear();
            _worldRoot = new GameObject("BattleWorld").transform;
            _worldRoot.SetParent(transform, false);

            int slot = 0;
            foreach (var p in _engine.Players) SpawnUnit(p, slot++);
            slot = 0;
            foreach (var e in _engine.Enemies) SpawnUnit(e, slot++);
        }

        private void SpawnUnit(Unit unit, int slot)
        {
            var go = new GameObject("Unit_" + unit.Id);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = PositioningView.Position(unit, slot, layout);
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
                v.Refresh(u, it != null ? IntentText(u.Id, it) : "", it != null ? it.type : "");
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
                        if (_queued.TryGetValue(u.Id, out var sk)) tag += "〔" + sk + "〕";
                        return tag;
                    }));
                if (_engine.IsFinished)
                    winner = _engine.Winner == Team.Player ? "我方胜利" : "我方失败";
            }
            bool hasNext = HasNextLevel();
            _ui.Refresh(_engine != null ? _engine.Round : 1, order, winner, hasNext ? "下一关 → " + _levels[_levelIndex + 1].name : "", hasNext);
            _ui.SetEvent(LogText());
        }

        // ---------- 指令 ----------

        private void EndRoundAction()
        {
            if (_engine == null || _engine.IsFinished) return;
            var before = SnapshotHp();
            _engine.EndRound();
            _pendingSkill = null;
            _queued.Clear();
            if (_radial != null) _radial.Hide();
            RefreshWorld();
            RefreshSelection();
            DrainEngineEvents();   // 逐条播报：谁 用什么 → 打谁 掉多少
            FloatDeltas(before);
            RefreshHud();
        }

        /// <summary>把引擎新产生的结算事件排进左栏战报（已播报过的按游标跳过）。</summary>
        private void DrainEngineEvents()
        {
            if (_engine == null) return;
            var events = _engine.RoundEvents;
            if (_logCursor > events.Count) _logCursor = 0;   // 引擎每回合清空，游标跟着回零
            for (; _logCursor < events.Count; _logCursor++)
                PushLog(FormatEvent(events[_logCursor]));
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

        /// <summary>结算前后血量快照差 → 飘字。数值反馈（用户要求能看出掉血/回血）。</summary>
        private Dictionary<string, int> SnapshotHp()
        {
            var map = new Dictionary<string, int>();
            if (_engine == null) return map;
            foreach (var u in _engine.Ctx.Units) map[u.Id] = u.Hp;
            return map;
        }

        private void FloatDeltas(Dictionary<string, int> before)
        {
            if (_engine == null) return;
            foreach (var u in _engine.Ctx.Units)
            {
                if (!before.TryGetValue(u.Id, out int hp0)) continue;
                int delta = u.Hp - hp0;
                if (delta == 0) continue;
                if (!_views.TryGetValue(u.Id, out var v)) continue;
                v.FloatText((delta > 0 ? "+" : "") + delta,
                    delta > 0 ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.45f), delta);
                v.Flash(delta);   // 掉血闪白 + 抖动，回血闪绿
            }
        }

        private void NextLevelAction()
        {
            if (!HasNextLevel()) return;
            _levelIndex++;
            NewBattle();
        }

        private bool HasNextLevel() =>
            _engine != null && _engine.IsFinished && _engine.Winner == Team.Player && _levelIndex + 1 < _levels.Count;

        // ---------- 意图文本 ----------

        private string IntentText(string enemyId, IntentConfig it)
        {
            if (it == null) return "";
            switch (it.type)
            {
                case "attack":
                    return it.target == "all" ? "全体 -" + it.damage : SingleTargetText(enemyId, it.damage);
                case "multi":
                    return SingleTargetText(enemyId, it.damage * it.hits);
                case "charge":
                    return "蓄 " + SingleTargetText(enemyId, it.nextDamage);
                case "attackUp":
                    return "↑";
                case "debuff":
                    return "↓";
                default:
                    return it.type;
            }
        }

        private string SingleTargetText(string enemyId, int damage)
        {
            string targetId = _engine.GetIntentTarget(enemyId);
            string name = targetId != null ? UnitName(targetId) : "单体";
            return name + " -" + damage;
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