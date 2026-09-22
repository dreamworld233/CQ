using System;

namespace CQ.Core.Config
{
    [Serializable]
    public class StatusConfig
    {
        public string id;      // weaken | shield | attackUp | slow | interrupt
        public int duration;
        public string turn;    // round | once
    }
}