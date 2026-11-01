using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private long seed = 12345;
        [SerializeField, Min(0f)] private float actionDuration = 0.42f;
        [SerializeField, Min(0f)] private float actionGap = 0.06f;
        [SerializeField] private BattleLayout layout = new BattleLayout();
        private Coroutine _driver;
        private int _generation;
        private readonly List<LevelConfig> _levels = new List<LevelConfig>();
        private int _levelIndex;
        private BattleEngine _engine;
        private readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();
        private readonly Dictionary<string, string> _savedSlots = new Dictionary<string, string>();
        private FormationDrag _formationDrag;
        private long _presentedSequence;
        private Transform _worldRoot;
        private Transform _slotRoot;
        private Camera _cam;
        private BattleUi _ui;
        private BattleAudio _audio;
        private bool _announcedWinner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureControllerInScene()
        {
            if (Object.FindFirstObjectByType<BattleController>() != null) return;
            new GameObject("BattleController").AddComponent<BattleController>();
        }

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            _cam = EnsureCamera();
            if (FindFirstObjectByType<AudioListener>() == null) _cam.gameObject.AddComponent<AudioListener>();
            _formationDrag = gameObject.AddComponent<FormationDrag>();
            _audio = BattleAudio.Build(transform);
            _ui = BattleUi.Build(transform);
            _ui.OnMuteChanged = muted => _audio.SetMuted(muted);
            _ui.OnStartBattle = StartBattleAction;
            _ui.OnRestart = NewBattle;
            _ui.OnRetry = NewBattle;
            _ui.OnNextLevel = NextLevelAction;
            _ui.OnSpeedChanged = ApplyPlaybackSpeed;
            NewBattle();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_formationDrag != null && (_formationDrag.IsDragging || _formationDrag.IsSettling))
                    _formationDrag.CancelAndSnap();
                else Application.Quit();
            }
        }

        private Camera EnsureCamera()
        {
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
            return cam;
        }

        private Vector3 SlotTargetPosition(Team team, string slotId) =>
            PositioningView.Position(team, slotId, layout) + Vector3.down * (0.78f * 0.72f);

        private void SaveFormation()
        {
            if (_engine == null) return;
            foreach (var unit in _engine.Players) _savedSlots[unit.Id] = unit.SlotId;
        }

        private void RestoreFormation()
        {
            foreach (var unit in _engine.Players)
                if (_savedSlots.TryGetValue(unit.Id, out var slot)) _engine.TryMovePlayer(unit.Id, slot);
            SaveFormation();
        }

        private void StopDriver()
        {
            _generation++;
            if (_driver != null) StopCoroutine(_driver);
            _driver = null;
        }

        private void OnDestroy() => StopDriver();

        private void NewBattle()
        {
            _formationDrag?.Configure(_cam, null, layout, _views, _slotRoot, null);
            StopDriver();
            _audio?.StopPlayback();
            SaveFormation();
            _engine = null;
            _announcedWinner = false;
            _presentedSequence = 0;
            _ui.ResetBattleFeedback();
            var chars = ConfigLoader.LoadCharacters();
            var enemies = ConfigLoader.LoadEnemies();
            var levels = ConfigLoader.LoadLevels();
            var formation = ConfigLoader.LoadFormation();
            if (chars.errors.Count + enemies.errors.Count + levels.errors.Count + formation.errors.Count > 0)
            {
                DumpErrors("角色", chars.errors);
                DumpErrors("敌人", enemies.errors);
                DumpErrors("关卡", levels.errors);
                DumpErrors("阵型", formation.errors);
                _ui.ShowError("配置错误，查看 Console");
                return;
            }
            _levels.Clear();
            _levels.AddRange(levels.items.OrderBy(level => level.id));
            if (_levels.Count == 0) { _ui.ShowError("无可用关卡"); return; }
            _levelIndex = Mathf.Clamp(_levelIndex, 0, _levels.Count - 1);
            var level = _levels[_levelIndex];
            var catalog = enemies.items.ToDictionary(enemy => enemy.id);
            string error = ConfigValidator.ValidateLevelReferences(level, chars.items, catalog);
            if (error != null)
            {
                Debug.LogError(error);
                _ui.ShowError("关卡配置错误，查看 Console");
                return;
            }
            _ui.ClearError();
            _engine = BattleEngine.Create(seed + _levelIndex, chars.items, level, catalog, formation.items[0]);
            RestoreFormation();
            SpawnWorld();
            RefreshWorld();
            RefreshSetup();
            RefreshHud();
        }

        private void SpawnWorld()
        {
            if (_worldRoot != null) Destroy(_worldRoot.gameObject);
            _views.Clear();
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
                    var renderer = go.AddComponent<SpriteRenderer>();
                    renderer.sprite = UiKit.CircleSprite();
                    renderer.color = team == Team.Player ? new Color(0.3f, 0.8f, 1f, 0.65f) : new Color(1f, 0.4f, 0.3f, 0.5f);
                    renderer.sortingOrder = 1;
                }
            foreach (var unit in _engine.Ctx.Units)
            {
                var go = new GameObject("Unit_" + unit.Id);
                go.transform.SetParent(_worldRoot, false);
                go.transform.position = PositioningView.Position(unit, layout);
                go.transform.localScale = Vector3.one * 0.72f;
                var view = go.AddComponent<UnitView>();
                view.Setup(unit, UnitColor(unit));
                _views[unit.Id] = view;
            }
            _formationDrag.Configure(_cam, _engine, layout, _views, _slotRoot, () =>
            {
                SaveFormation();
                _audio.Play(BattleSound.Select);
            });
            ApplyPlaybackSpeed();
        }

        private void ApplyPlaybackSpeed()
        {
            foreach (var view in _views.Values) view.PlaybackSpeed = _ui.PlaybackSpeed;
        }

        private static Color UnitColor(Unit unit)
        {
            if (unit.IsDead) return Color.gray;
            Color color = unit.Team == Team.Player ? new Color(0.30f, 0.85f, 0.90f) : new Color(0.90f, 0.35f, 0.35f);
            return unit.Row == "back" ? color * 0.75f : color;
        }

        private void RefreshWorld()
        {
            if (_engine == null) return;
            var action = _engine.CurrentAction;
            foreach (var unit in _engine.Ctx.Units)
            {
                if (!_views.TryGetValue(unit.Id, out var view)) continue;
                string current = action != null && action.ActorId == unit.Id
                    ? ActionTypeLabel(action.ActionType) + " · " + action.ActionName : "";
                view.Refresh(unit, current, _engine.Ctx.GetStatuses(unit.Id));
                view.SetAppearance(UnitColor(unit), unit.IsDead);
            }
        }

        private void RefreshSetup() => _formationDrag?.RefreshAvailability();

        private void RefreshHud()
        {
            if (_ui == null) return;
            bool setup = _engine != null && _engine.Flow == BattleFlow.Setup;
            _ui.StartBattleButton.gameObject.SetActive(setup);
            _ui.StartBattleButton.interactable = setup;
        }

        private void StartBattleAction()
        {
            if (_engine == null || _engine.Flow != BattleFlow.Setup) return;
            _formationDrag?.CancelAndSnap();
            SaveFormation();
            if (!_engine.StartBattle()) return;
            _audio.Play(BattleSound.Select);
            RefreshWorld();
            RefreshSetup();
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
                float approach = BeginActionPresentation(result);
                if (approach > 0f)
                {
                    yield return WaitPlayback(approach);
                    if (!IsCurrent(engine, generation)) yield break;
                }
                RefreshWorld();
                ConsumeAction(result);
                RefreshSetup();
                RefreshHud();
                float duration = result.ActionType == "passive" ? 0.14f :
                    result.ActionType == "ult" ? Mathf.Max(actionDuration, 0.72f) : actionDuration;
                if (result.UnitChanges.Any(change => !change.Before.IsDead && change.After.IsDead))
                    duration = Mathf.Max(duration, 0.65f);
                yield return WaitPlayback(Mathf.Max(0f, duration - approach));
                if (!IsCurrent(engine, generation)) yield break;
                while (_views.Values.Any(view => view != null && !view.FeedbackComplete))
                {
                    yield return null;
                    if (!IsCurrent(engine, generation)) yield break;
                }
                yield return WaitPlayback(actionGap);
                if (!IsCurrent(engine, generation)) yield break;
                engine.Confirm(engine.BattleId, result.Sequence);
            }
            if (!IsCurrent(engine, generation)) yield break;
            RefreshWorld();
            RefreshSetup();
            RefreshHud();
            if (engine.IsFinished)
            {
                bool won = engine.Winner == Team.Player;
                _audio.Play(won ? BattleSound.Victory : BattleSound.Defeat);
                _ui.ShowFeedback(won ? "我方胜利" : "我方失败", won ? new Color(1f, 0.85f, 0.3f) : Color.red);
                yield return new WaitForSecondsRealtime(0.65f);
                if (!IsCurrent(engine, generation)) yield break;
                ShowBattleResult();
            }
            _driver = null;
        }

        private IEnumerator WaitPlayback(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime * _ui.PlaybackSpeed;
            }
        }

        private void ShowBattleResult()
        {
            if (_announcedWinner) return;
            _announcedWinner = true;
            bool won = _engine.Winner == Team.Player;
            var rows = BattleStatistics.Summarize(_engine);
            var allies = rows.Where(row => row.Team == Team.Player).ToList();
            string summary = _levels[_levelIndex].name + " · " + _engine.CurrentRound + " 回合\n" +
                "队伍输出 " + BattleReportView.FormatValue(allies.Sum(row => row.DamageDealt)) +
                "    有效治疗 " + BattleReportView.FormatValue(allies.Sum(row => row.HealingDone)) +
                "\n" + (won ? (HasNextLevel() ? "整装待发，准备迎接下一场战斗。" : "三关完成，感谢你的指挥！") :
                "调整站位，再试一次。每次尝试都能了解伙伴的表现。");
            _ui.ShowResult(won ? "我方胜利" : "我方失败", summary, rows,
                HasNextLevel() ? "下一关 → " + _levels[_levelIndex + 1].name : "", HasNextLevel());
        }

        private float BeginActionPresentation(ActionResult result)
        {
            foreach (var pair in _views)
                pair.Value.SetActionText(pair.Key == result.ActorId
                    ? ActionTypeLabel(result.ActionType) + " · " + result.ActionName : "");
            if (!_views.TryGetValue(result.ActorId, out var actor)) return 0f;
            string targetId = result.HitTargetIds.FirstOrDefault(id => _views.ContainsKey(id));
            Vector3? target = targetId != null ? _views[targetId].transform.position : (Vector3?)null;
            return actor.PlayAction(result.ActionType, target);
        }

        private void ConsumeAction(ActionResult result)
        {
            if (result.Sequence <= _presentedSequence) return;
            var sound = BattleAudio.SoundFor(result);
            if (sound.HasValue) _audio.Play(sound.Value);
            string action = UnitName(result.ActorId) + "〔" + ActionTypeLabel(result.ActionType) + " · " + result.ActionName + "〕";
            if (result.ActionType == "ult") _ui.ShowFeedback(action, new Color(1f, 0.85f, 0.3f));
            foreach (var target in result.Hits.Where(hit => hit.HpDamage > 0).Select(hit => hit.TargetUnitId).Distinct())
                if (_views.TryGetValue(target, out var hitView))
                    BattleEffect.Spawn(hitView, "slash", result.ActionType == "ult" ? new Color(1f, 0.8f, 0.25f) :
                        Color.white, result.ActionType == "ult" ? 1.25f : 1f);
            foreach (var target in result.Heals.Where(heal => heal.HpRestored > 0).Select(heal => heal.TargetUnitId).Distinct())
                if (_views.TryGetValue(target, out var healedView))
                    BattleEffect.Spawn(healedView, "heal", new Color(0.35f, 1f, 0.57f));
            foreach (var change in result.UnitChanges)
            {
                if (!_views.TryGetValue(change.UnitId, out var view)) continue;
                var gainedStatuses = change.After.Statuses.Where(status => !change.Before.Statuses.Any(before =>
                    before.Type == status.Type && before.Percent >= status.Percent &&
                    (before.Turn == "permanent" || (status.Turn != "permanent" && before.Duration >= status.Duration)))).ToList();
                if (!change.After.IsDead && gainedStatuses.Count > 0)
                    BattleEffect.Spawn(view, "ring", gainedStatuses.Any(status => status.Type == StatusType.Stun)
                        ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.28f, 0.38f), 0.75f);
                if (change.HpDelta == 0) continue;
                view.FloatText((change.HpDelta > 0 ? "+" : "") + change.HpDelta,
                    change.HpDelta > 0 ? Color.green : Color.red, change.HpDelta);
                view.Flash(change.HpDelta);
            }
            _presentedSequence = result.Sequence;
        }

        private static string ActionTypeLabel(string type)
        {
            switch (type)
            {
                case "basic": return "普攻";
                case "special": return "特殊";
                case "ult": return "终结技";
                case "passive": return "被动";
                case "skipped": return "跳过";
                default: return type;
            }
        }

        private void NextLevelAction()
        {
            if (!HasNextLevel() || !_ui.ResultVisible) return;
            _levelIndex++;
            NewBattle();
        }

        private bool HasNextLevel() => _engine != null && _engine.IsFinished && !_engine.IsActionOccupied &&
            _engine.Winner == Team.Player && _levelIndex + 1 < _levels.Count;

        private static void DumpErrors(string label, List<string> errors)
        {
            foreach (var error in errors) Debug.LogError("[" + label + "] " + error);
        }

        private string UnitName(string id) => _engine?.Ctx.GetUnit(id)?.Name ?? id ?? "无";
    }
}
