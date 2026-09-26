using System.Collections.Generic;
using UnityEngine;

namespace AndoBoss
{
    // マテリアル・メッシュ・テクスチャを作る道具箱。外部の画像や3Dモデルは使わず、全部ここで作る
    public static class Mat
    {
        static Shader toonSh, fxSh, teleSh;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Mesh Cube, Sphere, Cylinder, Quad;
        public static Texture2D White, Glow, SoftGlow, Streak, RingTex, WallTex, Star, Circle;
        public static Sprite WhiteSprite, CircleSprite, RingSprite, GlowSprite, RoundSprite;

        public static readonly Color Electro = new Color(0.71f, 0.49f, 1f);
        public static readonly Color ElectroLight = new Color(0.88f, 0.78f, 1f);
        public static readonly Color Danger = new Color(1f, 0.3f, 0.28f);
        public static readonly Color Gold = new Color(1f, 0.84f, 0.3f);

        public static void Init()
        {
            if (toonSh != null) return;
            toonSh = Shader.Find("AndoBoss/Toon");
            fxSh = Shader.Find("AndoBoss/FX");
            teleSh = Shader.Find("AndoBoss/Telegraph");
            if (toonSh == null) toonSh = Shader.Find("Standard");
            if (fxSh == null) fxSh = Shader.Find("Sprites/Default");
            if (teleSh == null) teleSh = fxSh;

            Cube = Prim(PrimitiveType.Cube);
            Sphere = Prim(PrimitiveType.Sphere);
            Cylinder = Frustum(0.5f, 0.5f, 1f, 20);
            Quad = Prim(PrimitiveType.Quad);

            White = MakeTex(4, (x, y) => Color.white);
            Glow = MakeTex(64, (x, y) => { float d = Dist(x, y); float a = Mathf.Clamp01(1 - d); return new Color(1, 1, 1, a * a * a + Mathf.Clamp01(1 - d * 3) * 0.6f); });
            SoftGlow = MakeTex(64, (x, y) => { float a = Mathf.Clamp01(1 - Dist(x, y)); return new Color(1, 1, 1, a * a); });
            Star = MakeTex(64, (x, y) =>
            {
                float u = Mathf.Abs(x * 2 - 1), v = Mathf.Abs(y * 2 - 1);
                float a = Mathf.Clamp01(1 - (u * 8 * v + Mathf.Min(u, v) * 3)) + Mathf.Clamp01(1 - Dist(x, y) * 2.5f);
                return new Color(1, 1, 1, Mathf.Clamp01(a));
            });
            Streak = MakeTex(64, (x, y) =>
            {
                float u = Mathf.Abs(x * 2 - 1), v = Mathf.Abs(y * 2 - 1);
                float a = Mathf.Clamp01(1 - u * u) * Mathf.Clamp01(1 - v) ;
                return new Color(1, 1, 1, a * a);
            });
            RingTex = MakeTex(128, (x, y) =>
            {
                float d = Dist(x, y);
                float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.86f) / 0.12f);
                return new Color(1, 1, 1, a * a);
            });
            Circle = MakeTex(128, (x, y) => new Color(1, 1, 1, Mathf.Clamp01((1 - Dist(x, y)) * 40)));
            WallTex = MakeTex(64, (x, y) => new Color(1, 1, 1, Mathf.Pow(1 - y, 1.6f) * (0.7f + 0.3f * Mathf.Sin(x * Mathf.PI * 16))));
            WallTex.wrapMode = TextureWrapMode.Repeat;

            WhiteSprite = Sprite.Create(White, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100);
            CircleSprite = Sprite.Create(Circle, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100);
            RingSprite = Sprite.Create(MakeTex(128, (x, y) =>
            {
                float d = Dist(x, y);
                return new Color(1, 1, 1, Mathf.Clamp01((1 - d) * 40) * Mathf.Clamp01((d - 0.8f) * 40));
            }), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100);
            GlowSprite = Sprite.Create(SoftGlow, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100);
            var round = MakeTex(32, (x, y) =>
            {
                float px = Mathf.Max(0, Mathf.Abs(x * 32 - 16) - 8), py = Mathf.Max(0, Mathf.Abs(y * 32 - 16) - 8);
                return new Color(1, 1, 1, Mathf.Clamp01(8.5f - Mathf.Sqrt(px * px + py * py)));
            });
            RoundSprite = Sprite.Create(round, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        }

        static float Dist(float x, float y) => Mathf.Sqrt((x * 2 - 1) * (x * 2 - 1) + (y * 2 - 1) * (y * 2 - 1));

        public static Texture2D MakeTex(int size, System.Func<float, float, Color> f)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = f((x + 0.5f) / size, (y + 0.5f) / size);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        static Mesh Prim(PrimitiveType t)
        {
            var go = GameObject.CreatePrimitive(t);
            var m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            return m;
        }

        // ---------- マテリアル ----------
        public static Material Toon(Color c, float outline = 0f, Color? emission = null, Texture tex = null)
        {
            var m = new Material(toonSh);
            m.SetColor("_Color", c);
            m.color = c;
            m.SetFloat("_OutlineWidth", outline);
            if (emission.HasValue) m.SetColor("_Emission", emission.Value);
            if (tex != null) m.SetTexture("_MainTex", tex);
            // 影の色は元の色より少し青紫寄りにすると、アニメ塗りっぽくなる
            Color.RGBToHSV(c, out float h, out float s, out float v);
            var shade = Color.Lerp(new Color(0.55f, 0.52f, 0.75f), Color.HSVToRGB(h, Mathf.Min(1, s * 1.2f + 0.05f), v * 0.8f), 0.35f);
            m.SetColor("_ShadeColor", Color.Lerp(shade, Color.white, 0.25f));
            return m;
        }

        public static Material ToonShared(Color c, float outline = 0f)
        {
            string key = "t" + ColorUtility.ToHtmlStringRGBA(c) + outline;
            if (!cache.TryGetValue(key, out var m)) cache[key] = m = Toon(c, outline);
            return m;
        }

        // エフェクトの明るさ全体の倍率（まぶしすぎたので下げてある）
        public static float FxGain = 0.55f;

        public static Material Fx(Color c, Texture tex = null, bool additive = true, float intensity = 1f, bool cullOff = true)
        {
            if (additive) intensity *= FxGain;
            var m = new Material(fxSh);
            m.SetColor("_Color", c);
            m.mainTexture = tex != null ? tex : White;
            m.SetFloat("_Intensity", intensity);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_Cull", cullOff ? 0 : 2);
            return m;
        }

        public static Material FxShared(Color c, Texture tex = null, bool additive = true, float intensity = 1f)
        {
            string key = "f" + ColorUtility.ToHtmlStringRGBA(c) + (tex != null ? tex.GetInstanceID() : 0) + additive + intensity;
            if (!cache.TryGetValue(key, out var m)) cache[key] = m = Fx(c, tex, additive, intensity);
            return m;
        }

        public static Material Telegraph(Color c, bool rect)
        {
            var m = new Material(teleSh);
            m.SetColor("_Color", c);
            m.SetFloat("_Shape", rect ? 1 : 0);
            m.SetFloat("_Fill", 0);
            return m;
        }

        // ---------- 形を置く ----------
        public static GameObject Part(Transform parent, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 euler = default, bool shadow = true)
        {
            var go = new GameObject(mesh != null ? mesh.name : "part");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadow;
            return go;
        }

        public static Transform Pivot(Transform parent, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            return t;
        }

        // ---------- 手作りメッシュ ----------
        // 上下の半径がちがう円柱（原点は中心、高さ h）
        public static Mesh Frustum(float rBottom, float rTop, float h, int seg, bool caps = true, bool open = false)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            float slope = (rBottom - rTop) / h;
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                var nn = new Vector3(c, slope, s).normalized;
                v.Add(new Vector3(c * rBottom, -h / 2, s * rBottom)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(c * rTop, h / 2, s * rTop)); n.Add(nn); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++)
            {
                int k = i * 2;
                tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
            }
            if (open)
            {
                // 内側からも見えるように裏面を足す
                int b = v.Count;
                for (int i = 0; i < b; i++) { v.Add(v[i]); n.Add(-n[i]); uv.Add(uv[i]); }
                for (int i = 0; i < seg; i++)
                {
                    int k = b + i * 2;
                    tri.AddRange(new[] { k, k + 2, k + 1, k + 1, k + 2, k + 3 });
                }
            }
            if (caps && !open)
            {
                for (int side = 0; side < 2; side++)
                {
                    float y = side == 0 ? -h / 2 : h / 2;
                    float r = side == 0 ? rBottom : rTop;
                    var nn = side == 0 ? Vector3.down : Vector3.up;
                    int center = v.Count;
                    v.Add(new Vector3(0, y, 0)); n.Add(nn); uv.Add(new Vector2(0.5f, 0.5f));
                    for (int i = 0; i <= seg; i++)
                    {
                        float a = i / (float)seg * Mathf.PI * 2;
                        v.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r)); n.Add(nn);
                        uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
                    }
                    for (int i = 0; i < seg; i++)
                    {
                        if (side == 0) tri.AddRange(new[] { center, center + 1 + i, center + 2 + i });
                        else tri.AddRange(new[] { center, center + 2 + i, center + 1 + i });
                    }
                }
            }
            var m = new Mesh { name = "frustum" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        public static Mesh Torus(float R, float r, int seg = 32, int seg2 = 10)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j <= seg2; j++)
                {
                    float b = j / (float)seg2 * Mathf.PI * 2;
                    var nn = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    v.Add(c * R + nn * r); n.Add(nn); uv.Add(new Vector2(i / (float)seg, j / (float)seg2));
                }
            }
            int w = seg2 + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < seg2; j++)
                {
                    int a0 = i * w + j, a1 = (i + 1) * w + j;
                    tri.AddRange(new[] { a0, a1, a0 + 1, a1, a1 + 1, a0 + 1 });
                }
            var m = new Mesh { name = "torus" };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        // XZ 平面の円盤（半径 1、UV は四角に合わせる）
        public static Mesh Disc(int seg = 64)
        {
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2;
                v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < seg; i++) tri.AddRange(new[] { 0, i + 2, i + 1 });
            var m = new Mesh { name = "disc" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            m.normals = n;
            m.RecalculateBounds();
            return m;
        }

        // XZ 平面の四角（1x1、中心が原点、UV の v は +Z 方向）
        public static Mesh FlatQuad(float zFrom = -0.5f, float zTo = 0.5f)
        {
            var m = new Mesh { name = "flatquad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, zFrom), new Vector3(0.5f, 0, zFrom), new Vector3(-0.5f, 0, zTo), new Vector3(0.5f, 0, zTo) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            m.RecalculateBounds();
            return m;
        }

        // 同じマテリアルの部品をまとめて1回の描画にする（木や草など数が多いもの用）
        public static GameObject Combine(string name, List<CombineInstance> parts, Material mat, bool shadow = true)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public static CombineInstance CI(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            return new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) };
        }

        public static void SetFlash(List<Material> mats, float amount, Color c)
        {
            foreach (var m in mats)
            {
                m.SetFloat("_Flash", amount);
                m.SetColor("_FlashColor", c);
            }
        }
    }
}
