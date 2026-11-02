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
        public string[] players; // 本关可选伙伴；显式部署入口与默认全员战斗入口共用此目录
        public string tutorialIntro;
        public string[] tutorialHints;
        public List<WaveConfig> waves;
    }
}