using System.Collections.Generic;
using CQ.Core.Config;

namespace CQ.Core.Combat
{
    /// <summary>只读快照，无目标 id。数值均为查询时源状态修正后的盾前伤害，不预测未来状态/胜负。</summary>
    public sealed class EnemyIntentPreview
    {
        public string Type => Current?.Type;
        public string Text { get; }
        public bool Interrupted { get; }
        public bool CanAct { get; }
        public int SourceAttackUpPercent { get; }
        public int SourceWeakenPercent { get; }
        public EnemyIntentPreviewStep PendingRelease { get; }
        public EnemyIntentPreviewStep Current { get; }

        internal EnemyIntentPreview(bool interrupted, bool canAct, int attackUp, int weaken,
            EnemyIntentPreviewStep pending, EnemyIntentPreviewStep current)
        {
            Interrupted = interrupted;
            CanAct = canAct;
            SourceAttackUpPercent = attackUp;
            SourceWeakenPercent = weaken;
            PendingRelease = pending;
            Current = current;
            var parts = new List<string>();
            if (!canAct) parts.Add("无法行动");
            if (interrupted) parts.Add("已打断");
            if (pending != null) parts.Add("本轮蓄力释放：" + pending.Text);
            if (current != null) parts.Add("本轮意图：" + current.Text);
            if (pending != null && !pending.Suppressed && current != null && !current.Suppressed)
                parts.Add("释放后战斗未结束才执行本轮意图");
            if (attackUp != 0) parts.Add("源攻增 +" + attackUp + "%");
            if (weaken != 0) parts.Add("源虚弱 -" + weaken + "%");
            Text = string.Join("；", parts);
        }
    }

    /// <summary>Current 的附加效果在全部命中后施加一次；Suppressed 表示当前状态下取消/跳过。</summary>
    public sealed class EnemyIntentPreviewStep
    {
        public string Id { get; }
        public string Type { get; }
        public string Target { get; }
        public int DamagePerHit { get; }
        public int Hits { get; }
        public int NextRoundDamage { get; }
        public bool Interruptible { get; }
        public bool Suppressed { get; }
        public int BuffPercent { get; }
        public int BuffDuration { get; }
        public string DebuffType { get; }
        public int DebuffPercent { get; }
        public int DebuffDuration { get; }
        public string Text { get; }

        internal EnemyIntentPreviewStep(CombatContext ctx, string sourceId, IntentConfig intent, bool suppressed)
        {
            Id = intent.id;
            Type = intent.type;
            Target = intent.target;
            Suppressed = suppressed;
            Interruptible = intent.interruptible;
            BuffPercent = intent.buffPercent;
            BuffDuration = intent.buffDuration;
            DebuffType = intent.debuffType;
            DebuffPercent = intent.debuffPercent;
            DebuffDuration = intent.debuffDuration;
            bool damaging = Type == "attack" || Type == "multi" || Type == "chargeRelease";
            Hits = damaging ? (Type == "multi" ? intent.hits : 1) : 0;
            DamagePerHit = damaging ? Damage.CalculateSourceDamage(ctx, sourceId, intent.damage).AfterWeaken : 0;
            NextRoundDamage = Type == "charge"
                ? Damage.CalculateSourceDamage(ctx, sourceId, intent.nextDamage).AfterWeaken : 0;
            var parts = new List<string>();
            if (damaging)
                parts.Add((Target == "all" ? "全体 " : "单体 ") + DamagePerHit + (Type == "multi" ? "×" + Hits : "") + "（盾前）");
            if (Type == "charge")
                parts.Add("蓄力，下轮 " + NextRoundDamage + "（按当前源状态，盾前）" + (Interruptible ? "，可打断" : "，不可打断"));
            if (BuffPercent > 0) parts.Add((damaging ? "伤害后自身" : "自身") + "攻增 +" + BuffPercent + "%/" + BuffDuration + "回合");
            if (!string.IsNullOrEmpty(DebuffType))
                parts.Add((damaging ? "伤害后命中存活目标" : "单体") + (DebuffType == "weaken" ? "虚弱 " : "减速 ") + DebuffPercent + "%/" + DebuffDuration + "回合");
            Text = (suppressed ? "[取消/跳过] " : "") + string.Join("，", parts);
        }
    }
}
