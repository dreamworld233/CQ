using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Cards;
using CQ.Core.Combat;
using CQ.Core.Config;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CQ.Runtime
{
    /// <summary>
    /// 场景驱动桥接：加载配置 → 建 BattleEngine → 生成世界单位 →
    /// 战前布阵与一键终结技+ uGUI HUD（战力条/结束回合）。
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
        private int _selectedCard = -1;
        private BattleFlow _cardSelectionFlow;
        private readonly Dictionary<string, TMPro.TextMeshPro> _slotLabels = new Dictionary<string, TMPro.TextMeshPro>();
        private readonly List<string> _log = new List<string>();   // 左栏战报（逐条伤害/治疗）
        private long _logCursor;                                    // 已播报的引擎事件下标
        private Transform _worldRoot;
        private Transform _slotRoot;
        private Camera _cam;
        private BattleUi _ui;
        private readonly Dictionary<string, string> _savedSlots = new Dictionary<string, string>();

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
            _ui.OnSelectCard = SelectCard;
            _ui.OnCancelCard = () => { ClearCardSelection(); RefreshHud(); };
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

            // Card targeting owns world input. In particular, never fall through to an ultimate or move.
            if (_selectedCard >= 0)
            {
                if (!_engine.CanPlayCards) { ClearCardSelection(); RefreshHud(); return; }
                if (SelectedCardIsSlotEffect())
                {
                    foreach (Team team in new[] { Team.Player, Team.Enemy })
                        foreach (var slot in _engine.Ctx.FormationOf(team).Slots)
                        {
                            if (!SlotContainsPoint(team, slot.Id, point)) continue;
                            if (_engine.CanPlaySlotCard(_selectedCard, team, slot.Id) &&
                                _engine.PlaySlotCard(_selectedCard, team, slot.Id)) CardPlayed();
                            return;
                        }
                    return;
                }
                if (hit != null)
                {
                    var target = hit.GetComponent<UnitView>();
                    if (target != null && _engine.CanPlayCard(_selectedCard, target.UnitId) &&
                        _engine.PlayCard(_selectedCard, target.UnitId)) CardPlayed();
                }
                return;
            }

            // Slot targets take priority over overlapping unit colliders during placement.
            if (_engine.Flow == BattleFlow.Setup && _selectedActor != null)
            {
                foreach (var slot in _engine.Ctx.FormationOf(Team.Player).Slots)
                {
                    if (!SlotContainsPoint(Team.Player, slot.Id, point)) continue;
                    if (_engine.TryMovePlayer(_selectedActor, slot.Id))
                    {
                        SaveFormation();
                        foreach (var player in _engine.Players)
                            _views[player.Id].SetPosition(PositioningView.Position(player, layout), player);
                        RefreshWorld();
                        RefreshSelection();
                    }
                    return;
                }
            }
            if (hit == null) return;

            var uv = hit.GetComponent<UnitView>();
            if (uv == null) return;
            OnUnitTapped(_engine.Ctx.GetUnit(uv.UnitId));
        }

        private Vector3 SlotTargetPosition(Team team, string slotId) =>
            PositioningView.Position(team, slotId, layout) + Vector3.down * (0.78f * 0.72f);

        private bool SlotContainsPoint(Team team, string slotId, Vector2 point)
        {
            var center = SlotTargetPosition(team, slotId);
            var delta = point - (Vector2)center;
            return delta.x * delta.x / (0.48f * 0.48f) + delta.y * delta.y / (0.23f * 0.23f) <= 1f;
        }

        private bool SelectedCardIsSlotEffect() => _engine != null && _selectedCard >= 0 &&
            _selectedCard < _engine.Hand.Count &&
            CardEffectResolver.IsSlotEffect(_engine.GetCard(_engine.Hand[_selectedCard])?.effect);

        private void SelectCard(int index)
        {
            if (_engine == null || !_engine.CanPlayCards || index < 0 || index >= _engine.Hand.Count) return;
            if (_engine.CanPlayRemovalCard(index))
            {
                if (_engine.PlayRemovalCard(index)) CardPlayed();
                return;
            }
            if (CardEffectResolver.IsGroupEffect(_engine.GetCard(_engine.Hand[index])?.effect))
            {
                if (_engine.PlayGroupCard(index)) CardPlayed();
                return;
            }
            if (_selectedCard == index) ClearCardSelection();
            else
            {
                _selectedCard = index;
                _cardSelectionFlow = _engine.Flow;
                _ui.SetCardTargeting(true);
                RefreshSelection();
            }
            RefreshHud();
        }

        private void ClearCardSelection()
        {
            _selectedCard = -1;
            if (_ui != null) _ui.SetCardTargeting(false);
            RefreshSelection();
        }

        private void CardPlayed()
        {
            ClearCardSelection();
            // Instant card results are appended, not driven or Confirm'ed as occupied actions.
            var results = _engine.ActionResults;
            if (results.Count > 0) ConsumeAction(results[results.Count - 1]);
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
        }

        private string CardTargetHint()
        {
            string name = _engine.GetCard(_engine.Hand[_selectedCard]).name;
            if (SelectedCardIsSlotEffect()) return name + "：点双方圆盘（含空格），或取消";
            var effect = _engine.GetCard(_engine.Hand[_selectedCard])?.effect;
            return name + (effect != null && (effect.type == "damage" || effect.type == "vulnerable" ||
                effect.type == "interrupt" || effect.type == "weaken" || effect.type == "slow")
                ? "：点圈中存活敌人，或取消" : "：点圈中存活我方，或取消");
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
            if (_selectedCard >= 0) return;
            if (unit == null || unit.IsDead || unit.Team != Team.Player || _engine.IsFinished) return;
            if (_engine.Flow == BattleFlow.Setup)
            {
                _selectedActor = unit.Id;
                RefreshSelection();
                Log("已选 " + unit.Name + "，点我方脚下格子圆盘移动/交换");
                return;
            }
            TryRequestUltimate(unit.Id);
        }

        private void TryRequestUltimate(string actorId)
        {
            if (_engine == null || _selectedCard >= 0) return;
            ClearCardSelection();
            if (!_engine.RequestUltimate(actorId)) return;
            Log(UnitName(actorId) + " 终结技（已排队）");
            EnsureDriver();
            RefreshWorld();
            RefreshHud();
        }

        private void SaveFormation()
        {
            if (_engine == null) return;
            foreach (var player in _engine.Players) _savedSlots[player.Id] = player.SlotId;
        }

        private void RestoreFormation()
        {
            // Each desired destination is unique. Moving via swaps also handles cycles of any length.
            foreach (var player in _engine.Players)
                if (_savedSlots.TryGetValue(player.Id, out var slot)) _engine.TryMovePlayer(player.Id, slot);
            SaveFormation();
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
            ClearCardSelection();
            _ui.RefreshHand(null, -1, "");
            SaveFormation();
            _engine = null;
            _ui.ClearUltimateBindings();
            _log.Clear();
            _logCursor = 0;
            _selectedActor = null;

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

            string referenceError = ConfigValidator.ValidateCardReferences(chars.items, cards.items.ToDictionary(c => c.id));
            if (referenceError != null)
            {
                Debug.LogError(referenceError);
                _ui.ShowError("注牌配置错误，查看 Console");
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
            string levelError = ConfigValidator.ValidateLevelReferences(level, chars.items, catalog,
                cards.items.ToDictionary(card => card.id));
            if (levelError != null)
            {
                Debug.LogError(levelError);
                _ui.ShowError("关卡配置错误，查看 Console");
                return;
            }
            _engine = BattleEngine.Create(seed + _levelIndex, chars.items, level, catalog, cards.items, formation.items[0]);
            RestoreFormation();

            SpawnWorld();
            RefreshWorld();
            RefreshSelection();
            RefreshHud();
            Log("战前布阵：选角色后点我方脚下格子圆盘，点占格交换；布阵完成点开战");
        }

        private void SpawnWorld()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            _views.Clear();
            _slotLabels.Clear();
            _worldRoot = new GameObject("BattleWorld").transform;
            _worldRoot.SetParent(transform, false);

            _slotRoot = new GameObject("SetupSlots").transform;
            _slotRoot.SetParent(_worldRoot, false);
            foreach (Team team in new[] { Team.Player, Team.Enemy })
                foreach (var slot in _engine.Ctx.FormationOf(team).Slots)
                {
                    var go = new GameObject(team + "_" + slot.Id);
                    go.transform.SetParent(_slotRoot, false);
                    go.transform.position = SlotTargetPosition(team, slot.Id);
                    go.transform.localScale = new Vector3(0.95f, 0.42f, 1f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = UiKit.CircleSprite();
                    sr.color = team == Team.Player ? new Color(0.3f, 0.8f, 1f, 0.65f) : new Color(1f, 0.4f, 0.3f, 0.5f);
                    sr.sortingOrder = 1;
                    var label = TmpKit.MakeWorldText(_slotRoot, team + "_" + slot.Id + "_Target",
                        go.transform.position, UiKit.Font, 0.19f, Color.white, 65,
                        TMPro.FontStyles.Normal, "格子");
                    TmpKit.SetOutline(label, Color.black, 0.22f);
                    label.gameObject.SetActive(false);
                    _slotLabels[team + ":" + slot.Id] = label;
                }
            foreach (var p in _engine.Players) SpawnUnit(p);
            foreach (var e in _engine.Enemies) SpawnUnit(e);
        }

        private void SpawnUnit(Unit unit)
        {
            var go = new GameObject("Unit_" + unit.Id);
            go.transform.SetParent(_worldRoot, false);
            go.transform.position = PositioningView.Position(unit, layout);
            // Uniformly reduce art/bars/collider to fit both five-slot formations.
            go.transform.localScale = Vector3.one * 0.72f;

            var view = go.AddComponent<UnitView>();
            view.Setup(unit, UnitColor(unit, unit.IsDead));
            _views[unit.Id] = view;
            if (unit.Team == Team.Player) _ui.BindUltimate(unit.Id, view.transform, _engine, _cam, TryRequestUltimate);
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
                var preview = u.Team == Team.Enemy && !u.IsDead && _engine.Flow != BattleFlow.Setup
                    ? _engine.GetIntentPreview(u.Id) : null;
                v.Refresh(u, preview != null ? IntentPreviewText(preview) : "", preview?.Type ?? "", UnitStatusText(u));
                v.SetAppearance(UnitColor(u, u.IsDead), u.IsDead);
            }
        }

        private void RefreshSelection()
        {
            if (_engine == null) return;
            bool slotTargeting = SelectedCardIsSlotEffect() && _engine.CanPlayCards;
            if (_slotRoot != null) _slotRoot.gameObject.SetActive(_engine.Flow == BattleFlow.Setup || slotTargeting);
            foreach (var u in _engine.Ctx.Units)
                if (_views.TryGetValue(u.Id, out var view))
                    view.SetHighlight(_selectedCard >= 0 && !slotTargeting && _engine.CanPlayCard(_selectedCard, u.Id) ? 2 :
                        _engine.Flow == BattleFlow.Setup && u.Id == _selectedActor ? 1 : 0);
            foreach (Team team in new[] { Team.Player, Team.Enemy })
                foreach (var slot in _engine.Ctx.FormationOf(team).Slots)
                {
                    if (!_slotLabels.TryGetValue(team + ":" + slot.Id, out var label)) continue;
                    label.gameObject.SetActive(slotTargeting);
                    if (!slotTargeting) continue;
                    string occupant = slot.Occupant == null ? "空格" : slot.Occupant.IsDead ? "阵亡" : slot.Occupant.Name;
                    label.text = (team == Team.Player ? "我方 " : "敌方 ") + SlotLabel(slot.Id) + " " + occupant +
                        "\n嘲讽 " + slot.EffectiveTaunt + " 修正 " + (slot.TauntModifier >= 0 ? "+" : "") + slot.TauntModifier;
                    TmpKit.FitWorldHeight(label, 0.34f);
                }
        }

        private void RefreshHud()
        {
            if (_ui == null) return;
            if (_selectedCard >= 0 && (_engine == null || !_engine.CanPlayCards ||
                _engine.Flow != _cardSelectionFlow || _selectedCard >= _engine.Hand.Count)) ClearCardSelection();
            _ui.RefreshHand(_engine, _selectedCard, _selectedCard >= 0 ? CardTargetHint() : "");
            _ui.RefreshTutorial(_levels.Count > _levelIndex ? _levels[_levelIndex] : null, _engine);
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
            _ui.Refresh(_engine?.CurrentRound ?? 1, order, winner, hasNext ? "下一关 → " + _levels[_levelIndex + 1].name : "", hasNext);
            bool setup = _engine != null && _engine.Flow == BattleFlow.Setup;
            _ui.EndRoundButton.GetComponentInChildren<TMPro.TextMeshProUGUI>().text = setup ? "开战" : "开始行动";
            _ui.EndRoundButton.interactable = _engine != null && (setup || _engine.CanPlayCards);
            _ui.SetEvent(LogText());
        }

        // ---------- 指令 ----------

        private void BeginActionsAction()
        {
            if (_engine == null) return;
            ClearCardSelection();
            if (_engine.Flow == BattleFlow.Setup)
            {
                SaveFormation();
                _selectedActor = null;
                _engine.StartBattle();
                if (_slotRoot != null) _slotRoot.gameObject.SetActive(false);
                RefreshWorld();
                RefreshSelection();
                RefreshHud();
                return;
            }
            if (!_engine.BeginActions()) return;
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
            bool any = result.InjectedCardIds.Count > 0 || result.RemovedCardId != null;
            if (result.InjectedCardIds.Count > 0)
            {
                string cards = string.Join("、", result.InjectedCardIds.GroupBy(id => id)
                    .Select(group => (_engine.GetCard(group.Key)?.name ?? group.Key) + "×" + group.Count()));
                PushLog(UnitName(result.ActorId) + " 终结技 → 注入抽牌堆：" + cards + "（后续抽取）");
                if (_views.TryGetValue(result.ActorId, out var caster)) caster.FloatText("注牌 " + cards, Color.cyan);
            }
            if (result.RemovedCardId != null)
                PushLog("卡〔" + (_engine.GetCard(result.RemovedCardId)?.name ?? result.RemovedCardId) + "〕 → 移出本场，不回洗");
            if (result.Kind == BattleEventKind.Card && result.RemovedCardId == null)
            {
                any = true;
                string target = result.TargetTeam.HasValue && !string.IsNullOrEmpty(result.TargetSlotId)
                    ? (result.TargetTeam.Value == Team.Player ? "我方 " : "敌方 ") +
                        SlotLabel(result.TargetSlotId) + "（" + result.TargetSlotId + "）"
                    : result.TargetTeam.HasValue ? "全体敌人" : UnitName(result.TargetUnitId);
                var card = _engine.GetCard(result.ActionId);
                PushLog("卡〔" + (card != null ? card.name : result.ActionId) + "〕 → " + target);
            }
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
                return "卡" + action + " → " + UnitName(ev.TargetId) +
                    (ev.HpDelta == 0 ? "" : " " + (ev.HpDelta > 0 ? "+" : "") + ev.HpDelta + (ev.TargetDead ? "（倒）" : ""));
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

        private string UnitStatusText(Unit unit)
        {
            if (unit.IsDead) return "";
            var tags = new List<string>();
            foreach (var status in _engine.Ctx.GetStatuses(unit.Id))
            {
                switch (status.Type)
                {
                    case StatusType.Shield: tags.Add("盾" + status.Amount); break;
                    case StatusType.AttackUp: tags.Add("攻+" + status.Percent + "%"); break;
                    case StatusType.Weaken: tags.Add("弱" + status.Percent + "%"); break;
                    case StatusType.Slow: tags.Add("慢" + status.Percent + "%"); break;
                    case StatusType.Interrupt: tags.Add("打断"); break;
                    case StatusType.Vulnerable: tags.Add("易伤+" + status.Percent + "%"); break;
                }
                if (status.Turn == "round") tags[tags.Count - 1] += "(" + status.Duration + "回合)";
            }
            int haste = _engine.Ctx.GetHaste(unit.Id);
            if (haste > 0) tags.Add("急" + haste + "%");
            return string.Join(" ", tags);
        }

        private static string IntentPreviewText(EnemyIntentPreview preview)
        {
            var lines = new List<string>();
            if (preview.PendingRelease != null) lines.Add("蓄放 " + IntentStepText(preview.PendingRelease));
            // Displayed numbers are source-modified attack power, not target HP loss.
            if (preview.Current != null)
                lines.Add((preview.PendingRelease != null ? "续 " : "") + IntentStepText(preview.Current));
            if (lines.Count > 0) lines.Add("源伤害，另计易伤/护盾");
            return string.Join("\n", lines);
        }

        // Numbers come from the Core source-modified, pre-shield preview, never re-derived here.
        private static string IntentStepText(EnemyIntentPreviewStep step)
        {
            var parts = new List<string>();
            if (step.Suppressed) parts.Add("取消");
            if (step.Hits > 0)
                parts.Add((step.Target == "all" ? "全" : "单") + step.DamagePerHit + "x" + step.Hits);
            if (step.Type == "charge")
                parts.Add("蓄 下轮" + step.NextRoundDamage + (step.Interruptible ? " 可断" : " 不可断"));
            if (step.BuffPercent > 0) parts.Add("攻+" + step.BuffPercent + "%");
            if (!string.IsNullOrEmpty(step.DebuffType))
                parts.Add((step.DebuffType == "weaken" ? "虚弱" : "减速") + step.DebuffPercent + "%");
            return string.Join(" ", parts);
        }


        // ---------- 辅助 ----------

        private void Log(string msg)
        {
            Debug.Log("[CQ.Battle] " + msg);
            PushLog(msg);
        }

        private void PushLog(string msg)
        {
            _log.Add(msg);
            RefreshHud();
        }

        private string LogText() => string.Join("\n", _log);

        private void DumpErrors(string label, List<string> errors)
        {
            foreach (var e in errors) Debug.LogError("[" + label + "] " + e);
        }

        private static string SlotLabel(string slotId) =>
            (Formation.RowOf(slotId) == "front" ? "前" : "后") + slotId[slotId.Length - 1];

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
