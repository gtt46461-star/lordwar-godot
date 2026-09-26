using System;
namespace LordWar {
    public sealed class DeterministicRandom {
        private uint _state;
        public DeterministicRandom(int seed) { _state = (uint)(seed == 0 ? 0x6d2b79f5 : seed); }
        public uint State { get { return _state; } set { _state = value == 0 ? 0x6d2b79f5u : value; } }
        public uint NextUInt() { uint x = _state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; _state = x; return x; }
        public int Range(int minInclusive, int maxExclusive) { if (maxExclusive <= minInclusive) return minInclusive; return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive)); }
        public float Next01() { return (NextUInt() & 0x00FFFFFF) / 16777216f; }
        public bool Chance(float probability) { return Next01() < probability; }
    }
    public static class Mathx {
        public static int Clamp(int v,int a,int b){return v<a?a:(v>b?b:v);} public static float Clamp(float v,float a,float b){return v<a?a:(v>b?b:v);}
        public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp(t,0f,1f);} public static int Manhattan(int ax,int ay,int bx,int by){return Math.Abs(ax-bx)+Math.Abs(ay-by);}
    }
    public static class Ids {
        private static long _seq = 1000;
        public static long Current { get { return _seq; } }
        public static string Next(string prefix) { _seq++; return prefix + "_" + _seq.ToString("D8"); }
        public static void RestoreToAtLeast(long value) { if(value>_seq)_seq=value; }
    }
}
