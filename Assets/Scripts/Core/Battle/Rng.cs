using System;

namespace CQ.Core.Battle
{
    /// <summary>
    /// 确定性伪随机（xorshift64*），可注入 seed。同 seed 必得同序列。
    /// 禁止读 Time / frameCount，保证 headless 可重放。
    /// </summary>
    public sealed class Rng
    {
        private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;
        private ulong _state;

        public Rng(long seed)
        {
            _state = seed == 0 ? DefaultSeed : (ulong)seed;
        }

        public ulong NextULong()
        {
            ulong x = _state;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            _state = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        public uint NextUInt() => (uint)(NextULong() >> 32);

        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + NextInt(maxExclusive - minInclusive);
        }

        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
    }
}