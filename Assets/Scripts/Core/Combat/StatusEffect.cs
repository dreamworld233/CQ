namespace CQ.Core.Combat
{
    public sealed class StatusEffect
    {
        public StatusType Type;
        public int Duration;     // 剩余回合
        public string Turn;      // "round" | "once"（shield=once）
        public int Percent;      // weaken / slow / attackUp 百分比（如 25 表示 25%）
        public int Amount;       // shield 值
        public string SourceUnitId;
    }
}