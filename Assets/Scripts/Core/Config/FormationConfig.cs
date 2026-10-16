using System;

namespace CQ.Core.Config
{
    /// <summary>阵型基础嘲讽规则；数值由资源或调用方注入，不在 Core 固定平衡值。</summary>
    [Serializable]
    public sealed class FormationConfig
    {
        public int frontBaseTaunt;
        public int backBaseTaunt;
    }
}
