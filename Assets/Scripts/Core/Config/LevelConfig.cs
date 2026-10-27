using System;
using System.Collections.Generic;

namespace CQ.Core.Config
{
    [Serializable]
    public class WaveConfig
    {
        public string[] enemies;
        public string[] slots; // 与 enemies 平行，重复怪物各自指定占位
        public int[] maxHp;
    }

    [Serializable]
    public class LevelConfig
    {
        public int id;
        public string name;
        public string[] players;
        public string tutorialIntro;
        public string[] tutorialHints;
        public string[] tutorialDrawOrder;
        public List<WaveConfig> waves;
    }
}