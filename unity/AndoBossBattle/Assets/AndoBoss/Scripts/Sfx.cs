using System;
using System.Collections.Generic;
using UnityEngine;
using static AndoBoss.Synth;

namespace AndoBoss
{
    // 効果音。全部その場で合成して AudioClip にする。
    // 高い「ピコッ」「ポン」という音は安っぽく聞こえるので、低い胴鳴り＋ノイズ＋残響を中心に作っている。
    // 仕上げ（Polish）で、ステレオの部屋鳴り・残響の余韻・耳に痛い高域のカット・軽いサチュレーションをかける。
    // よく鳴る音（命中・斬撃・足音など）は少しずつちがう版を3つ作り、鳴らすたびに選ぶ（同じ音のくり返し感を消す）
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        int next;
        public static float Volume = 0.9f;

        public static void Play(string name, float vol = 1f, float pitch = 1f, float pitchRand = 0.04f)
        {
            if (I == null || !I.clips.TryGetValue(name, out var list)) return;
            var clip = list[UnityEngine.Random.Range(0, list.Count)];
            // 同じ音が同じフレームに何十個も重なると割れるので間引く
            float now = Time.unscaledTime;
            if (I.lastPlayed.TryGetValue(name, out var t) && now - t < 0.03f) return;
            I.lastPlayed[name] = now;
            var src = I.pool[I.next];
            I.next = (I.next + 1) % I.pool.Count;
            src.clip = clip;
            src.volume = vol * Volume;
            src.pitch = pitch * (1 + UnityEngine.Random.Range(-pitchRand, pitchRand));
            // ほんの少しだけ左右に散らして、重なったときに団子にならないようにする
            src.panStereo = UnityEngine.Random.Range(-0.12f, 0.12f);
            src.Play();
        }

        void Awake()
        {
            I = this;
            for (int i = 0; i < 32; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                pool.Add(s);
            }
            // データはステレオ（左右交互）。"name#1" のような名前は "name" の別バージョン
            foreach (var kv in BuildData())
            {
                var c = AudioClip.Create(kv.Key, kv.Value.Length / 2, 2, SR, false);
                c.SetData(kv.Value, 0);
                int sharp = kv.Key.IndexOf('#');
                string key = sharp >= 0 ? kv.Key.Substring(0, sharp) : kv.Key;
                if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
                list.Add(c);
            }
        }

        // ---- 仕上げ ----
        // 残響だけを作る（くし形フィルター6本＋オールパス3本）。scale を左右で変えると広がりが出る
        static float[] RoomWet(float[] src, float scale, float size)
        {
            int[] cd = { 1557, 1617, 1491, 1422, 1277, 1356 };
            int[] ad = { 225, 556, 441 };
            var wet = new float[src.Length];
            float fb = 0.76f + 0.12f * Math.Min(1f, size);
            foreach (var d0 in cd)
            {
                int d = Math.Max(8, (int)(d0 * scale * (0.7f + size * 0.7f)));
                var buf = new float[d]; int idx = 0; float lp = 0;
                for (int i = 0; i < src.Length; i++)
                {
                    float y = buf[idx];
                    lp = y * 0.58f + lp * 0.42f;
                    buf[idx] = src[i] + lp * fb;
                    idx = (idx + 1) % d;
                    wet[i] += y * (1f / 6);
                }
            }
            foreach (var d0 in ad)
            {
                int d = Math.Max(4, (int)(d0 * scale));
                var buf = new float[d]; int idx = 0;
                for (int i = 0; i < src.Length; i++)
                {
                    float bo = buf[idx];
                    float y = -wet[i] + bo;
                    buf[idx] = wet[i] + bo * 0.5f;
                    idx = (idx + 1) % d;
                    wet[i] = y;
                }
            }
            // 残響の低音はにごるので切る
            var hp = new SVF();
            for (int i = 0; i < wet.Length; i++) { hp.Run(wet[i], 170, 0.7f); wet[i] = hp.High; }
            return wet;
        }

        // モノラルの素材を、ステレオの完成品（左右交互）にする
        static float[] Polish(float[] d, float peak, float room)
        {
            int pad = (int)(SR * (0.12f + room * 0.8f));
            var m = new float[d.Length + pad];
            Array.Copy(d, m, d.Length);
            // 耳に痛い高域と、聞こえない超低域を切る
            var lp = new SVF(); var hp = new SVF();
            for (int i = 0; i < m.Length; i++) { lp.Run(m[i], 9000, 0.6f); hp.Run(lp.Low, 30, 0.7f); m[i] = hp.High; }
            float[] wl = null, wr = null;
            if (room > 0) { wl = RoomWet(m, 1f, room); wr = RoomWet(m, 1.13f, room); }
            float mix = 0.3f * Math.Min(1.5f, room);
            // 余韻が消えたところで切る
            float mx = 1e-6f;
            for (int i = 0; i < m.Length; i++) mx = Math.Max(mx, Math.Abs(m[i]));
            int end = m.Length;
            for (int i = m.Length - 1; i > d.Length / 2; i--)
            {
                float v = Math.Abs(m[i]) + (wl != null ? Math.Abs(wl[i]) * mix : 0);
                if (v > mx * 0.002f) { end = Math.Min(m.Length, i + (int)(0.02f * SR)); break; }
            }
            var st = new float[end * 2];
            for (int i = 0; i < end; i++)
            {
                float l = m[i] + (wl != null ? wl[i] * mix : 0);
                float r = m[i] + (wr != null ? wr[i] * mix : 0);
                // 軽いサチュレーションでまとめる
                st[i * 2] = (float)Math.Tanh(l * 1.15f);
                st[i * 2 + 1] = (float)Math.Tanh(r * 1.15f);
            }
            float pk = 1e-6f;
            foreach (var x in st) pk = Math.Max(pk, Math.Abs(x));
            float k = peak / pk;
            int fo = Math.Min(end, (int)(0.03f * SR)), fi = Math.Min(end, (int)(0.0015f * SR));
            for (int i = 0; i < end; i++)
            {
                float g = k;
                if (i < fi) g *= i / (float)fi;
                if (i > end - fo) g *= (end - i) / (float)fo;
                st[i * 2] *= g; st[i * 2 + 1] *= g;
            }
            return st;
        }

        // 音のデータだけを作る（Unity の外でも動くので、WAV に書き出して確認できる）。値はステレオ（左右交互）
        public static Dictionary<string, float[]> BuildData()
        {
            var data = new Dictionary<string, float[]>();
            var mono = new Dictionary<string, float[]>();
            var rng = new Rng(12345);
            // room：部屋鳴りの量（0 でなし、1 がふつう、1.5 で大きな空間）
            void Add(string name, float[] d, float peak = 0.9f, float room = 1f)
            {
                Normalize(d, 1f);
                mono[name] = (float[])d.Clone();
                data[name] = Polish(d, peak, room);
            }
            float[] Get(string name) => (float[])mono[name].Clone();
            string V(int v) => v == 0 ? "" : "#" + v;

            // 打撃の素：低い胴鳴り（f0→f1 に下がる）＋ローパスしたノイズ＋少しの歪み
            float[] Impact(float len, float f0, float f1, float bodyDec, float noiseCut, float noiseDec, float drive = 1.5f, float noiseAmt = 1f)
            {
                var b = Buffer(len);
                var lp = new SVF(); var hpc = new SVF(); double ph = 0, phSub = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (f1 + (f0 - f1) * Math.Exp(-t * 30)) / SR;
                    lp.Run(rng.Noise(), noiseCut, 0.8f);
                    float v = Sin(ph) * Env(t, 0.001f, bodyDec) + lp.Low * Env(t, 0.0005f, noiseDec) * 2.2f * noiseAmt;
                    // 重さ：おなかに来る低いドン（周波数が下がっていく）
                    phSub += (38 + 26 * Math.Exp(-t * 18)) / SR;
                    v += Sin(phSub) * Env(t, 0.002f, bodyDec * 1.6f + 0.03f) * 0.55f;
                    // 立ち上がりの芯：ごく短い「カッ」で輪郭をつける
                    hpc.Run(rng.Noise(), 3200, 0.8f);
                    v += hpc.High * Env(t, 0.0002f, 0.0035f) * 0.45f;
                    b[i] = SoftClip(v * drive);
                }
                return b;
            }

            // 風切り音（f0→f1→f2 と帯域が動く）
            float[] Swish(float len, float f0, float f1, float q = 2.5f)
            {
                var b = Buffer(len);
                var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / len;
                    f.Run(rng.Noise(), Mathf.Lerp(f0, f1, u), q);
                    b[i] = f.Band * Mathf.Sin(Mathf.Min(1, u * 1.2f) * Mathf.PI) * 1.5f;
                }
                return b;
            }

            // ================= 主人公の攻撃 =================
            // 斬撃：低めの風切り＋刃の重み。4段目は重く
            for (int k = 0; k < 4; k++)
                for (int v = 0; v < 3; v++)
                {
                    float len = k == 3 ? 0.5f : 0.3f, j = rng.Range(0.92f, 1.08f);
                    var b = Swish(len, (k == 3 ? 350 : 500 + k * 120) * j, (k == 3 ? 1600 : 2200 + k * 150) * j, 2f);
                    // 刃が空気を切る「シャッ」の芯（細い帯域の風）
                    Synth.Add(b, Swish(len * 0.6f, 2600 * j, 4200 * j, 5f), (int)(len * 0.15f * SR), 0.35f);
                    var body = Impact(len, 160 * j, 70, 0.05f, 900, 0.02f, 1.2f, 0.4f);
                    Synth.Add(b, body, (int)(len * 0.3f * SR), k == 3 ? 0.5f : 0.25f);
                    Add("slash" + k + V(v), b, k == 3 ? 0.85f : 0.7f, 0.7f);
                }

            // 命中：ドスッと低く。電気の「バチッ」は低めの帯域で少しだけ
            for (int v = 0; v < 3; v++)
            {
                float j = rng.Range(0.9f, 1.1f);
                var b = Impact(0.35f, 170 * j, 50 * j, 0.07f, 1600, 0.035f, 1.8f);
                var f = new SVF();
                for (int i = 0; i < SR * 0.08f; i++)
                {
                    float t = i / (float)SR;
                    f.Run(rng.Noise() * (rng.Next() < 0.3f ? 1 : 0.2f), 1400 * j, 1.2f);
                    b[i] += f.Band * Env(t, 0.001f, 0.025f) * 0.8f;
                }
                Add("hit" + V(v), b, 0.9f, 0.8f);
            }
            // 会心：もっと重く、長い残響
            for (int v = 0; v < 3; v++)
            {
                float j = rng.Range(0.92f, 1.08f);
                var b = Impact(0.9f, 140 * j, 38, 0.14f, 2200, 0.06f, 2.2f);
                var s = Swish(0.35f, 2500 * j, 600, 1.5f);
                Synth.Add(b, s, 0, 0.35f);
                Add("crit" + V(v), b, 0.9f, 1.3f);
            }
            // 電撃（命中に重ねる）：低めのバリバリ
            {
                var b = Buffer(0.3f);
                var f = new SVF(); double ph = 0; float fr = 200;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    if (i % 300 == 0) fr = rng.Range(80, 420);
                    ph += fr / SR;
                    f.Run(Sq(ph) * 0.7f + rng.Noise() * 0.5f, 1300, 1.2f);
                    b[i] = SoftClip(f.Low * 2.5f) * Env(t, 0.002f, 0.08f);
                }
                Add("zap", b, 0.7f);
            }
            // 多段ヒット（レポートのババババ）：短く太い電撃の粒
            for (int v = 0; v < 3; v++)
            {
                var b = Buffer(0.16f);
                var f = new SVF(); var lp2 = new SVF(); double ph = 0;
                float j = rng.Range(0.88f, 1.12f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(220, 90, t / 0.16f) * j / SR;
                    f.Run(rng.Noise(), 1800 * j, 1);
                    lp2.Run(Sq(ph), 1400, 0.8f);
                    b[i] = SoftClip((lp2.Low * 0.8f + f.Band * 1.5f + Sin(ph * 0.5) * 0.6f) * 2f) * Env(t, 0.001f, 0.035f);
                }
                Add("multihit" + V(v), b, 0.8f, 0.6f);
            }
            // レポートを投げる：紙がバサッ＋風切り
            {
                var b = Swish(0.4f, 700, 2500, 1.2f);
                var f = new SVF();
                for (int i = 0; i < SR * 0.12f; i++)
                {
                    float t = i / (float)SR;
                    f.Run(rng.Noise() * (0.5f + 0.5f * Sin(t * 60)), 2500, 0.8f);
                    b[i] += f.Band * Env(t, 0.002f, 0.04f) * 1.5f;
                }
                Synth.Add(b, Get("zap"), (int)(0.05f * SR), 0.5f);
                Add("paper", b, 0.75f);
            }

            // 弓（やましょう）：弦のビンッという低い音＋風切り
            {
                var b = Buffer(0.4f); var f = new SVF(); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (140 + 60 * Math.Exp(-t * 40)) / SR;
                    f.Run(Saw(ph) + rng.Noise() * 0.3f, 900 * Env(t, 0.001f, 0.05f) + 200, 3);
                    b[i] = f.Low * Env(t, 0.001f, 0.12f) * 1.5f;
                }
                Synth.Add(b, Swish(0.3f, 1400, 500, 2f), (int)(0.02f * SR), 0.6f);
                Add("arrow", b, 0.7f, 0.72f);
            }

            // エナジードリンク：缶を開ける「プシュッ」＋ごくごく
            {
                var b = Buffer(1.0f); var hp = new SVF(); var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float n = rng.Noise();
                    hp.Run(n, 2500, 0.7f);
                    b[i] = hp.Band * Env(t, 0.002f, 0.08f) * 0.8f;
                    for (int g = 0; g < 3; g++)
                    {
                        float gt = t - 0.3f - g * 0.2f;
                        if (gt > 0 && gt < 0.15f) b[i] += Sin(gt * Mathf.Lerp(180, 110, gt / 0.15f)) * Env(gt, 0.01f, 0.05f) * 0.9f;
                    }
                    lp.Run(b[i], 3000, 0.8f); b[i] = lp.Low;
                }
                Add("drink", b, 0.7f);
            }

            // ---- 菅原先生の声：かすれたささやき声（息の音を母音のように響かせ、ガラガラした低いうなりを混ぜる） ----
            {
                float[,] vowels = { { 750, 1200 }, { 300, 2300 }, { 350, 1300 }, { 500, 1900 }, { 480, 850 } }; // あいうえお
                for (int k = 0; k < 3; k++)
                {
                    float len = 1.3f + k * 0.3f;
                    var b = Buffer(len);
                    var f1 = new SVF(); var f2 = new SVF(); var hp = new SVF();
                    float syl = 0.13f + k * 0.01f;
                    int v = k; double ph = 0;
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        int sIdx = (int)(t / syl);
                        float st = t - sIdx * syl;
                        if (st < 1f / SR * 2) v = (v * 7 + sIdx * 3 + k) % 5;
                        float env = Mathf.Sin(Mathf.Clamp01(st / syl) * Mathf.PI);
                        env *= 0.6f + 0.4f * Mathf.Sin(sIdx * 1.7f + k);
                        // ガラガラ：低い周期でパルス状に息が途切れる（声のかすれ）
                        ph += (38 + 8 * Sin(t * 3)) / SR;
                        float fry = Mathf.Pow(Mathf.Max(0, Saw(ph)), 6) * 1.5f + 0.35f;
                        float n = rng.Noise() * fry;
                        f1.Run(n, vowels[v, 0], 5f); f2.Run(n, vowels[v, 1], 6f); hp.Run(n, 4500, 0.7f);
                        b[i] = (f1.Band * 1.2f + f2.Band * 0.9f + hp.High * 0.15f) * env * Mathf.Clamp01((len - t) * 5);
                    }
                    Add("whisper" + k, b, 0.55f, 0.63f);
                }
            }
            // 放送禁止の「ピー」音（テレビの自主規制っぽく）
            {
                var b = Buffer(0.75f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    b[i] = Mathf.Sin(2 * Mathf.PI * 1000 * t) * 0.35f * Mathf.Clamp01(t * 80) * Mathf.Clamp01((0.75f - t) * 80);
                }
                Add("bleep", b, 0.5f, 0f);
            }
            // カウントがつく音：重い判子＋鎖のようなジャラッとした音
            {
                var b = Impact(0.9f, 120, 45, 0.15f, 700, 0.06f, 2f);
                var bp = new SVF();
                for (int i = 0; i < SR * 0.35f; i++)
                {
                    float t = i / (float)SR;
                    bp.Run(rng.Noise() * (rng.Next() < 0.08f ? 3 : 0.3f), 1500, 3);
                    b[i + (int)(0.05f * SR)] += bp.Band * Env(t, 0.005f, 0.12f) * 0.8f;
                }
                Add("count", b, 0.85f, 1.26f);
            }

            // ---- 炎（杉山くん）----
            {
                var b = Buffer(1.1f);
                var lp = new SVF(); var bp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float n = rng.Noise();
                    lp.Run(n, 500 + 1500 * Env(t, 0.05f, 0.25f), 0.7f);
                    bp.Run(n * (rng.Next() < 0.02f ? 4 : 0.3f), 1200, 2);
                    b[i] = (lp.Low * 2.5f + bp.Band * 0.8f) * Adsr(t, 0.6f, 0.06f, 0.2f, 0.7f, 0.4f);
                }
                Add("fire", b, 0.8f, 0.90f);
            }
            {
                var b = Impact(1.4f, 110, 30, 0.3f, 900, 0.45f, 2.4f, 1.4f);
                Synth.Add(b, Get("fire"), (int)(0.02f * SR), 0.6f);
                Add("explode", b, 0.9f, 1.4f);
            }

            // ---- 氷（やましょう）：ザクッと割れる音（高い鈴の音は使わない） ----
            {
                var b = Impact(0.5f, 200, 90, 0.05f, 3500, 0.03f, 1.6f, 1.2f);
                var f = new SVF();
                for (int k = 0; k < 6; k++)
                {
                    int off = (int)(rng.Range(0, 0.1f) * SR);
                    for (int i = 0; i < SR * 0.05f && i + off < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        f.Run(rng.Noise(), 2800, 3);
                        b[i + off] += f.Band * Env(t, 0.0005f, 0.012f) * 0.8f;
                    }
                }
                Add("ice", b, 0.8f, 0.81f);
            }
            {
                var b = Buffer(1.6f);
                var f = new SVF(); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 1.6f;
                    ph += Mathf.Lerp(60, 35, u) / SR;
                    f.Run(rng.Noise(), Mathf.Lerp(4000, 600, u), 1.5f);
                    b[i] = (f.Band * 1.2f * (1 - u) + Sin(ph) * 0.8f) * Env(t, 0.01f, 0.6f);
                }
                Synth.Add(b, Get("ice"), 0, 0.8f);
                Add("freeze", b, 0.9f, 1.35f);
            }

            // ================= 共通 =================
            {
                var b = Impact(1.8f, 120, 40, 0.3f, 900, 0.4f, 2.2f, 1.3f);
                var hp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    hp.Run(rng.Noise(), 2500, 0.8f);
                    b[i] += hp.High * Env(t, 0.0005f, 0.03f) * 0.8f;
                }
                Add("thunder", b, 0.9f, 1.5f);
            }
            {
                var b = Impact(1.2f, 130, 32, 0.35f, 700, 0.4f, 2.2f, 1.2f);
                Add("boom", b, 0.9f, 1.4f);
            }
            Add("whoosh", Swish(0.34f, 350, 1400, 2.5f), 0.6f);
            Add("jump", Swish(0.2f, 400, 1100, 2f), 0.45f);
            Add("land", Impact(0.2f, 130, 60, 0.04f, 500, 0.03f, 1.2f), 0.55f, 0.5f);
            for (int v = 0; v < 3; v++)
            {
                // 足音：砂をふむザッ＋かかとの低いトッ
                var b = Buffer(0.09f); var f = new SVF();
                float j = rng.Range(0.85f, 1.15f);
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    f.Run(rng.Noise(), 900 * j, 1.1f);
                    b[i] = f.Band * Env(t, 0.002f, 0.018f) + Sin(t * 95 * j) * Env(t, 0.001f, 0.012f) * 0.6f;
                }
                Add("step" + V(v), b, 0.3f, 0.3f);
            }
            // キャラ交代：ブワッ＋低いドン
            {
                var b = Swish(0.45f, 300, 1800, 1.5f);
                Synth.Add(b, Impact(0.4f, 110, 50, 0.08f, 600, 0.05f, 1.5f), (int)(0.12f * SR), 0.6f);
                Add("swap", b, 0.75f, 0.90f);
            }
            // 奥義のため（上昇音）
            {
                var b = Buffer(1.25f); var f = new SVF(); double ph = 0, ph2 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 1.25f;
                    ph += Mathf.Lerp(55, 440, u * u) / SR; ph2 += Mathf.Lerp(55.5f, 446, u * u) / SR;
                    f.Run(Saw(ph) + Saw(ph2) + rng.Noise() * u * 0.8f, 300 + 2500 * u, 2);
                    b[i] = f.Low * u * Mathf.Clamp01((1.25f - t) * 30);
                }
                Add("charge", b, 0.8f);
            }
            // 奥義の大きい一撃
            {
                var b = Buffer(2.2f);
                Synth.Add(b, Get("thunder"), 0, 1f);
                Synth.Add(b, Get("boom"), 0, 0.8f);
                Synth.Add(b, Get("crit"), 0, 0.4f);
                Add("burstHit", b);
            }
            // カットイン：シャキーン（低めの和音）＋斬撃
            {
                var b = Buffer(1.2f); var f = new SVF();
                float[] notes = { 50, 57, 62, 65, 69 };
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float s = 0;
                    foreach (var n in notes) s += Saw(t * Mtof(n)) + Saw(t * Mtof(n) * 1.006f);
                    f.Run(s * 0.2f, 500 + 3000 * Env(t, 0.01f, 0.25f), 1.3f);
                    b[i] = f.Low * Env(t, 0.005f, 0.5f);
                }
                Synth.Add(b, Get("slash3"), 0, 0.8f);
                Add("cutin", b, 0.9f, 1.08f);
            }
            // 被弾
            {
                var b = Impact(0.35f, 150, 55, 0.08f, 1200, 0.05f, 2.5f);
                Add("hurt", b, 0.8f, 0.72f);
            }
            // 鈍足になった：音程が下がるうねり
            {
                var b = Buffer(0.9f); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (Mathf.Lerp(220, 70, t / 0.9f) * (1 + 0.05f * Sin(t * 9))) / SR;
                    b[i] = (Saw(ph) * 0.4f + Sin(ph) * 0.6f) * Env(t, 0.02f, 0.4f);
                }
                var o = new float[b.Length]; var f = new SVF();
                for (int i = 0; i < b.Length; i++) { f.Run(b[i], 900, 1); o[i] = f.Low; }
                Add("slowdown", o, 0.7f);
            }

            // ================= 安東先生の攻撃 =================
            // 弾：シュッと太く
            for (int v = 0; v < 3; v++)
            {
                float j = rng.Range(0.9f, 1.1f);
                var b = Swish(0.16f, 1000 * j, 350 * j, 1.2f);
                Synth.Add(b, Impact(0.12f, 220 * j, 110, 0.03f, 800, 0.02f, 1.2f, 0.5f), 0, 0.5f);
                Add("shoot" + V(v), b, 0.5f, 0.7f);
            }
            {
                var b = Buffer(0.9f); double ph = 0; var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += Mathf.Lerp(95, 38, t / 0.9f) / SR;
                    lp.Run(rng.Noise(), 300, 1.2f);
                    float sw = Mathf.Sin(Mathf.Min(1, t / 0.9f) * Mathf.PI);
                    b[i] = (Sin(ph) * 1.2f + lp.Low * 3) * sw;
                }
                Add("wave", b, 0.8f, 1.17f);
            }
            // 警告：低いブザー（ピコピコしない）
            {
                var b = Buffer(0.6f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float on = (t < 0.22f || (t > 0.3f && t < 0.52f)) ? 1 : 0;
                    f.Run(Saw(t * 147) + Saw(t * 148.5f) + Sq(t * 73.5f), 900, 1.2f);
                    b[i] = f.Low * on;
                }
                var o = new float[b.Length];
                float g = 0;
                for (int i = 0; i < b.Length; i++) { g += ((b[i] != 0 ? 1 : 0) - g) * 0.004f; o[i] = b[i] * g; }
                Add("warn", o, 0.5f);
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
            // いやいや攻撃の着地：ドタッ
            for (int v = 0; v < 2; v++)
            {
                var b = Impact(0.5f, 120 * rng.Range(0.92f, 1.08f), 45, 0.1f, 600, 0.08f, 2f, 1.3f);
                Add("stomp" + V(v), b, 0.9f, 1.2f);
            }
            {
                var b = Buffer(2.0f); var f = new SVF();
                double p1 = 0, p2 = 0, p3 = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR, u = t / 2f;
                    float fr = Mathf.Lerp(200, 55, u);
                    p1 += fr / SR; p2 += fr * 1.498f / SR; p3 += fr * 0.503f / SR;
                    f.Run(Saw(p1) + Saw(p2) + Saw(p3) + rng.Noise() * 0.7f, 1300 - 800 * u, 1.3f);
                    b[i] = SoftClip(f.Low * 2.5f) * Adsr(t, 1.4f, 0.08f, 0.3f, 0.8f, 0.6f);
                }
                Synth.Add(b, Get("boom"), 0, 0.6f);
                Add("roar", b, 0.9f, 1.26f);
            }
            // 三相交流：50Hz の太いうなり（3つの位相）
            {
                var b = Buffer(2.4f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float s = Sin(t * 50) + Sin(t * 50 + 1f / 3) + Sin(t * 50 + 2f / 3);
                    s = s * 0.3f + Saw(t * 100) * 0.4f + Saw(t * 150) * 0.25f + Sq(t * 50) * 0.3f;
                    f.Run(s, 700 + 500 * Sin(t * 3), 1.6f);
                    b[i] = SoftClip(f.Low * 2) * Adsr(t, 2.0f, 0.2f, 0.2f, 0.9f, 0.4f);
                }
                Add("hum3", b, 0.7f);
            }
            // トランス（変圧器）：ブーンという唸り
            {
                var b = Buffer(1.2f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    f.Run(Sq(t * 100) * 0.5f + Saw(t * 200) * 0.3f + rng.Noise() * 0.1f, 600, 2);
                    b[i] = f.Low * Adsr(t, 0.9f, 0.05f, 0.1f, 0.8f, 0.3f);
                }
                Add("trans", b, 0.6f);
            }
            {
                var b = Buffer(1.0f); double ph = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    ph += (90 + 260 * t) / SR;
                    b[i] = (Saw(ph) * 0.5f + Sin(ph) * 0.5f) * t;
                }
                var o = new float[b.Length]; var f = new SVF();
                for (int i = 0; i < b.Length; i++) { f.Run(b[i], 1200, 1.5f); o[i] = f.Low; }
                Add("laserCharge", o, 0.55f);
            }
            {
                var b = Buffer(2.2f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float s = Saw(t * 110) + Saw(t * 111) * 0.7f + Sq(t * 55) * 0.5f + rng.Noise() * 0.4f;
                    f.Run(s, 1500 + 400 * Sin(t * 7), 2);
                    b[i] = SoftClip(f.Low * 1.5f) * Adsr(t, 1.9f, 0.02f, 0.1f, 0.85f, 0.3f);
                }
                Add("laser", b, 0.6f);
            }

            // ================= UI・演出 =================
            // やる気の玉を拾う：ふわっとした風（小さく）
            Add("orb", Swish(0.25f, 500, 1200, 3f), 0.3f);
            // 奥義の準備完了：あたたかい和音のふくらみ
            {
                var b = Buffer(1.3f); var f = new SVF();
                float[] ch = { 50, 57, 62, 64, 69 };
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR; float s = 0;
                    foreach (var n in ch) s += Saw(t * Mtof(n)) + Saw(t * Mtof(n) * 1.005f);
                    f.Run(s * 0.15f, 400 + 1400 * Mathf.Sin(Mathf.Min(1, t / 1.3f) * Mathf.PI), 1f);
                    b[i] = f.Low * Adsr(t, 0.7f, 0.25f, 0.2f, 0.8f, 0.5f);
                }
                Add("ready", b, 0.55f, 1.17f);
            }
            // UI：木をたたくような低い「トッ」
            {
                var b = Buffer(0.1f); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    f.Run(rng.Noise(), 900, 2);
                    b[i] = Sin(t * 330) * Env(t, 0.001f, 0.02f) + f.Band * Env(t, 0.001f, 0.01f);
                }
                Add("tick", b, 0.3f, 0.4f);
            }
            {
                var b = Impact(0.6f, 120, 55, 0.12f, 800, 0.06f, 1.6f);
                Synth.Add(b, Get("ready"), 0, 0.4f);
                Add("confirm", b, 0.6f);
            }
            // 大きな文字が出るとき：ドゥン
            {
                var b = Swish(0.35f, 2000, 400, 1.2f);
                Synth.Add(b, Impact(0.6f, 100, 42, 0.15f, 600, 0.05f, 1.8f), (int)(0.08f * SR), 0.9f);
                Add("pop", b, 0.6f, 1.08f);
            }
            {
                var b = Buffer(0.35f); var lp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    lp.Run(rng.Noise(), 1200, 1);
                    b[i] = Sin(t * Mathf.Lerp(110, 50, t * 3)) * Env(t, 0.001f, 0.08f) * 1.3f + lp.Low * Env(t, 0.001f, 0.05f) * 2.5f;
                }
                Add("stamp", b, 0.9f, 0.90f);
            }
            // ブレイク：ガラスが割れる（高い成分は控えめ）＋大きな地響き
            {
                var b = Buffer(1.6f); var hp = new SVF(); var bp = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    float n = rng.Noise();
                    hp.Run(n, 3500, 0.8f); bp.Run(n * (rng.Next() < 0.05f ? 3 : 0.4f), 2000, 4);
                    b[i] = hp.High * Env(t, 0.001f, 0.08f) * 0.6f + bp.Band * Env(t, 0.01f, 0.25f) * 0.5f;
                }
                Synth.Add(b, Get("boom"), 0, 1f);
                Add("break", b, 0.9f, 1.35f);
            }
            // ジャスト回避：吸い込むような逆再生＋深い「ブォン」
            {
                var b = Buffer(1.1f); var hp = new SVF(); var f = new SVF();
                for (int i = 0; i < b.Length; i++)
                {
                    float t = i / (float)SR;
                    hp.Run(rng.Noise(), 3000, 0.7f);
                    float sw = t < 0.3f ? (t / 0.3f) * (t / 0.3f) : 0;
                    f.Run(Saw(t * 70) + Saw(t * 70.8f) + Sin(t * 35) * 2, 500, 1.5f);
                    b[i] = hp.High * sw * 0.6f + (t > 0.3f ? f.Low * Env(t - 0.3f, 0.01f, 0.35f) * 1.2f : 0);
                }
                Add("perfect", b, 0.75f, 1.26f);
            }
            // 勝利ファンファーレ（金管っぽく）
            {
                var b = Buffer(3.6f); var f = new SVF();
                float[,] mel = { { 0, 0.5f, 62 }, { 0.5f, 0.5f, 67 }, { 1, 0.5f, 71 }, { 1.5f, 0.5f, 74 }, { 2, 1.5f, 79 }, { 3.5f, 0.5f, 78 }, { 4, 2.5f, 81 } };
                float[] chord = { 55, 62, 67, 71, 74 };
                for (int n = 0; n < mel.GetLength(0); n++)
                {
                    float st = mel[n, 0] * 0.5f, len = mel[n, 1] * 0.5f, fr = Mtof(mel[n, 2]);
                    int off = (int)(st * SR);
                    for (int i = 0; i + off < b.Length && i < (len + 0.4f) * SR; i++)
                    {
                        float t = i / (float)SR;
                        float vib = 1 + 0.004f * Sin(t * 6) * Mathf.Clamp01(t * 4);
                        b[i + off] += (Saw(t * fr * vib) * 0.6f + Saw(t * fr * vib * 1.003f) * 0.4f) * Adsr(t, len, 0.03f, 0.1f, 0.8f, 0.3f);
                    }
                }
                int co = (int)(1.0f * SR);
                for (int i = 0; i + co < b.Length; i++)
                {
                    float t = i / (float)SR; float s = 0;
                    foreach (var c in chord) s += Saw(t * Mtof(c)) + Saw(t * Mtof(c) * 1.004f);
                    b[i + co] += s * 0.08f * Adsr(t, 1.8f, 0.05f, 0.4f, 0.7f, 0.6f);
                }
                var o = new float[b.Length];
                for (int i = 0; i < b.Length; i++) { f.Run(b[i], 2200, 0.9f); o[i] = f.Low; }
                Synth.Add(o, Get("boom"), 0, 0.5f);
                Add("fanfare", o, 0.9f, 1.35f);
            }
            {
                var b = Buffer(3f);
                float[] ns = { 57, 55, 53, 52 };
                for (int k = 0; k < ns.Length; k++)
                {
                    float fr = Mtof(ns[k]); int off = (int)(k * 0.5f * SR);
                    for (int i = 0; i + off < b.Length; i++)
                    {
                        float t = i / (float)SR;
                        b[i + off] += (Tri(t * fr) + Sin(t * fr * 0.5f) * 0.5f) * Adsr(t, k == 3 ? 1.4f : 0.45f, 0.02f, 0.1f, 0.7f, 0.5f);
                    }
                }
                Add("defeat", b, 0.6f, 1.35f);
            }
            return data;
        }
    }
}
