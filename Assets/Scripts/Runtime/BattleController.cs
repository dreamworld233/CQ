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
        private PartyRoster _party;
        private string _newCompanion;
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
            _ui.Roster.OnToggle = ToggleRoster;
            _formationDrag.OnInspect = InspectUnit;
            _formationDrag.OnDragStarted = () => _ui.Skills.Hide();
            NewBattle();
        }

        private void Update()
        {
            UpdateCameraViewport();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_ui != null && _ui.Skills.Visible) _ui.Skills.Hide();
                else if (_formationDrag != null && (_formationDrag.IsDragging || _formationDrag.IsSettling))
                    _formationDrag.CancelAndSnap();
                else Application.Quit();
            }
        }

        private void LateUpdate() => UpdateSkillBubble();

        private void UpdateSkillBubble()
        {
            if (_ui == null || !_ui.Skills.Visible) return;
            if (_engine == null || _engine.Flow != BattleFlow.Setup)
            {
                _ui.Skills.Hide();
                return;
            }
            var unit = _engine.Ctx.GetUnit(_ui.Skills.UnitId);
            if (unit == null || unit.IsDead || !_views.TryGetValue(unit.Id, out var view) || view == null)
            {
                _ui.Skills.Hide();
                return;
            }
            _ui.Skills.Follow(_cam, view.InspectionBounds,
                SkillDetailsView.PreferRight(unit.Team == Team.Enemy, unit.Row == "front"));
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

        private void InspectUnit(string id)
        {
            if (_engine == null || _engine.Flow != BattleFlow.Setup) { _ui.Skills.Hide(); return; }
            var unit = _engine.Ctx.GetUnit(id);
            if (unit == null) { _ui.Skills.Hide(); return; }
            if (_ui.ResultVisible || _ui.ErrorPanel.activeSelf) return;
            _ui.Skills.Show(unit.Id, unit.Name, unit.Team == Team.Enemy, _engine.SkillsOf(id));
            UpdateSkillBubble();
        }

        private void SaveFormation()
        {
            if (_engine != null && _party != null) _party.CapturePositions(_engine.Players);
        }

        private void ToggleRoster(string id)
        {
            if (_engine == null || _engine.Flow != BattleFlow.Setup || _party == null) return;
            _formationDrag.CancelAndSnap();
            SaveFormation();
            if (!_party.TryToggle(id, out var error))
            {
                _ui.ShowFeedback(error, new Color(1f, 0.78f, 0.42f));
                return;
            }
            if (id == _newCompanion) _newCompanion = null;
            NewBattle();
            _audio.Play(BattleSound.Select);
        }

        private void UpdateCameraViewport()
        {
            if (_cam == null || _ui == null || _ui.Roster == null) return;
            float left = 0f;
            if (_ui.Roster.gameObject.activeInHierarchy)
            {
                var panel = _ui.Roster.PanelRect;
                float right = RectTransformUtility.WorldToScreenPoint(null,
                    panel.TransformPoint(new Vector3(panel.rect.xMax, 0f, 0f))).x;
                left = Mathf.Clamp(right / Mathf.Max(1, Screen.width), 0f, 0.4f);
            }
            _cam.rect = new Rect(left, 0f, 1f - left, 1f);
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
            _ui.Roster.gameObject.SetActive(false);
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
            string error = ConfigValidator.ValidateLevelReferences(level, chars.items, catalog, true);
            if (error != null)
            {
                Debug.LogError(error);
                _ui.ShowError("关卡配置错误，查看 Console");
                return;
            }
            _ui.ClearError();
            var known = _party == null ? null : new HashSet<string>(_party.Available.Select(character => character.id));
            _party = new PartyRoster(chars.items, level.players, _party);
            if (known != null)
                _newCompanion = _party.Available.FirstOrDefault(character => !known.Contains(character.id))?.id ?? _newCompanion;
            _engine = BattleEngine.Create(seed + _levelIndex, chars.items, level, catalog, formation.items[0], _party.Deployment);
            layout.setupSpread = 1.15f;
            SpawnWorld();
            RefreshWorld();
            RefreshSetup();
            RefreshHud();
        }

        private void SpawnWorld()
        {
            if (_worldRoot != null)
            {
                _worldRoot.gameObject.SetActive(false);
                Destroy(_worldRoot.gameObject);
            }
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

        private void RefreshSetup()
        {
            _formationDrag?.RefreshAvailability();
            if (_engine != null && _engine.Flow == BattleFlow.Setup && _party != null) _ui.Roster.Show(_party, _newCompanion);
            else _ui.Roster.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            UpdateCameraViewport();
        }

        private void RefreshHud()
        {
            if (_ui == null) return;
            bool setup = _engine != null && _engine.Flow == BattleFlow.Setup;
            _ui.StartBattleButton.gameObject.SetActive(setup);
            _ui.StartBattleButton.interactable = setup && _party != null && _party.CanStart;
        }

        private void StartBattleAction()
        {
            if (_engine == null || _engine.Flow != BattleFlow.Setup) return;
            _ui.Skills.Hide();
            _formationDrag?.CancelAndSnap();
            SaveFormation();
            if (!_engine.StartBattle()) return;
            layout.setupSpread = 0f;
            foreach (var unit in _engine.Ctx.Units)
                if (_views.TryGetValue(unit.Id, out var view)) view.SetPosition(PositioningView.Position(unit, layout), unit);
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
            foreach (var hits in result.Hits.Where(hit => hit.HpDamage > 0).GroupBy(hit => hit.TargetUnitId))
                if (_views.TryGetValue(hits.Key, out var hitView))
                    hitView.PlayDamageHits(hits.Select(hit => hit.HpDamage), result.ActionType == "ult");
            foreach (var heals in result.Heals.Where(heal => heal.HpRestored > 0).GroupBy(heal => heal.TargetUnitId))
                if (_views.TryGetValue(heals.Key, out var healedView))
                {
                    int amount = heals.Sum(heal => heal.HpRestored);
                    healedView.FloatText("+" + amount, Color.green, amount);
                    healedView.Flash(amount);
                    BattleEffect.Spawn(healedView, "heal", new Color(0.35f, 1f, 0.57f));
                }
            foreach (var change in result.UnitChanges)
            {
                if (!_views.TryGetValue(change.UnitId, out var view)) continue;
                var gainedStatuses = change.After.Statuses.Where(status => !change.Before.Statuses.Any(before =>
                    before.Type == status.Type && before.Percent >= status.Percent &&
                    (before.Turn == "permanent" || (status.Turn != "permanent" && before.Duration >= status.Duration)))).ToList();
                if (!change.After.IsDead && gainedStatuses.Count > 0)
                    BattleEffect.Spawn(view, "ring", gainedStatuses.Any(status => status.Type == StatusType.Stun)
                        ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.28f, 0.38f), 0.75f);
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
