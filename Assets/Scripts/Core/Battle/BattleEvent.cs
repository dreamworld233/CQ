namespace CQ.Core.Battle
{
    /// <summary>事件来源：玩家技能 / 卡牌 / 敌人意图。</summary>
    public enum BattleEventKind
    {
        Skill,
        Card,
        Intent
    }

    /// <summary>
    /// 已结算动作的事件记录（表现层逐条播报用，不参与战斗判定）。
    /// 一次动作可产生多条：AOE 命中 N 个目标即 N 条；无血量变化的动作产生 1 条 HpDelta = 0 的记录。
    /// ActionId 是机器 id（技能 id / 卡 id / 意图 type），中文文案由表现层决定，Core 不存文案。
    /// </summary>
    public sealed class BattleEvent
    {
        public BattleEventKind Kind;
        public string ActorId;
        public string ActionId;
        public string TargetId;
        public CQ.Core.Combat.Team? TargetTeam;
        public string TargetSlotId;

        /// <summary>目标 HP 变化量（负=伤害，正=治疗，0=无血量变化）。</summary>
        public int HpDelta;

        /// <summary>本次动作后目标是否死亡。</summary>
        public bool TargetDead;
    }
}
