using System;
using System.Collections.Generic;
using UnityEngine;
using static AndoBoss.Synth;

namespace AndoBoss
{
    // 効果音。全部その場で合成して AudioClip にする
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        int next;
        public static float Volume = 0.9f;

        public static void Play(string name, float vol = 1f, float pitch = 1f, float pitchRand = 0.04f)
        {
            if (I == null || !I.clips.TryGetValue(name, out var clip)) return;
            // 同じ音が同じフレームに何十個も重なると割れるので間引く
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(name, out var t) && now - t < 0.025f) return;
            I.lastPlayed[name] = now;
            var src = I.pool[I.next];
            I.next = (I.next + 1) % I.pool.Count;
            src.clip = clip;
            src.volume = vol * Volume;
            src.pitch = pitch * (1 + UnityEngine.Random.Range(-pitchRand, pitchRand));
            src.Play();
        }

        public static AudioClip Get(string name) => I != null && I.clips.TryGetValue(name, out var c) ? c : null;

        void Awake()
        {
            I = this;
            for (int i = 0; i < 32; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                s.ignoreListenerPause = false;
                pool.Add(s);
            }
            Build();
        }

        void Build()
        {
            foreach (var kv in BuildData())
            {
                var c = AudioClip.Create(kv.Key, kv.Value.Length, 1, SR, false);
                c.SetData(kv.Value, 0);
                clips[kv.Key] = c;
            }
        }

        // 音のデータだけを作る（Unity の外でも動くので、WAV に書き出して確認できる）
        public static Dictionary<string, float[]> BuildData()
        {
            var data = new Dictionary<string, float[]>();
            void Add(string name, float[] d, float peak = 0.9f)
            {
                Fade(d);
                Normalize(d, peak);
                data[name] = d;
            }
            float[] clipData(string name) => (float[])data[name].Clone();

            var rng = new Rng(12345);

            // ---- 斬撃（3種類＋重い一撃） ----
            for (int k = 0; k < 4; k++)
            {
                float len = k == 3 ? 0.42f : 0.24f;
                var b = Buffer(len);
                var f = new SVF();
                float f0 = k == 3 ? 500 : 900 + k * 250, f1 = k == 3 ? 2600 : 4200 + k * 300;
                double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / len;
                    f.Run(rng.Noise(), Mathf.Lerp(f0, f1, Mathf.Sqrt(u)), 2.2f);
                    float env = Env(t, 0.012f, len * 0.3f);
                    ph += Mathf.Lerp(2600, 1700, u) / SR;
                    b[i] = f.Band * env * 1.4f + Sin(ph) * Env(t, 0.004f, 0.06f) * 0.12f;
                }
                Add("slash" + k, b, k == 3 ? 0.85f : 0.7f);
            }

            // ---- 命中 ----
            {
                var b = Buffer(0.22f);
                var f = new SVF(); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(190, 50, Mathf.Min(1, t / 0.12f)) / SR;
                    f.Run(rng.Noise(), 2500, 1);
                    b[i] = Sin(ph) * Env(t, 0.001f, 0.07f) + f.Band * Env(t, 0.0005f, 0.018f) * 1.5f
                         + SoftClip(Sq(t * 95) * 2) * Env(t, 0.001f, 0.03f) * 0.3f;
                }
                Add("hit", b);
            }
            // 会心（鐘のようなキラッとした音を重ねる）
            {
                var b = Buffer(0.7f);
                var hit = clipData("hit");
                Synth.Add(b, hit, 0, 1f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float bell = (Sin(t * 1568) + Sin(t * 2349) * 0.6f + Sin(t * 3136) * 0.4f) * Env(t, 0.002f, 0.22f);
                    b[i] += bell * 0.45f;
                }
                Add("crit", b);
            }

            // ---- 電撃 ----
            {
                var b = Buffer(0.35f);
                var f = new SVF(); double ph = 0; float fr = 600;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    if (i % 220 == 0) fr = rng.Range(250, 1400);
                    ph += fr / SR;
                    f.Run(Sq(ph) * 0.6f + rng.Noise() * 0.6f, 3000, 1.5f);
                    b[i] = SoftClip(f.Band * 3) * Env(t, 0.002f, 0.1f);
                }
                Add("zap", b, 0.75f);
            }

            // ---- 雷鳴 ----
            {
                var b = Buffer(1.6f);
                var hp = new SVF(); var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float n = rng.Noise();
                    hp.Run(n, 3000, 0.8f); lp.Run(n, 180 + 200 * Env(t, 0.01f, 0.3f), 0.9f);
                    b[i] = hp.High * Env(t, 0.0005f, 0.04f) * 1.4f + lp.Low * Env(t, 0.02f, 0.5f) * 3f + Sin(t * 50) * Env(t, 0.01f, 0.4f) * 0.4f;
                    b[i] = SoftClip(b[i] * 1.4f);
                }
                Add("thunder", b);
            }

            // ---- 爆発・着地の地響き ----
            {
                var b = Buffer(1.1f);
                var lp = new SVF(); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(130, 32, Mathf.Min(1, t / 0.5f)) / SR;
                    lp.Run(rng.Noise(), 700 * Env(t, 0.005f, 0.25f) + 80, 0.8f);
                    b[i] = SoftClip((Sin(ph) * Env(t, 0.003f, 0.35f) * 1.2f + lp.Low * Env(t, 0.003f, 0.4f) * 2.5f) * 1.6f);
                }
                Add("boom", b);
            }

            // ---- 回避の風切り ----
            {
                var b = Buffer(0.34f);
                var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 0.34f;
                    f.Run(rng.Noise(), 400 + 1600 * Mathf.Sin(u * Mathf.PI), 3f);
                    b[i] = f.Band * Mathf.Sin(u * Mathf.PI);
                }
                Add("whoosh", b, 0.6f);
            }

            // ---- ジャンプ・着地・足音 ----
            {
                var b = Buffer(0.16f); double ph = 0; var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(280, 700, t / 0.16f) / SR;
                    f.Run(rng.Noise(), 1200, 1);
                    b[i] = Tri(ph) * Env(t, 0.003f, 0.05f) * 0.5f + f.Band * Env(t, 0.001f, 0.03f);
                }
                Add("jump", b, 0.5f);
            }
            {
                var b = Buffer(0.15f); double ph = 0; var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(130, 60, t / 0.1f) / SR;
                    f.Run(rng.Noise(), 500, 1);
                    b[i] = Sin(ph) * Env(t, 0.002f, 0.04f) + f.Low * Env(t, 0.001f, 0.03f) * 2;
                }
                Add("land", b, 0.6f);
            }
            {
                var b = Buffer(0.06f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    f.Run(rng.Noise(), 900, 1.2f);
                    b[i] = f.Band * Env(t, 0.001f, 0.015f);
                }
                Add("step", b, 0.35f);
            }

            // ---- 元素スキル「放電」 ----
            {
                var b = Buffer(0.9f); var f = new SVF();
                double p1 = 0, p2 = 0, p3 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float rise = 1 + t * 0.6f;
                    p1 += 220 * rise / SR; p2 += 331 * rise / SR; p3 += 443 * rise / SR;
                    float trem = 0.6f + 0.4f * Sin(t * 32);
                    float chord = (Saw(p1) + Saw(p2) + Saw(p3)) * trem;
                    f.Run(chord + rng.Noise() * 0.8f, 1800 + 3000 * Env(t, 0.01f, 0.2f), 2);
                    b[i] = SoftClip(f.Low * Env(t, 0.005f, 0.3f) * 1.5f);
                }
                Synth.Add(b, clipData("zap"), 0, 0.8f);
                Synth.Add(b, clipData("boom"), 0, 0.35f);
                Add("skill", b);
            }

            // ---- 元素爆発のため（上昇音） ----
            {
                var b = Buffer(1.25f); var f = new SVF(); double ph = 0, ph2 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 1.25f;
                    ph += Mathf.Lerp(90, 1100, u * u) / SR; ph2 += Mathf.Lerp(91, 1112, u * u) / SR;
                    f.Run(Saw(ph) + Saw(ph2) + rng.Noise() * u, 400 + 6000 * u, 3);
                    float trem = 0.7f + 0.3f * Sin(t * (8 + 40 * u));
                    b[i] = f.Low * u * trem * Mathf.Clamp01((1.25f - t) * 30);
                }
                Add("charge", b, 0.8f);
            }
            // 元素爆発の落雷（大）
            {
                var b = Buffer(1.8f);
                Synth.Add(b, clipData("thunder"), 0, 1f);
                Synth.Add(b, clipData("boom"), 0, 0.8f);
                Synth.Add(b, clipData("crit"), 0, 0.5f);
                Add("burstHit", b);
            }
            // カットイン（シャキーン＋和音）
            {
                var b = Buffer(1.0f); var f = new SVF();
                float[] notes = { 62, 69, 74, 77, 81 };
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float s = 0;
                    foreach (var n in notes) s += Saw(t * Mtof(n)) + Saw(t * Mtof(n) * 1.006f);
                    f.Run(s * 0.2f, 800 + 7000 * Env(t, 0.01f, 0.25f), 1.5f);
                    b[i] = f.Low * Env(t, 0.005f, 0.45f);
                }
                Synth.Add(b, clipData("slash3"), 0, 0.8f);
                Add("cutin", b);
            }

            // ---- 被弾 ----
            {
                var b = Buffer(0.3f); double ph = 0; var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(240, 80, t / 0.3f) / SR;
                    f.Run(rng.Noise(), 1500, 1);
                    b[i] = SoftClip(Sq(ph) * 1.5f) * Env(t, 0.003f, 0.09f) * 0.6f + f.Band * Env(t, 0.001f, 0.05f) * 1.2f;
                }
                Add("hurt", b, 0.8f);
            }

            // ---- ボスの弾・衝撃波・警告・突進・咆哮 ----
            {
                var b = Buffer(0.14f); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(1500, 300, t / 0.14f) / SR;
                    b[i] = Sq(ph, 0.3) * Env(t, 0.001f, 0.05f);
                }
                Add("shoot", b, 0.4f);
            }
            {
                var b = Buffer(0.8f); double ph = 0; var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(95, 38, t / 0.8f) / SR;
                    lp.Run(rng.Noise(), 300, 1.2f);
                    float sw = Mathf.Sin(Mathf.Min(1, t / 0.8f) * Mathf.PI);
                    b[i] = (Sin(ph) * 1.2f + lp.Low * 3) * sw;
                }
                Add("wave", b, 0.8f);
            }
            {
                var b = Buffer(0.5f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float f0 = t < 0.25f ? 988 : 740;
                    float lt = t < 0.25f ? t : t - 0.25f;
                    b[i] = (Sq(t * f0, 0.5) * 0.5f + Sin(t * f0 * 2) * 0.3f) * Env(lt, 0.004f, 0.08f);
                }
                Add("warn", b, 0.5f);
            }
            {
                var b = Buffer(1.1f); var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    lp.Run(rng.Noise(), 260, 1.4f);
                    b[i] = lp.Low * 3 * (0.6f + 0.4f * Sin(t * 18)) * Env(t, 0.05f, 0.5f);
                }
                Add("rumble", b, 0.8f);
            }
            {
                var b = Buffer(2.0f); var f = new SVF();
                double p1 = 0, p2 = 0, p3 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 2f;
                    float fr = Mathf.Lerp(200, 55, u);
                    p1 += fr / SR; p2 += fr * 1.498f / SR; p3 += fr * 0.503f / SR;
                    f.Run(Saw(p1) + Saw(p2) + Saw(p3) + rng.Noise() * 0.7f, 1500 - 900 * u, 1.3f);
                    b[i] = SoftClip(f.Low * 2.5f) * Adsr(t, 1.4f, 0.08f, 0.3f, 0.8f, 0.6f);
                }
                Synth.Add(b, clipData("boom"), 0, 0.6f);
                Add("roar", b);
            }

            // ---- レーザー ----
            {
                var b = Buffer(1.0f); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (200 + 700 * t + 20 * Sin(t * 12)) / SR;
                    b[i] = (Sin(ph) + Sin(ph * 2.01) * 0.4f) * t;
                }
                Add("laserCharge", b, 0.55f);
            }
            {
                var b = Buffer(2.2f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float s = Saw(t * 110) + Saw(t * 221) * 0.7f + Sq(t * 55) * 0.5f + rng.Noise() * 0.4f;
                    f.Run(s, 2400 + 600 * Sin(t * 7), 2);
                    b[i] = SoftClip(f.Low * 1.5f) * Adsr(t, 1.9f, 0.02f, 0.1f, 0.85f, 0.3f);
                }
                Add("laser", b, 0.6f);
            }

            // ---- 元素エネルギー・ゲージ満タン ----
            {
                float[] ns = { 84, 88, 91, 96 };
                var b = Buffer(0.9f);
                for (int k = 0; k < ns.Length; k++)
                {
                    float fr = Mtof(ns[k]); int off = (int)(k * 0.07f * SR);
                    for (int i = 0; i + off < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        b[i + off] += (Sin(t * fr) + Sin(t * fr * 2.01f) * 0.3f) * Env(t, 0.002f, 0.25f);
                    }
                }
                Add("ready", b, 0.55f);
            }
            {
                var b = Buffer(0.12f);
                for (int i = 0; i < b.Length; i++) { float t = i / (float)SR; b[i] = (Sin(t * 1760) + Sin(t * 2637) * 0.5f) * Env(t, 0.001f, 0.035f); }
                Add("orb", b, 0.35f);
            }

            // ---- コンボ・UI ----
            {
                var b = Buffer(0.09f);
                for (int i = 0; i < b.Length; i++) { float t = i / (float)SR; b[i] = Tri(t * 1320) * Env(t, 0.001f, 0.03f); }
                Add("tick", b, 0.35f);
            }
            {
                var b = Buffer(0.3f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR; float fr = t < 0.08f ? 988 : 1319; float lt = t < 0.08f ? t : t - 0.08f;
                    b[i] = (Sq(t * fr, 0.25) * 0.4f + Sin(t * fr) * 0.6f) * Env(lt, 0.002f, 0.07f);
                }
                Add("confirm", b, 0.45f);
            }
            {
                var b = Buffer(0.18f); double ph = 0;
                for (int i = 0; i < b.Length; i++) { float t = i / (float)SR; ph += Mathf.Lerp(500, 1400, t / 0.18f) / SR; b[i] = Sin(ph) * Env(t, 0.002f, 0.06f); }
                Add("pop", b, 0.45f);
            }
            {
                var b = Buffer(0.35f); var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    lp.Run(rng.Noise(), 1200, 1);
                    b[i] = Sin(t * Mathf.Lerp(110, 50, t * 3)) * Env(t, 0.001f, 0.08f) * 1.3f + lp.Low * Env(t, 0.001f, 0.05f) * 2.5f;
                }
                Add("stamp", b);
            }

            // ---- ブレイク（ガラスが割れる） ----
            {
                var b = Buffer(1.3f); var hp = new SVF();
                for (int k = 0; k < 24; k++)
                {
                    float fr = rng.Range(2200, 7500); int off = (int)(rng.Range(0, 0.25f) * SR);
                    float dec = rng.Range(0.05f, 0.35f);
                    for (int i = 0; i + off < b.Length; i++) { float t = i / (float)SR; b[i + off] += Sin(t * fr) * Env(t, 0.0005f, dec) * 0.3f; }
                }
                for (int i = 0; i < b.Length; i++) { float t = i / (float)SR; hp.Run(rng.Noise(), 5000, 0.8f); b[i] += hp.High * Env(t, 0.001f, 0.12f) * 1.2f; }
                Synth.Add(b, clipData("boom"), 0, 0.7f);
                Add("break", b);
            }

            // ---- ジャスト回避（逆再生シンバル＋チーン） ----
            {
                var b = Buffer(0.9f); var hp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    hp.Run(rng.Noise(), 6000, 0.7f);
                    float sw = t < 0.25f ? (t / 0.25f) * (t / 0.25f) : 0;
                    b[i] = hp.High * sw * 0.8f + (Sin(t * 2637) + Sin(t * 3951) * 0.5f) * Env(t - 0.25f, 0.002f, 0.25f) * (t > 0.25f ? 1 : 0);
                }
                Add("perfect", b, 0.7f);
            }

            // ---- 勝利ファンファーレ ----
            {
                var b = Buffer(3.4f); var f = new SVF();
                // (開始拍, 長さ拍, 音) 120BPM
                float[,] mel = { { 0, 0.5f, 67 }, { 0.5f, 0.5f, 72 }, { 1, 0.5f, 76 }, { 1.5f, 0.5f, 79 }, { 2, 1.5f, 84 }, { 3.5f, 0.5f, 83 }, { 4, 2.5f, 86 } };
                float[] chord = { 60, 67, 72, 76, 79 };
                for (int n = 0; n < mel.GetLength(0); n++)
                {
                    float st = mel[n, 0] * 0.5f, len = mel[n, 1] * 0.5f, fr = Mtof(mel[n, 2]);
                    int off = (int)(st * SR);
                    for (int i = 0; i + off < b.Length && i < (len + 0.4f) * SR; i++)
                    {
                        float t = i / (float)SR;
                        float vib = 1 + 0.004f * Sin(t * 6) * Mathf.Clamp01(t * 4);
                        b[i + off] += (Saw(t * fr * vib) * 0.6f + Sq(t * fr * vib, 0.3) * 0.3f) * Adsr(t, len, 0.02f, 0.1f, 0.8f, 0.3f);
                    }
                }
                int co = (int)(2.0f * 0.5f * SR);
                for (int i = 0; i + co < b.Length; i++)
                {
                    float t = i / (float)SR; float s = 0;
                    foreach (var c in chord) s += Saw(t * Mtof(c)) + Saw(t * Mtof(c) * 1.004f);
                    b[i + co] += s * 0.08f * Adsr(t, 1.8f, 0.05f, 0.4f, 0.7f, 0.6f);
                }
                var o = new float[b.Length];
                for (int i = 0; i < b.Length; i++) { f.Run(b[i], 3500, 0.9f); o[i] = f.Low; }
                Echo(o, 0.18f, 0.35f, 0.4f);
                Synth.Add(o, clipData("crit"), 0, 0.4f);
                Add("fanfare", o);
            }
            // ---- 敗北 ----
            {
                var b = Buffer(3f);
                float[] ns = { 69, 67, 65, 64 };
                for (int k = 0; k < ns.Length; k++)
                {
                    float fr = Mtof(ns[k]); int off = (int)(k * 0.5f * SR);
                    for (int i = 0; i + off < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        b[i + off] += (Tri(t * fr) + Sin(t * fr * 0.5f) * 0.5f) * Adsr(t, k == 3 ? 1.4f : 0.45f, 0.02f, 0.1f, 0.7f, 0.5f);
                    }
                }
                Echo(b, 0.25f, 0.3f, 0.4f);
                Add("defeat", b, 0.6f);
            }
            return data;
        }

    }
}
