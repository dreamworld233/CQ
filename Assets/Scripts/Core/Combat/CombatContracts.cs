namespace CQ.Core.Combat
{
    public struct DamageRequest
    {
        public int Amount;         // 基础伤害
        public string Kind;        // physical | magic
        public string SourceUnitId;
        public string TargetUnitId;
    }

    public struct DamageResult
    {
        public int RawDamage;
        public int AfterAttackUp;
        public int AfterWeaken;
        public int ShieldAbsorbed;
        public int FinalDamage;
        public bool TargetDead;
    }

    public interface IDamageResolver
    {
        DamageResult ResolveDamage(in DamageRequest req, CombatContext ctx);
    }

    public interface IStatusResolver
    {
        void Apply(string unitId, StatusEffect fx, CombatContext ctx);
    }

    public interface ICardEffect
    {
        void Resolve(CombatContext ctx, string targetUnitId);
    }
}