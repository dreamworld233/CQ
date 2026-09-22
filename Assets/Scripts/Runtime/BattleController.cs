using System.Collections.Generic;
using System.Linq;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 场景驱动桥接（T10）：加载配置 → 建 BattleEngine → 生成世界单位（前后排站位）→
    /// IMGUI HUD（左侧常驻行动条 / 手牌 / 技能 / 意图 / 结束回合）。
    /// BattleEngine 是唯一战斗状态源；表现层只读、只发指令。
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private long seed = 12345;

        private readonly List<LevelConfig> _levels = new List<LevelConfig>();
        private int _levelIndex = 0;

        private BattleEngine _engine;
        private readonly Dictionary<string, UnitView> _views = new Dictionary<string, UnitView>();
        private string _selectedActor;
        private string _selectedTarget;
        private string _lastEvent = "";
        private string _configError;
        private Transform _worldRoot;

        private void Start()
        {
            EnsureCamera();
            NewBattle();
        }

        private void EnsureCamera()
        {
            if (Camera.main != null) return;
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.tag = "MainCamera";
        }

        // ---------- 建局 ----------

        private void NewBattle()
        {
            _configError = null;
            _lastEvent = "";
            var chars = ConfigLoader.LoadCharacters();
            var enemies = ConfigLoader.LoadEnemies();
            var cards = ConfigLoader.LoadCards();
            var levels = ConfigLoader.LoadLevels();

            int errors = chars.errors.Count + enemies.errors.Count + cards.errors.Count + levels.errors.Count;
            if (errors > 0)
            {
                _configError = "配置错误，查看 Console";
                DumpErrors("角色", chars.errors);
                DumpErrors("敌人", enemies.errors);
                DumpErrors("卡", cards.errors);
                DumpErrors("关卡", levels.errors);
                return;
            }

            _levels.Clear();
            _levels.AddRange(levels.items.OrderBy(l => l.id));
            if (_levels.Count == 0)
            {
                _configError = "无可用关卡";
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
            Log("开战 " + (_levelIndex + 1) + "/" + _levels.Count + "：" + level.name);
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
            go.transform.position = PositioningView.Position(unit, slot);
            go.transform.localScale = Vector3.one * 1.3f;

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
                string intent = u.Team == Team.Enemy ? IntentLabel(_engine.CurrentIntentOf(u.Id)) : "";
                v.Refresh(u, intent);
                v.SetColor(UnitColor(u, u.IsDead));
            }
        }

        private static string IntentLabel(IntentConfig it)
        {
            if (it == null) return "";
            switch (it.type)
            {
                case "attack": return it.target == "all" ? "全体" : "攻击";
                case "multi": return "连击x" + it.hits;
                case "charge": return "蓄力!";
                case "attackUp": return "强化";
                default: return it.type;
            }
        }

        // ---------- 玩家指令 ----------

        private void PlayCard(int handIndex)
        {
            if (_engine == null) return;
            var id = handIndex >= 0 && handIndex < _engine.Hand.Count ? _engine.Hand[handIndex] : null;
            string name = _engine.GetCard(id)?.name ?? id;
            if (_engine.PlayCard(handIndex, _selectedTarget))
            {
                Log("打出 " + name + " → " + UnitName(_selectedTarget));
                RefreshWorld();
            }
        }

        private void ChooseSkill(string skillId)
        {
            if (_engine == null || string.IsNullOrEmpty(_selectedActor)) return;
            if (_engine.ChooseSkill(_selectedActor, skillId, _selectedTarget))
                Log(UnitName(_selectedActor) + " 选择 " + SkillName(_selectedActor, skillId));
        }

        private void EndRound()
        {
            if (_engine == null || _engine.IsFinished) return;
            var order = _engine.EndRound();
            Log("出手：" + string.Join(" → ", order.Select(u => u.Name + (u.IsDead ? "(亡)" : ""))));
            RefreshWorld();
        }

        private void NextLevel()
        {
            if (_levelIndex + 1 >= _levels.Count) return;
            _levelIndex++;
            NewBattle();
        }

        private void Log(string msg)
        {
            _lastEvent = msg;
            Debug.Log("[CQ.Battle] " + msg);
        }

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

        // ---------- HUD ----------

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_configError))
            {
                GUILayout.Label(_configError);
                if (GUILayout.Button("重试加载")) NewBattle();
                return;
            }
            if (_engine == null) return;

            GUILayout.BeginArea(new Rect(8f, 8f, 180f, Screen.height - 16f), GUI.skin.box);
            DrawActionBar();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(200f, 8f, Screen.width - 210f, Screen.height - 16f));
            DrawMain();
            GUILayout.EndArea();
        }

        private void DrawActionBar()
        {
            GUILayout.Label("== 行动条 ==");
            GUILayout.Label("第 " + _engine.Round + " 回合");
            if (_engine.IsFinished)
                GUILayout.Label(_engine.Winner == Team.Player ? "我方胜利" : "我方失败");

            var order = _engine.TurnOrder;
            if (order == null || order.Count == 0)
            {
                GUILayout.Label("(尚未出手)");
            }
            else
            {
                foreach (var u in order)
                    GUILayout.Label((u.Team == Team.Player ? "我 " : "敌 ") + u.Name + (u.IsDead ? "(亡)" : ""));
            }

            GUILayout.Space(8);
            GUILayout.Label("抽牌堆 " + _engine.Deck.DrawPileCount);
            GUILayout.Label("弃牌堆 " + _engine.Deck.DiscardPileCount);
        }

        private void DrawMain()
        {
            if (_lastEvent.Length > 0) GUILayout.Label("最近：" + _lastEvent);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            DrawEnemies();
            DrawPlayers();
            DrawSkills();
            GUILayout.EndHorizontal();

            DrawHand();
            DrawControls();
        }

        private void DrawEnemies()
        {
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label("— 敌方（点设为目标）—");
            foreach (var e in _engine.Enemies)
            {
                bool sel = e.Id == _selectedTarget;
                string line = (sel ? "▶ " : "") + e.Name + " " + e.Hp + "/" + e.MaxHp + " | " + IntentLabel(_engine.CurrentIntentOf(e.Id));
                if (GUILayout.Button(line)) _selectedTarget = e.Id;
            }
            GUILayout.EndVertical();
        }

        private void DrawPlayers()
        {
            GUILayout.BeginVertical(GUILayout.Width(220));
            GUILayout.Label("— 我方（点设操作角色）—");
            foreach (var p in _engine.Players)
            {
                bool sel = p.Id == _selectedActor;
                string line = (sel ? "▶ " : "") + p.Name + " " + p.Hp + "/" + p.MaxHp
                    + " 能" + p.Energy + "/" + p.MaxEnergy + " 大" + p.Ult + "/" + p.MaxUlt + " " + p.Row;
                if (GUILayout.Button(line))
                {
                    _selectedActor = p.Id;
                    _selectedTarget = p.Id;
                }
            }
            GUILayout.EndVertical();
        }

        private void DrawSkills()
        {
            GUILayout.BeginVertical();
            GUILayout.Label("— 技能（目标：" + UnitName(_selectedTarget) + "）—");
            if (string.IsNullOrEmpty(_selectedActor)) { GUILayout.EndVertical(); return; }
            var skills = _engine.SkillsOf(_selectedActor);
            if (skills != null)
            {
                GUILayout.BeginHorizontal();
                foreach (var s in skills)
                {
                    GUI.enabled = _engine.CanUseSkill(_selectedActor, s.id);
                    if (GUILayout.Button(s.name)) ChooseSkill(s.id);
                    GUI.enabled = true;
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private void DrawHand()
        {
            GUILayout.Label("— 手牌（点一张打出，作用于当前目标）—");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                var c = _engine.GetCard(_engine.Hand[i]);
                if (GUILayout.Button(c != null ? c.name : _engine.Hand[i])) PlayCard(i);
            }
            if (_engine.Hand.Count == 0) GUILayout.Label("(空)");
            GUILayout.EndHorizontal();
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("结束回合")) EndRound();
            if (GUILayout.Button("重开")) NewBattle();
            GUILayout.EndHorizontal();

            if (_engine.IsFinished && _engine.Winner == Team.Player)
            {
                if (_levelIndex + 1 < _levels.Count)
                {
                    if (GUILayout.Button("下一关 → " + _levels[_levelIndex + 1].name)) NextLevel();
                }
                else
                {
                    GUILayout.Label("★ 已打穿全部关卡 ★");
                }
            }
        }
    }
}