using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class WaveConfig
    {
        public string[] enemies;
        public string[] slots; // 与 enemies 平行，重复怪物各自指定占位
    }

    [Serializable]
    public class LevelConfig
    {
        public int id;
        public string name;
        public List<WaveConfig> waves;
    }
}