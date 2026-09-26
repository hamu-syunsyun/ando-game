using System;

namespace AndoBoss
{
    // 音を計算で作るための小物。音声ファイルは1つも使っていない。
    // 別スレッドからも呼ぶので UnityEngine の API は使わない
    public static class Synth
    {
        public const int SR = 44100;
        const double TAU = Math.PI * 2;

        public static float Mtof(float midi) => 440f * (float)Math.Pow(2, (midi - 69) / 12.0);

        public static float Saw(double ph) { ph -= Math.Floor(ph); return (float)(ph * 2 - 1); }
        public static float Sq(double ph, double duty = 0.5) { ph -= Math.Floor(ph); return ph < duty ? 1f : -1f; }
        public static float Tri(double ph) { ph -= Math.Floor(ph); return (float)(ph < 0.5 ? ph * 4 - 1 : 3 - ph * 4); }
        public static float Sin(double ph) => (float)Math.Sin(ph * TAU);
        public static float SoftClip(float x) => (float)Math.Tanh(x);

        // 状態変数フィルター（ローパス・バンドパス・ハイパスを同時に出す）
        public class SVF
        {
            float lp, bp;
            public float Low, Band, High;
            public void Run(float x, float cutoff, float q)
            {
                float f = (float)(2 * Math.Sin(Math.PI * Math.Min(cutoff, SR * 0.2f) / SR));
                float damp = 1f / Math.Max(0.5f, q);
                // 2回まわして安定させる
                for (int k = 0; k < 2; k++)
                {
                    float hp = x - lp - damp * bp;
                    bp += f * 0.5f * hp;
                    lp += f * 0.5f * bp;
                    High = hp;
                }
                Low = lp; Band = bp;
            }
        }

        public class Rng
        {
            uint s;
            public Rng(uint seed) { s = seed == 0 ? 1u : seed; }
            public float Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return (s & 0xFFFFFF) / (float)0x1000000; }
            public float Noise() => Next() * 2 - 1;
            public float Range(float a, float b) => a + (b - a) * Next();
        }

        // 立ち上がり a 秒・減衰 d 秒のエンベロープ（指数減衰）
        public static float Env(float t, float a, float d)
        {
            if (t < 0) return 0;
            if (t < a) return t / a;
            return (float)Math.Exp(-(t - a) / Math.Max(1e-4f, d));
        }

        // ADSR（len は鍵盤を押している長さ）
        public static float Adsr(float t, float len, float a, float d, float s, float r)
        {
            if (t < 0) return 0;
            float v;
            if (t < a) v = t / a;
            else if (t < a + d) v = 1 - (1 - s) * (t - a) / d;
            else v = s;
            if (t > len) v *= Math.Max(0f, 1 - (t - len) / r);
            return v;
        }

        public static float[] Buffer(float sec) => new float[(int)(sec * SR)];

        public static void Normalize(float[] b, float peak = 0.9f)
        {
            float m = 1e-6f;
            foreach (var x in b) m = Math.Max(m, Math.Abs(x));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        public static void Fade(float[] b, float inSec = 0.002f, float outSec = 0.01f)
        {
            int ni = (int)(inSec * SR), no = (int)(outSec * SR);
            for (int i = 0; i < ni && i < b.Length; i++) b[i] *= i / (float)ni;
            for (int i = 0; i < no && i < b.Length; i++) b[b.Length - 1 - i] *= i / (float)no;
        }

        // 単純なフィードバックディレイ（残響の代わり）
        public static void Echo(float[] b, float sec, float fb, float mix)
        {
            int d = (int)(sec * SR);
            var wet = new float[b.Length];
            for (int i = d; i < b.Length; i++) wet[i] = (b[i - d] + wet[i - d]) * fb;
            for (int i = 0; i < b.Length; i++) b[i] += wet[i] * mix;
        }

        public static void Add(float[] dst, float[] src, int offset, float gain)
        {
            for (int i = 0; i < src.Length && offset + i < dst.Length; i++)
                if (offset + i >= 0) dst[offset + i] += src[i] * gain;
        }
    }
}
