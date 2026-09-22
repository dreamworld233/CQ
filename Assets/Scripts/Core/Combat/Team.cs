namespace CQ.Core.Combat
{
    /// <summary>
    /// 阵营。Player = 0 先于 Enemy = 1，供同速 tie-break 使用。
    /// </summary>
    public enum Team
    {
        Player = 0,
        Enemy = 1
    }
}