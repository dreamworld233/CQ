using System;

namespace CQ.Core
{
    /// <summary>
    /// 去尾取整统一入口。Core 层禁止 UnityEngine.Mathf，全部走这里。
    /// </summary>
    public static class Mathd
    {
        public static int FloorToInt(double value) => (int)Math.Floor(value);

        public static int FloorToInt(float value) => (int)Math.Floor(value);
    }
}