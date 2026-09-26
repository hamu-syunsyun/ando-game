using System;
using System.Threading;
using UnityEngine;
using static AndoBoss.Synth;

namespace AndoBoss
{
    // ボス戦の曲（160BPM・ニ短調・16小節ループ）を計算で作る。
    // パートごとに別の AudioClip（ステム）にして同時に鳴らし、場面に合わせて音量を変える。
    //   タイトル：ハーモニーだけ → 戦闘：全部 → 第2形態：「激化」パートが加わる
    public class Music : MonoBehaviour
    {
        public static Music I;

        public enum Stem { Drums, Bass, Harmony, Lead, Intense }
        const int STEMS = 5;
        const float BPM = 160f;
        const int BARS = 16;
        static readonly double StepSec = 60.0 / BPM / 4;
        static readonly int Total = (int)Math.Round(StepSec * 16 * BARS * SR);

        float[][] dataL = new float[STEMS][], dataR = new float[STEMS][];
        volatile bool ready;
        Thread worker;
        readonly AudioSource[] src = new AudioSource[STEMS];
        readonly float[] target = new float[STEMS];
        readonly float[] cur = new float[STEMS];
        public bool Ready => src[0] != null && src[0].clip != null;
        public static float Master = 0.45f;
        public static bool Muted;
        float duck = 1f, duckTarget = 1f;
        AudioLowPassFilter[] lowpass = new AudioLowPassFilter[STEMS];
        float lpTarget = 22000;

        void Awake()
        {
            I = this;
            worker = new Thread(Generate) { IsBackground = true };
            worker.Start();
        }

        void Update()
        {
            if (ready && src[0] == null) CreateSources();
            if (src[0] == null) return;
            float dt = Time.unscaledDeltaTime;
            duck = Mathf.MoveTowards(duck, duckTarget, dt * 2f);
            for (int i = 0; i < STEMS; i++)
            {
                cur[i] = Mathf.MoveTowards(cur[i], target[i], dt * 1.2f);
                src[i].volume = cur[i] * Master * duck * (Muted ? 0 : 1);
                lowpass[i].cutoffFrequency = Mathf.Lerp(lowpass[i].cutoffFrequency, lpTarget, dt * 4f);
            }
        }

        void CreateSources()
        {
            string[] names = { "drums", "bass", "harmony", "lead", "intense" };
            double start = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < STEMS; i++)
            {
                var go = new GameObject("music_" + names[i]);
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<AudioSource>();
                bool st = dataR[i] != null;
                var clip = AudioClip.Create("bgm_" + names[i], Total, st ? 2 : 1, SR, false);
                if (st)
                {
                    var inter = new float[Total * 2];
                    for (int k = 0; k < Total; k++) { inter[k * 2] = dataL[i][k]; inter[k * 2 + 1] = dataR[i][k]; }
                    clip.SetData(inter, 0);
                }
                else clip.SetData(dataL[i], 0);
                s.clip = clip;
                s.loop = true;
                s.playOnAwake = false;
                s.volume = 0;
                s.spatialBlend = 0;
                s.PlayScheduled(start);
                lowpass[i] = go.AddComponent<AudioLowPassFilter>();
                lowpass[i].cutoffFrequency = 22000;
                src[i] = s;
            }
            dataL = null; dataR = null;
        }

        // ---- 場面ごとのミックス ----
        public void Mix(float drums, float bass, float harmony, float lead, float intense)
        {
            target[0] = drums; target[1] = bass; target[2] = harmony; target[3] = lead; target[4] = intense;
        }
        public void SetTitle() { Mix(0, 0.5f, 0.9f, 0, 0); lpTarget = 22000; duckTarget = 1; }
        public void SetBattle(bool phase2) { Mix(0.85f, 1, 0.85f, 0.95f, phase2 ? 1 : 0); lpTarget = 22000; duckTarget = 1; }
        public void SetMuffled(bool on) { lpTarget = on ? 700 : 22000; }
        public void Duck(bool on) { duckTarget = on ? 0.25f : 1f; }
        public void StopAll() { Mix(0, 0, 0, 0, 0); }
        public void CutNow()
        {
            for (int i = 0; i < STEMS; i++) { cur[i] = 0; target[i] = 0; }
        }
        // 曲の頭から鳴らし直す（戦闘開始の瞬間に1小節目が来るように）
        public void Restart()
        {
            if (src[0] == null) return;
            double start = AudioSettings.dspTime + 0.08;
            foreach (var s in src) { s.Stop(); s.time = 0; s.PlayScheduled(start); }
        }

        // ================= ここから作曲 =================
        struct Chord { public int root; public int[] tones; }
        static Chord C(int root, params int[] t) => new Chord { root = root, tones = t };

        // A: Dm Bb C A / Dm Bb C A   B: Gm Dm Bb A / Gm Dm Bb A
        static readonly Chord[] Prog =
        {
            C(38, 62, 65, 69), C(34, 62, 65, 70), C(36, 64, 67, 72), C(33, 61, 64, 69),
            C(38, 62, 65, 69), C(34, 62, 65, 70), C(36, 64, 67, 72), C(33, 61, 64, 69),
            C(31, 62, 67, 70), C(38, 62, 65, 69), C(34, 62, 65, 70), C(33, 61, 64, 69),
            C(31, 62, 67, 70), C(38, 62, 65, 69), C(34, 65, 70, 74), C(33, 64, 69, 73),
        };

        // メロディ（音, 16分音符いくつ分）。0 は休符
        static readonly int[][] Melody =
        {
            new[] { 74,3, 77,3, 81,2, 79,2, 77,2, 76,2, 77,2 },
            new[] { 74,3, 77,3, 82,2, 81,4, 77,4 },
            new[] { 76,3, 79,3, 84,2, 82,2, 81,2, 79,2, 76,2 },
            new[] { 81,6, 79,2, 76,4, 73,4 },
            new[] { 74,3, 77,3, 81,2, 86,4, 84,2, 81,2 },
            new[] { 82,3, 81,3, 77,2, 74,4, 77,4 },
            new[] { 79,3, 81,3, 82,2, 84,4, 88,4 },
            new[] { 88,4, 85,4, 81,8 },
            new[] { 82,6, 81,2, 79,4, 86,4 },
            new[] { 81,6, 77,2, 74,4, 81,4 },
            new[] { 82,4, 84,4, 86,4, 89,4 },
            new[] { 88,8, 85,4, 88,4 },
            new[] { 91,6, 89,2, 86,4, 82,4 },
            new[] { 81,6, 86,2, 89,4, 88,4 },
            new[] { 86,6, 84,2, 82,4, 84,4 },
            new[] { 85,4, 88,4, 93,8 },
        };

        static int At(double sec) => (int)Math.Round(sec * SR);
        static double StepT(int bar, float step) => (bar * 16 + step) * StepSec;

        // ループの終わりをはみ出した音は頭に回り込ませる
        static void Put(float[] b, int i, float v) { b[((i % Total) + Total) % Total] += v; }

        void Generate()
        {
            try
            {
                var st = Render();
                dataL = st.L; dataR = st.R;
                ready = true;
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // 5つのステムを計算する（Unity の外でも動く）。R が null のステムはモノラル
        public static (float[][] L, float[][] R) Render()
        {
            var dataL = new float[STEMS][]; var dataR = new float[STEMS][];
            {
                var rng = new Rng(777);
                var drums = new float[Total];
                var bass = new float[Total];
                var harmL = new float[Total]; var harmR = new float[Total];
                var lead = new float[Total];
                var intL = new float[Total]; var intR = new float[Total];

                // キックのサイドチェイン（パッドがキックに合わせて「うねる」やつ）
                var pump = new float[Total];
                for (int i = 0; i < Total; i++)
                {
                    double t = i / (double)SR;
                    double beat = t / (StepSec * 4);
                    double since = (beat - Math.Floor(beat)) * StepSec * 4;
                    pump[i] = (float)(1 - 0.65 * Math.Exp(-since * 9));
                }

                for (int bar = 0; bar < BARS; bar++)
                {
                    bool fill = bar == 7 || bar == 15;
                    bool sectionB = bar >= 8;
                    // ---------- ドラム ----------
                    for (int s = 0; s < 16; s++)
                    {
                        double t = StepT(bar, s);
                        if (s % 4 == 0 && !(fill && s == 12)) Kick(drums, t, 1f);
                        if (sectionB && s == 14 && bar % 2 == 1) Kick(drums, t, 0.7f);
                        if ((s == 4 || s == 12) && !(fill && s == 12)) Snare(drums, t, 1f, rng);
                        if (fill && s >= 12) { Snare(drums, t, 0.55f + (s - 12) * 0.15f, rng); Snare(drums, t + StepSec / 2, 0.5f + (s - 12) * 0.15f, rng); Tom(drums, t, 150 - (s - 12) * 22); }
                        Hat(drums, t, s % 4 == 2 ? 0.55f : 0.28f, s % 4 == 2 ? 0.16f : 0.035f, rng);
                    }
                    if (bar == 0 || bar == 8) Crash(drums, StepT(bar, 0), rng);

                    // ---------- ベース ----------
                    var ch = Prog[bar];
                    int[] bs = { 0, 2, 3, 6, 8, 10, 11, 14 };
                    int[] bo = { 0, 0, 12, 0, 0, 0, 12, 7 };
                    for (int k = 0; k < bs.Length; k++)
                        BassNote(bass, StepT(bar, bs[k]), StepSec * 1.6, ch.root + bo[k]);

                    // ---------- アルペジオ・パッド ----------
                    int[] pat = { 0, 1, 2, 3, 2, 1, 2, 3, 0, 1, 2, 3, 2, 3, 1, 2 };
                    for (int s = 0; s < 16; s++)
                    {
                        int n = pat[s] == 3 ? ch.tones[0] + 12 : ch.tones[pat[s]];
                        Pluck(harmL, harmR, StepT(bar, s), n + 12, s % 2 == 0 ? 0.8f : 0.35f, s % 2 == 0 ? 0.35f : 0.8f, pump);
                    }
                    Pad(harmL, harmR, StepT(bar, 0), StepSec * 16, ch.tones, pump);

                    // ---------- リード ----------
                    var mel = Melody[bar];
                    float pos = 0;
                    for (int k = 0; k < mel.Length; k += 2)
                    {
                        if (mel[k] > 0) LeadNote(lead, StepT(bar, pos), mel[k + 1] * StepSec * 0.92, mel[k]);
                        pos += mel[k + 1];
                    }

                    // ---------- 激化パート（第2形態） ----------
                    foreach (int s in new[] { 2, 6, 10, 13 })
                        Stab(intL, intR, StepT(bar, s), ch.tones);
                    for (int s = 0; s < 16; s++) Ride(intL, intR, StepT(bar, s), s % 2 == 0 ? 0.5f : 0.25f, rng);
                    if (fill) for (int s = 0; s < 16; s++) Snare(intL, StepT(bar, s), 0.15f + s * 0.035f, rng);
                    // メロディを1オクターブ下で重ねる
                    pos = 0;
                    for (int k = 0; k < mel.Length; k += 2)
                    {
                        if (mel[k] > 0) LeadNote(intR, StepT(bar, pos), mel[k + 1] * StepSec * 0.9, mel[k] - 12, 0.5f);
                        pos += mel[k + 1];
                    }
                }

                EchoLoop(lead, StepSec * 3, 0.35f, 0.45f);
                EchoLoop(harmL, StepSec * 3, 0.3f, 0.3f);
                EchoLoop(harmR, StepSec * 4, 0.3f, 0.3f);
                for (int i = 0; i < Total; i++) intL[i] += intR[i] * 0.3f;

                MasterMono(drums, 0.95f); MasterMono(bass, 0.8f); MasterMono(lead, 0.7f);
                MasterSt(harmL, harmR, 0.6f); MasterSt(intL, intR, 0.65f);

                dataL[0] = drums; dataL[1] = bass; dataL[2] = harmL; dataR[2] = harmR; dataL[3] = lead; dataL[4] = intL; dataR[4] = intR;
            }
            return (dataL, dataR);
        }

        // ループの継ぎ目でもこだまが途切れないディレイ（末尾のこだまが頭に回り込む）
        static void EchoLoop(float[] b, double sec, float fb, float mix)
        {
            int n = b.Length, d = (int)(sec * SR);
            var wet = new float[n];
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < n; i++)
                {
                    int j = i - d; if (j < 0) j += n;
                    wet[i] = (b[j] + wet[j]) * fb;
                }
            for (int i = 0; i < n; i++) b[i] += wet[i] * mix;
        }

        static void MasterMono(float[] b, float peak)
        {
            for (int i = 0; i < b.Length; i++) b[i] = SoftClip(b[i] * 1.2f);
            Normalize(b, peak);
        }
        static void MasterSt(float[] l, float[] r, float peak)
        {
            float m = 1e-6f;
            for (int i = 0; i < l.Length; i++) { l[i] = SoftClip(l[i]); r[i] = SoftClip(r[i]); m = Math.Max(m, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); }
            float k = peak / m;
            for (int i = 0; i < l.Length; i++) { l[i] *= k; r[i] *= k; }
        }

        // ---------- 楽器 ----------
        static void Kick(float[] b, double t0, float vel)
        {
            int s = At(t0); double ph = 0;
            for (int i = 0; i < SR * 0.35; i++)
            {
                float t = i / (float)SR;
                ph += (45 + 120 * Math.Exp(-t * 28)) / SR;
                Put(b, s + i, (Sin(ph) * Env(t, 0.001f, 0.16f) * 1.3f + (i < 60 ? 0.5f : 0)) * vel);
            }
        }
        static void Snare(float[] b, double t0, float vel, Rng rng)
        {
            int s = At(t0); var f = new SVF();
            for (int i = 0; i < SR * 0.22; i++)
            {
                float t = i / (float)SR;
                f.Run(rng.Noise(), 5000, 0.7f);
                Put(b, s + i, ((f.High + f.Band) * 0.6f * Env(t, 0.001f, 0.07f) + Sin(t * 190) * Env(t, 0.001f, 0.04f) * 0.7f) * vel);
            }
        }
        static void Hat(float[] b, double t0, float vel, float dec, Rng rng)
        {
            int s = At(t0); var f = new SVF();
            for (int i = 0; i < SR * (dec * 4); i++)
            {
                float t = i / (float)SR;
                f.Run(rng.Noise(), 8000, 0.8f);
                Put(b, s + i, f.High * Env(t, 0.0005f, dec) * vel * 0.45f);
            }
        }
        static void Crash(float[] b, double t0, Rng rng)
        {
            int s = At(t0); var f = new SVF();
            for (int i = 0; i < SR * 1.8; i++)
            {
                float t = i / (float)SR;
                f.Run(rng.Noise(), 6500, 0.6f);
                Put(b, s + i, f.High * Env(t, 0.002f, 0.6f) * 0.55f);
            }
        }
        static void Ride(float[] l, float[] r, double t0, float vel, Rng rng)
        {
            int s = At(t0);
            for (int i = 0; i < SR * 0.25; i++)
            {
                float t = i / (float)SR;
                float v = (Sq(t * 3200, 0.5) * 0.3f + Sq(t * 4530, 0.5) * 0.3f + rng.Noise() * 0.2f) * Env(t, 0.001f, 0.06f) * vel * 0.18f;
                Put(l, s + i, v * 0.6f); Put(r, s + i, v);
            }
        }
        static void Tom(float[] b, double t0, float f0)
        {
            int s = At(t0); double ph = 0;
            for (int i = 0; i < SR * 0.3; i++)
            {
                float t = i / (float)SR;
                ph += (f0 * (1 + 0.6 * Math.Exp(-t * 20))) / SR;
                Put(b, s + i, Sin(ph) * Env(t, 0.001f, 0.12f) * 0.8f);
            }
        }
        static void BassNote(float[] b, double t0, double len, int midi)
        {
            int s = At(t0); var f = new SVF(); float fr = Mtof(midi);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < SR * (len + 0.05); i++)
            {
                float t = i / (float)SR;
                p1 += fr / SR; p2 += fr * 1.007 / SR;
                f.Run(Saw(p1) + Saw(p2), 180 + 1600 * Env(t, 0.002f, 0.06f), 1.6f);
                float env = Adsr(t, (float)len, 0.003f, 0.08f, 0.75f, 0.04f);
                Put(b, s + i, (SoftClip(f.Low * 1.5f) * 0.8f + Sin(p1) * 0.6f) * env);
            }
        }
        static void Pluck(float[] l, float[] r, double t0, int midi, float gl, float gr, float[] pump)
        {
            int s = At(t0); var f = new SVF(); float fr = Mtof(midi);
            for (int i = 0; i < SR * 0.3; i++)
            {
                float t = i / (float)SR;
                f.Run(Saw(t * fr) * 0.7f + Sq(t * fr * 2, 0.5) * 0.2f, 700 + 5000 * Env(t, 0.001f, 0.05f), 1.8f);
                int idx = ((s + i) % Total + Total) % Total;
                float v = f.Low * Env(t, 0.002f, 0.09f) * 0.32f * pump[idx];
                l[idx] += v * gl; r[idx] += v * gr;
            }
        }
        static void Pad(float[] l, float[] r, double t0, double len, int[] tones, float[] pump)
        {
            int s = At(t0); var fl = new SVF(); var fr2 = new SVF();
            float[] det = { -0.012f, -0.005f, 0f, 0.006f, 0.013f };
            for (int i = 0; i < SR * (len + 0.3); i++)
            {
                float t = i / (float)SR;
                float sl = 0, sr = 0;
                foreach (var n in tones)
                {
                    float fq = Mtof(n);
                    for (int d = 0; d < det.Length; d++)
                    {
                        float v = Saw(t * fq * (1 + det[d]) + d * 0.13f + n * 0.07f);
                        if (d % 2 == 0) sl += v; else sr += v;
                        if (d == 2) { sl += v * 0.5f; sr += v * 0.5f; }
                    }
                }
                float cut = 1400 + 500 * Sin(t * 0.5f);
                fl.Run(sl, cut, 0.9f); fr2.Run(sr, cut, 0.9f);
                int idx = ((s + i) % Total + Total) % Total;
                float env = Adsr(t, (float)len, 0.08f, 0.2f, 0.8f, 0.3f) * 0.07f * pump[idx];
                l[idx] += fl.Low * env; r[idx] += fr2.Low * env;
            }
        }
        static void LeadNote(float[] b, double t0, double len, int midi, float gain = 1f)
        {
            int s = At(t0); var f = new SVF(); float fr = Mtof(midi);
            double ph = 0, ph2 = 0;
            for (int i = 0; i < SR * (len + 0.08); i++)
            {
                float t = i / (float)SR;
                float vib = 1 + 0.006f * Sin(t * 5.5f) * Mathf.Clamp01((t - 0.15f) * 4);
                ph += fr * vib / SR; ph2 += fr * vib * 1.004 / SR;
                f.Run(Saw(ph) * 0.6f + Saw(ph2) * 0.4f + Sq(ph * 0.5, 0.5) * 0.25f, 1200 + 3800 * Env(t, 0.01f, 0.25f) + 800, 1.4f);
                Put(b, s + i, f.Low * Adsr(t, (float)len, 0.008f, 0.12f, 0.75f, 0.07f) * 0.5f * gain);
            }
        }
        static void Stab(float[] l, float[] r, double t0, int[] tones)
        {
            int s = At(t0); var f = new SVF();
            for (int i = 0; i < SR * 0.3; i++)
            {
                float t = i / (float)SR; float v = 0;
                foreach (var n in tones) { float fq = Mtof(n); v += Saw(t * fq) + Saw(t * fq * 1.008f) + Sq(t * fq * 0.5f, 0.5) * 0.5f; }
                f.Run(v * 0.2f, 900 + 4000 * Env(t, 0.003f, 0.06f), 1.2f);
                float o = f.Low * Env(t, 0.003f, 0.09f) * 0.5f;
                Put(l, s + i, o); Put(r, s + i, o * 0.8f);
            }
        }
    }
}
