using System;

namespace Shepherd.Battle.Core
{
    public interface IRandom
    {
        /// <summary>回傳 [0, maxExclusive) 的整數。</summary>
        int Next(int maxExclusive);
    }

    /// <summary>
    /// 確定性亂數（xorshift32）。不使用 System.Random 或 UnityEngine.Random，
    /// 確保同一個種子在任何平台與執行環境都產生相同序列。
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        private uint state;

        public SeededRandom(int seed)
        {
            state = (uint)seed ^ 0x9E3779B9u;
            if (state == 0) state = 0x6D2B79F5u;
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException("maxExclusive");
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (int)(state % (uint)maxExclusive);
        }
    }
}
