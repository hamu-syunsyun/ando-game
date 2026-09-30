using System.Collections.Generic;
using UnityEngine;

namespace AndoBoss
{
    // 空の見た目を変える窓口（第2形態の雷雲・稲光）
    public static class Sky
    {
        public static Material Mat;
        static float storm, stormTarget, flash;
        public static Light Sun;
        static Color sunDay = new Color(1f, 0.95f, 0.84f), sunStorm = new Color(0.75f, 0.6f, 1f);
        static Color fogDay = new Color(0.8f, 0.9f, 1f), fogStorm = new Color(0.32f, 0.2f, 0.42f);

        public static void SetStorm(float s) => stormTarget = s;
        public static void SnapStorm(float s) { storm = stormTarget = s; }
        public static void Flash(float a) => flash = Mathf.Max(flash, a);

        public static void Update(float dt)
        {
            storm = Mathf.MoveTowards(storm, stormTarget, dt * 0.5f);
            flash = Mathf.MoveTowards(flash, 0, dt * 3f);
            if (Mat)
            {
                Mat.SetFloat("_Storm", storm);
                Mat.SetFloat("_Flash", flash);
            }
            if (Sun)
            {
                Sun.color = Color.Lerp(sunDay, sunStorm, storm) * (1 + flash * 0.8f);
                Sun.intensity = Mathf.Lerp(1.0f, 0.75f, storm) + flash * 0.3f;
            }
            RenderSettings.fogColor = Color.Lerp(fogDay, fogStorm, storm);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.55f, 0.63f, 0.8f), new Color(0.4f, 0.3f, 0.58f), storm) * (1 + flash * 0.5f);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.6f, 0.66f, 0.62f), new Color(0.36f, 0.26f, 0.44f), storm);
        }
    }

    // アリーナと周りの景色を作る
    public static class World
    {
        public const float ArenaR = 20f;
        static readonly List<(Transform t, float phase, float baseY)> bobbers = new List<(Transform, float, float)>();
        static readonly List<Material> ledMats = new List<Material>();
        static Material edgeMat, wallMat;
        static float time;

        public static void Build(Transform root)
        {
            // ---- 空・光・霧 ----
            var skySh = Shader.Find("AndoBoss/Sky");
            if (skySh != null)
            {
                Sky.Mat = new Material(skySh);
                RenderSettings.skybox = Sky.Mat;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientGroundColor = new Color(0.45f, 0.5f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 60;
            RenderSettings.fogEndDistance = 210;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(52, -35, 0);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            Sky.Sun = sun;
            if (Sky.Mat) Sky.Mat.SetVector("_SunDir", -sunGo.transform.forward);
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 70;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 4;
            QualitySettings.vSyncCount = 1;
            Sky.SnapStorm(0);
            Sky.Update(0);

            var disc = Mat.Disc(96);

            // ---- 地面 ----
            var grassTex = Mat.MakeTex(256, (x, y) =>
            {
                float n = Mathf.PerlinNoise(x * 12, y * 12) * 0.6f + Mathf.PerlinNoise(x * 40, y * 40) * 0.4f;
                return Color.Lerp(new Color(0.42f, 0.72f, 0.33f), new Color(0.6f, 0.86f, 0.42f), n);
            });
            grassTex.wrapMode = TextureWrapMode.Repeat;
            var groundMat = Mat.Toon(Color.white, 0, null, grassTex);
            groundMat.SetTextureScale("_MainTex", new Vector2(30, 30));
            var ground = Mat.Part(root, disc, groundMat, new Vector3(0, -0.6f, 0), new Vector3(300, 1, 300));
            ground.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---- アリーナの台座と床（基板柄） ----
            Mat.Part(root, Mat.Frustum(ArenaR + 2.6f, ArenaR + 1.3f, 1.2f, 96), Mat.Toon(new Color(0.78f, 0.7f, 0.56f), 0), new Vector3(0, -0.6f, 0), Vector3.one);
            Mat.Part(root, Mat.Frustum(ArenaR + 1.35f, ArenaR + 1.35f, 0.3f, 96), Mat.Toon(new Color(0.36f, 0.33f, 0.42f)), new Vector3(0, -0.2f, 0), Vector3.one);
            var floorMat = Mat.Toon(Color.white, 0, null, FloorTexture());
            var floor = Mat.Part(root, disc, floorMat, new Vector3(0, 0.02f, 0), new Vector3(ArenaR + 1.2f, 1, ArenaR + 1.2f));
            floor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 床のふちで光る線と、うっすら見える結界の壁
            edgeMat = Mat.Fx(Mat.Electro, Mat.RingTex, true, 2f);
            // RingTex の輪は半径の 0.86 倍の位置にあるので、輪がアリーナのふち（半径20.3）に来るように広げる
            Mat.Part(root, disc, edgeMat, new Vector3(0, 0.05f, 0), Vector3.one * ((ArenaR + 0.3f) / 0.86f), default, false);
            wallMat = Mat.Fx(new Color(0.7f, 0.55f, 1f, 0.22f), Mat.WallTex, true, 1.5f);
            Mat.Part(root, Mat.Frustum(1, 1, 1, 96, false, true), wallMat, new Vector3(0, 1.5f, 0), new Vector3(ArenaR + 1.3f, 3f, ArenaR + 1.3f), default, false);

            // ---- 巨大な電子部品の柱 ----
            Props.Clear();
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2 + 0.2f;
                var p = new GameObject("pillar").transform;
                p.SetParent(root, false);
                p.localPosition = new Vector3(Mathf.Cos(a) * (ArenaR + 6), 0, Mathf.Sin(a) * (ArenaR + 6));
                p.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                Props.Add(new Vector3(p.localPosition.x, 0, p.localPosition.z));
                switch (i % 3)
                {
                    case 0: Resistor(p); break;
                    case 1: Capacitor(p); break;
                    default: Led(p, new[] { new Color(1f, 0.3f, 0.3f), new Color(0.3f, 1f, 0.5f), new Color(0.4f, 0.7f, 1f) }[i / 3 % 3]); break;
                }
            }

            // ---- 木（まとめて描画） ----
            var trunks = new List<CombineInstance>();
            var leaves = new List<CombineInstance>[3] { new List<CombineInstance>(), new List<CombineInstance>(), new List<CombineInstance>() };
            var trunkMesh = Mat.Frustum(0.45f, 0.3f, 2.4f, 7);
            for (int i = 0; i < 70; i++)
            {
                float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(34f, 110f);
                float s = Random.Range(0.9f, 2f);
                var basePos = new Vector3(Mathf.Cos(a) * r, -0.6f, Mathf.Sin(a) * r);
                trunks.Add(Mat.CI(trunkMesh, basePos + Vector3.up * 1.2f * s, Quaternion.identity, Vector3.one * s));
                int k = Random.Range(0, 3);
                leaves[k].Add(Mat.CI(Mat.Sphere, basePos + Vector3.up * 3.4f * s, Quaternion.Euler(0, Random.Range(0, 360f), 0), new Vector3(3.4f, Random.Range(3.4f, 4.6f), 3.4f) * s));
                leaves[k].Add(Mat.CI(Mat.Sphere, basePos + new Vector3(0.9f, 2.7f, 0.4f) * s, Quaternion.identity, Vector3.one * 2.2f * s));
            }
            Mat.Combine("trunks", trunks, Mat.Toon(new Color(0.55f, 0.36f, 0.24f))).transform.SetParent(root, false);
            Color[] leafC = { new Color(0.36f, 0.68f, 0.31f), new Color(0.44f, 0.75f, 0.35f), new Color(0.31f, 0.62f, 0.29f) };
            for (int k = 0; k < 3; k++) Mat.Combine("leaves", leaves[k], Mat.Toon(leafC[k], 0)).transform.SetParent(root, false);

            // ---- 草むら（小さい円すいをたくさん） ----
            var grass = new List<CombineInstance>();
            var blade = Mat.Frustum(0.12f, 0.0f, 0.7f, 4, false);
            for (int i = 0; i < 900; i++)
            {
                float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(ArenaR + 3.5f, 60f);
                var bp = new Vector3(Mathf.Cos(a) * r, -0.35f, Mathf.Sin(a) * r);
                for (int k = 0; k < 3; k++)
                    grass.Add(Mat.CI(blade, bp + new Vector3(Random.Range(-0.3f, 0.3f), 0, Random.Range(-0.3f, 0.3f)),
                        Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360), Random.Range(-20, 20)), Vector3.one * Random.Range(0.6f, 1.3f)));
            }
            Mat.Combine("grass", grass, Mat.Toon(new Color(0.5f, 0.8f, 0.36f)), false).transform.SetParent(root, false);

            // ---- 遠くの山 ----
            var mtn = new List<CombineInstance>();
            var cone = Mat.Frustum(1, 0.12f, 1, 9);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2 + Random.Range(-0.1f, 0.1f);
                float r = Random.Range(150, 190), h = Random.Range(40, 80);
                mtn.Add(Mat.CI(cone, new Vector3(Mathf.Cos(a) * r, h / 2 - 2, Mathf.Sin(a) * r), Quaternion.Euler(0, Random.Range(0, 90), 0), new Vector3(Random.Range(35, 55), h, Random.Range(35, 55))));
            }
            Mat.Combine("mountains", mtn, Mat.Toon(new Color(0.5f, 0.62f, 0.78f)), false).transform.SetParent(root, false);

            // ---- 浮き島 ----
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2 + 0.5f, r = Random.Range(42, 70);
                var isl = new GameObject("island").transform;
                isl.SetParent(root, false);
                float y = Random.Range(12, 26);
                isl.localPosition = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                float s = Random.Range(0.8f, 1.6f);
                isl.localScale = Vector3.one * s;
                Mat.Part(isl, Mat.Frustum(0.2f, 4f, 5f, 8), Mat.ToonShared(new Color(0.62f, 0.5f, 0.4f)), new Vector3(0, -2.5f, 0), Vector3.one);
                Mat.Part(isl, Mat.Frustum(4.1f, 4.1f, 0.6f, 8), Mat.ToonShared(new Color(0.46f, 0.76f, 0.36f)), new Vector3(0, 0.2f, 0), Vector3.one);
                Mat.Part(isl, trunkMesh, Mat.ToonShared(new Color(0.55f, 0.36f, 0.24f)), new Vector3(1, 1.5f, 0.5f), Vector3.one * 0.8f);
                Mat.Part(isl, Mat.Sphere, Mat.ToonShared(leafC[i % 3]), new Vector3(1, 3.3f, 0.5f), Vector3.one * 2.6f);
                // 島から落ちる小さな光（滝のかわり）
                bobbers.Add((isl, Random.Range(0, 10f), y));
            }

            // ---- 空に浮かぶ光の粒 ----
            var motes = new GameObject("motes");
            motes.transform.SetParent(root, false);
            var ps = motes.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.startLifetime = 6; main.startSpeed = 0.3f; main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.maxParticles = 300; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(0.8f, 0.7f, 1f));
            var em = ps.emission; em.rateOverTime = 45;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(70, 5, 70);
            sh.position = new Vector3(0, 2.5f, 0);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.5f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var pr = motes.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Mat.Fx(Color.white, Mat.Glow, true, 2.5f);
            ps.Play();
        }

        public static void Update(float dt, float phase2)
        {
            time += dt;
            foreach (var b in bobbers)
                b.t.localPosition = new Vector3(b.t.localPosition.x, b.baseY + Mathf.Sin(time * 0.6f + b.phase) * 0.8f, b.t.localPosition.z);
            for (int i = 0; i < ledMats.Count; i++)
            {
                var c = ledMats[i].GetColor("_Color");
                float a = 0.55f + 0.45f * Mathf.Sin(time * (2 + phase2 * 6) + i * 1.3f);
                ledMats[i].SetColor("_Emission", new Color(c.r, c.g, c.b, a * (0.6f + phase2 * 0.4f)));
            }
            if (edgeMat)
            {
                var ec = Color.Lerp(Mat.Electro, new Color(1f, 0.35f, 0.7f), phase2);
                ec.a = 0.7f + 0.3f * Mathf.Sin(time * 3);
                edgeMat.SetColor("_Color", ec);
                wallMat.SetColor("_Color", new Color(ec.r, ec.g, ec.b, 0.08f + 0.08f * phase2 + 0.03f * Mathf.Sin(time * 2)));
                wallMat.SetTextureOffset("_MainTex", new Vector2(time * 0.02f, 0));
            }
        }

        // カメラが柱にめりこまないように使う（柱は半径 PropR・高さ PropH の円柱とみなす）
        public static readonly List<Vector3> Props = new List<Vector3>();
        const float PropR = 2.6f, PropH = 11.5f;

        // from（見ている点）から to（カメラを置きたい所）へ線を引き、柱に当たったら手前で止める
        public static Vector3 CamClip(Vector3 from, Vector3 to)
        {
            var d = to - from;
            float best = 1f;
            foreach (var c in Props)
            {
                // XZ 平面での、線分と円の交わり
                float ox = from.x - c.x, oz = from.z - c.z;
                float a = d.x * d.x + d.z * d.z;
                if (a < 1e-6f) continue;
                float b = 2 * (ox * d.x + oz * d.z);
                float cc = ox * ox + oz * oz - PropR * PropR;
                if (cc < 0) continue; // 見ている点が柱の中なら何もしない
                float disc = b * b - 4 * a * cc;
                if (disc < 0) continue;
                float t = (-b - Mathf.Sqrt(disc)) / (2 * a);
                if (t < 0 || t > best) continue;
                float y = from.y + d.y * t;
                if (y > PropH) continue;
                best = t;
            }
            if (best >= 1f) return to;
            // 当たったところより少し手前に置く
            return from + d * Mathf.Max(0.15f, best - 0.4f / Mathf.Max(0.01f, d.magnitude));
        }

        // ---- 部品の柱 ----
        static void Resistor(Transform p)
        {
            var body = Mat.ToonShared(new Color(0.91f, 0.83f, 0.64f), 0.05f);
            Mat.Part(p, Mat.Frustum(1.2f, 1.2f, 5.5f, 20), body, new Vector3(0, 6, 0), Vector3.one);
            Mat.Part(p, Mat.Sphere, body, new Vector3(0, 8.9f, 0), new Vector3(2.5f, 1.4f, 2.5f));
            Mat.Part(p, Mat.Sphere, body, new Vector3(0, 3.1f, 0), new Vector3(2.5f, 1.4f, 2.5f));
            Color[] bands = { new Color(0.55f, 0.3f, 0.15f), Color.black, new Color(0.9f, 0.2f, 0.2f), new Color(0.85f, 0.7f, 0.2f) };
            for (int i = 0; i < 4; i++)
                Mat.Part(p, Mat.Frustum(1.26f, 1.26f, 0.45f, 20), Mat.ToonShared(bands[i]), new Vector3(0, 4.3f + i * 1.1f, 0), Vector3.one);
            var lead = Mat.ToonShared(new Color(0.75f, 0.78f, 0.82f), 0.03f);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 3.1f, 8), lead, new Vector3(0, 1.3f, 0), Vector3.one);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 2.4f, 8), lead, new Vector3(0, 10.6f, 0), Vector3.one);
        }
        static void Capacitor(Transform p)
        {
            Mat.Part(p, Mat.Frustum(1.8f, 1.8f, 7, 24), Mat.ToonShared(new Color(0.23f, 0.42f, 0.8f), 0.05f), new Vector3(0, 4.5f, 0), Vector3.one);
            Mat.Part(p, Mat.Frustum(1.82f, 1.82f, 7.02f, 24), Mat.ToonShared(new Color(0.8f, 0.85f, 0.95f)), new Vector3(0, 4.5f, 0), new Vector3(0.35f, 1, 1.05f), new Vector3(0, 90, 0));
            Mat.Part(p, Mat.Frustum(1.7f, 1.7f, 0.3f, 24), Mat.ToonShared(new Color(0.75f, 0.78f, 0.82f)), new Vector3(0, 8.1f, 0), Vector3.one);
            var lead = Mat.ToonShared(new Color(0.75f, 0.78f, 0.82f), 0.03f);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 1.2f, 8), lead, new Vector3(-0.7f, 0.5f, 0), Vector3.one);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 1.2f, 8), lead, new Vector3(0.7f, 0.5f, 0), Vector3.one);
        }
        static void Led(Transform p, Color c)
        {
            var m = Mat.Toon(c, 0.05f, new Color(c.r, c.g, c.b, 0.6f));
            ledMats.Add(m);
            Mat.Part(p, Mat.Frustum(1.5f, 1.5f, 3.5f, 24), m, new Vector3(0, 5.2f, 0), Vector3.one);
            Mat.Part(p, Mat.Sphere, m, new Vector3(0, 6.95f, 0), Vector3.one * 3f);
            Mat.Part(p, Mat.Frustum(1.8f, 1.8f, 0.4f, 24), m, new Vector3(0, 3.5f, 0), Vector3.one);
            var lead = Mat.ToonShared(new Color(0.75f, 0.78f, 0.82f), 0.03f);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 3.4f, 8), lead, new Vector3(-0.6f, 1.6f, 0), Vector3.one);
            Mat.Part(p, Mat.Frustum(0.14f, 0.14f, 2.6f, 8), lead, new Vector3(0.6f, 2f, 0), Vector3.one);
            var halo = Mat.Part(p, Mat.Quad, Mat.Fx(new Color(c.r, c.g, c.b, 0.35f), Mat.SoftGlow, true, 1.2f), new Vector3(0, 6.5f, 0), Vector3.one * 7, default, false);
            halo.AddComponent<Billboard>();
        }

        // 基板っぽい床のテクスチャ
        static Texture2D FloorTexture()
        {
            const int S = 1024;
            var px = new Color32[S * S];
            var baseC = new Color32(214, 204, 184, 255);
            for (int i = 0; i < px.Length; i++) px[i] = baseC;
            var trace = new Color32(186, 168, 128, 255);
            var pad = new Color32(168, 146, 100, 255);
            var ringC = new Color32(180, 160, 118, 255);
            float m = S / 2f;

            void Dot(float x, float y, float r, Color32 c)
            {
                for (int yy = (int)(y - r); yy <= (int)(y + r); yy++)
                    for (int xx = (int)(x - r); xx <= (int)(x + r); xx++)
                        if (xx >= 0 && yy >= 0 && xx < S && yy < S && (xx - x) * (xx - x) + (yy - y) * (yy - y) <= r * r) px[yy * S + xx] = c;
            }
            void Line(float x0, float y0, float x1, float y1, float w, Color32 c)
            {
                float len = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
                for (int i = 0; i <= len; i += 2) Dot(Mathf.Lerp(x0, x1, i / len), Mathf.Lerp(y0, y1, i / len), w, c);
            }
            void Circle(float r, float w, Color32 c, float a0 = 0, float a1 = Mathf.PI * 2)
            {
                int n = (int)(r * (a1 - a0));
                for (int i = 0; i <= n; i += 2) { float a = Mathf.Lerp(a0, a1, i / (float)n); Dot(m + Mathf.Cos(a) * r, m + Mathf.Sin(a) * r, w, c); }
            }

            Circle(140, 6, ringC); Circle(470, 4, ringC); Circle(500, 5, ringC);
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2;
                float r = 160 + (i % 3) * 40;
                float x = m + Mathf.Cos(a) * r, y = m + Mathf.Sin(a) * r;
                r += 120 + (i % 4) * 30;
                float x2 = m + Mathf.Cos(a) * r, y2 = m + Mathf.Sin(a) * r;
                if (i % 2 == 1) { Line(x, y, x2, y, 5, trace); Line(x2, y, x2, y2, 5, trace); }
                else { Line(x, y, x, y2, 5, trace); Line(x, y2, x2, y2, 5, trace); }
                Dot(x2, y2, 13, pad);
                Dot(x2, y2, 5, baseC);
            }
            // 真ん中に大きな Ω
            var om = new Color32(165, 140, 95, 255);
            float fa = -0.15f * Mathf.PI, fb = 1.15f * Mathf.PI;
            Circle(78, 11, om, fa, fb);
            Line(m + Mathf.Cos(fa) * 78, m + Mathf.Sin(fa) * 78, m + Mathf.Cos(fa) * 78 + 40, m + Mathf.Sin(fa) * 78, 11, om);
            Line(m + Mathf.Cos(fb) * 78, m + Mathf.Sin(fb) * 78, m + Mathf.Cos(fb) * 78 - 40, m + Mathf.Sin(fb) * 78, 11, om);

            var t = new Texture2D(S, S, TextureFormat.RGBA32, true);
            t.SetPixels32(px);
            t.Apply(true);
            t.anisoLevel = 4;
            return t;
        }
    }

    // いつもカメラの方を向く板
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (c) transform.rotation = c.transform.rotation;
        }
    }
}
