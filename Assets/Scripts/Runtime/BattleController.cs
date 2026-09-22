using System.Collections.Generic;
using System.Linq;
using System.Text;
using CQ.Core.Battle;
using CQ.Core.Combat;
using CQ.Core.Config;
using UnityEngine;

namespace CQ.Runtime
{
    /// <summary>
    /// 最小可玩场景桥接（T7）：加载配置 → 建 BattleEngine → IMGUI 交互（选招/打牌/结束回合/重开）。
    /// 临时表现，正式 2.5D 表现在 T10。
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        [SerializeField] private long seed = 12345;
        [SerializeField] private int levelId = 1;

        private BattleEngine _engine;
        private string _selectedActor;
        private string _selectedTarget;
        private string _lastEvent = "";
        private string _configError;

        private void Start()
        {
            NewBattle();
        }

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
                foreach (var e in chars.errors) Debug.LogError("[角色] " + e);
                foreach (var e in enemies.errors) Debug.LogError("[敌人] " + e);
                foreach (var e in cards.errors) Debug.LogError("[卡] " + e);
                foreach (var e in levels.errors) Debug.LogError("[关卡] " + e);
                return;
            }

            var level = levels.items.FirstOrDefault(l => l.id == levelId) ?? levels.items.FirstOrDefault();
            if (level == null || level.waves == null || level.waves.Count == 0)
            {
                _configError = "无可用关卡";
                return;
            }

            var enemyCatalog = enemies.items.ToDictionary(e => e.id);
            _engine = BattleEngine.Create(seed, chars.items, level.waves[0].enemies, enemyCatalog, cards.items);
            _engine.StartBattle();

            _selectedActor = _engine.Players.FirstOrDefault(p => !p.IsDead)?.Id;
            _selectedTarget = _engine.Enemies.FirstOrDefault(e => !e.IsDead)?.Id;
            Log("开战：" + level.name);
        }

        private void Log(string msg)
        {
            _lastEvent = msg;
            Debug.Log("[CQ.Battle] " + msg);
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_configError))
            {
                GUILayout.Label(_configError);
                if (GUILayout.Button("重试加载")) NewBattle();
                return;
            }
            if (_engine == null) return;

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(320));
            DrawTop();
            DrawEnemies();
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.Width(320));
            DrawPlayers();
            DrawSkills();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            DrawHand();
            DrawControls();
        }

        private void DrawTop()
        {
            GUILayout.Label($"第 {_engine.Round} 回合  |  抽牌堆 {_engine.Deck.DrawPileCount} / 弃牌堆 {_engine.Deck.DiscardPileCount}");
            if (_engine.IsFinished)
                GUILayout.Label("结果：" + (_engine.Winner == Team.Player ? "我方胜利" : "我方失败"));
            if (_lastEvent.Length > 0) GUILayout.Label("最近：" + _lastEvent);
            GUILayout.Space(8);
        }

        private void DrawEnemies()
        {
            GUILayout.Label("———— 敌方（点击设为目标）————");
            foreach (var e in _engine.Enemies)
            {
                bool sel = e.Id == _selectedTarget;
                string intent = "";
                var cfg = _engine.CurrentIntentOf(e.Id);
                if (cfg != null) intent = " 意图:" + cfg.id + (cfg.type == "charge" ? "⚠蓄力" : "");
                string line = (sel ? "▶ " : "") + e.Name + " " + e.Hp + "/" + e.MaxHp + intent;
                if (GUILayout.Button(line)) _selectedTarget = e.Id;
            }
            GUILayout.Space(8);
        }

        private void DrawPlayers()
        {
            GUILayout.Label("———— 我方（点击设为操作角色）————");
            foreach (var p in _engine.Players)
            {
                bool sel = p.Id == _selectedActor;
                string line = (sel ? "▶ " : "") + p.Name + " " + p.Hp + "/" + p.MaxHp
                    + " 能量" + p.Energy + "/" + p.MaxEnergy + " 大招" + p.Ult + "/" + p.MaxUlt
                    + " " + p.Row;
                if (GUILayout.Button(line))
                {
                    _selectedActor = p.Id;
                    _selectedTarget = p.Id;
                }
            }
            GUILayout.Space(8);
        }

        private void DrawSkills()
        {
            if (string.IsNullOrEmpty(_selectedActor)) return;
            GUILayout.Label("———— 技能（角色：" + UnitName(_selectedActor) + "，目标：" + UnitName(_selectedTarget) + "）————");
            var skills = _engine.SkillsOf(_selectedActor);
            if (skills == null) return;
            GUILayout.BeginHorizontal();
            foreach (var s in skills)
            {
                bool ok = _engine.CanUseSkill(_selectedActor, s.id);
                GUI.enabled = ok;
                if (GUILayout.Button(s.name))
                {
                    if (_engine.ChooseSkill(_selectedActor, s.id, _selectedTarget))
                        Log(UnitName(_selectedActor) + " 选择 " + s.name);
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawHand()
        {
            GUILayout.Label("———— 手牌（点一张打出，作用于当前目标）————");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                var c = _engine.GetCard(_engine.Hand[i]);
                string label = c != null ? c.name : _engine.Hand[i];
                if (GUILayout.Button(label))
                {
                    if (_engine.PlayCard(i, _selectedTarget))
                        Log("打出 " + label + " → " + UnitName(_selectedTarget));
                }
            }
            if (_engine.Hand.Count == 0) GUILayout.Label("(空)");
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawControls()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("结束回合"))
            {
                if (!_engine.IsFinished)
                {
                    var order = _engine.EndRound();
                    var names = order.Select(u => u.Name + (u.IsDead ? "(亡)" : "")).ToArray();
                    Log("出手顺序：" + string.Join(" → ", names));
                }
            }
            if (GUILayout.Button("重开")) NewBattle();
            GUILayout.EndHorizontal();

            if (_engine.TurnOrder != null && _engine.TurnOrder.Count > 0)
            {
                GUILayout.Label("上回合顺序：" + string.Join(" → ", _engine.TurnOrder.Select(u => u.Name)));
            }
        }

        private string UnitName(string id)
        {
            var u = _engine.Ctx.GetUnit(id);
            return u != null ? u.Name : (id ?? "无");
        }
    }
}