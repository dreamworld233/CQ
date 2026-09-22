using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class WaveConfig
    {
        public string[] enemies;
    }

    [Serializable]
    public class LevelConfig
    {
        public int id;
        public string name;
        public List<WaveConfig> waves;
    }
}