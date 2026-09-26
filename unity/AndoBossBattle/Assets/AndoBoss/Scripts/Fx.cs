using System;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine;

namespace AndoBoss
{
    // エフェクトと時間の演出（ヒットストップ・スロー）をまとめて管理する
    public class Fx : MonoBehaviour
    {
        public static Fx I;

        class Job { public Func<float, bool> update; public GameObject obj; }
        readonly List<Job> jobs = new List<Job>();
        readonly List<Job> adding = new List<Job>();
        class Timer { public float t; public Action fn; }
        readonly List<Timer> timers = new List<Timer>();

        ParticleSystem sparks, glow, debris, confetti, stars, embers;
        Mesh disc, flatQuad, wallMesh, beamQuad, spikeMesh;

        // 時間
        float hitStop;          // 実時間でのこり何秒止めるか
        float slowT, slowScale = 1f;
        public bool Paused;

        void Awake()
        {
            I = this;
            disc = Mat.Disc(48);
            flatQuad = Mat.FlatQuad();
            wallMesh = Mat.Frustum(1, 1, 1, 48, false, true);
            beamQuad = Mat.FlatQuad(0, 1);
            spikeMesh = Mat.Frustum(0.35f, 0f, 1f, 6);

            sparks = MakePS("sparks", Mat.Streak, true, 800, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(4, 14);
                m.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
                m.gravityModifier = 1.2f;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.5f;
            });
            glow = MakePS("glow", Mat.Glow, true, 200, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                m.startSpeed = 0;
                m.startSize = 3f;
                var sz = ps.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0.4f), new Keyframe(0.3f, 1f), new Keyframe(1, 1.3f)));
            });
            debris = MakePS("debris", Mat.SoftGlow, false, 400, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(3, 9);
                m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                m.gravityModifier = 0.9f;
            });
            confetti = MakePS("confetti", Mat.White, false, 1500, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(8, 18);
                m.startSize3D = true;
                m.startSizeX = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                m.startSizeY = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                m.startSizeZ = 1;
                m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                m.gravityModifier = 0.5f;
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-8, 8);
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35; sh.radius = 0.5f;
                var noise = ps.noise; noise.enabled = true; noise.strength = 1.5f; noise.frequency = 0.8f;
                var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.drag = 1.2f; drag.limit = 100;
            }, false);
            stars = MakePS("stars", Mat.Star, true, 300, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(2, 6);
                m.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4, 4);
            });
            embers = MakePS("embers", Mat.Glow, true, 600, ps =>
            {
                var m = ps.main;
                m.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
                m.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
                m.gravityModifier = -0.25f;
            });
        }

        ParticleSystem MakePS(string name, Texture tex, bool additive, int max, Action<ParticleSystem> setup, bool fade = true)
        {
            var go = new GameObject("ps_" + name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.maxParticles = max;
            m.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.2f;
            if (fade)
            {
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                          new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 0.5f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var sz = ps.sizeOverLifetime; sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0.2f));
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mat.Fx(Color.white, tex, additive, additive ? 2.2f : 1f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            setup(ps);
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector3 pos, Color c, int n, float speedMul = 1f, float sizeMul = 1f)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, startColor = c, applyShapeToPosition = true };
            var m = ps.main;
            for (int i = 0; i < n; i++)
            {
                if (speedMul != 1f || sizeMul != 1f)
                {
                    var dir = UnityEngine.Random.onUnitSphere;
                    ep.velocity = dir * UnityEngine.Random.Range(m.startSpeed.constantMin, m.startSpeed.constantMax) * speedMul;
                    ep.startSize = UnityEngine.Random.Range(m.startSize.constantMin, m.startSize.constantMax) * sizeMul;
                }
                ps.Emit(ep, 1);
            }
        }

        // ---------- よく使うエフェクト ----------
        public static void Sparks(Vector3 p, Color c, int n = 12, float speed = 1f) { if (I) Emit(I.sparks, p, c, n, speed); }
        public static void Glow(Vector3 p, Color c, float size = 1f)
        {
            if (!I) return;
            var ep = new ParticleSystem.EmitParams { position = p, startColor = c, startSize = 3f * size, applyShapeToPosition = false };
            I.glow.Emit(ep, 1);
        }
        public static void Debris(Vector3 p, Color c, int n = 10) { if (I) Emit(I.debris, p, c, n); }
        public static void Stars(Vector3 p, Color c, int n = 8) { if (I) Emit(I.stars, p, c, n); }
        public static void Embers(Vector3 p, Color c, int n = 3) { if (I) Emit(I.embers, p, c, n); }
        public static void Confetti(Vector3 p, int n = 120)
        {
            if (!I) return;
            Color[] cs = { new Color(1, 0.35f, 0.4f), new Color(1, 0.85f, 0.25f), new Color(0.35f, 0.85f, 1), new Color(0.7f, 0.5f, 1), new Color(0.4f, 1, 0.55f), Color.white };
            var sh = I.confetti.shape;
            sh.rotation = new Vector3(-90, 0, 0);
            for (int i = 0; i < n; i++)
            {
                var ep = new ParticleSystem.EmitParams { position = p, startColor = cs[i % cs.Length], applyShapeToPosition = true };
                I.confetti.Emit(ep, 1);
            }
        }

        public static GameObject Obj(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        // 地面に広がる光の輪
        public static void Ring(Vector3 p, float radius, Color c, float dur = 0.4f, float intensity = 2.5f)
        {
            if (!I) return;
            var m = Mat.Fx(c, Mat.RingTex, true, intensity);
            var go = Obj("ring", I.disc, m, new Vector3(p.x, 0.06f, p.z), Quaternion.identity, Vector3.one * 0.1f);
            float t = 0;
            Run(dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                float e = 1 - (1 - u) * (1 - u) * (1 - u);
                go.transform.localScale = new Vector3(radius * e, 1, radius * e);
                m.SetColor("_Color", new Color(c.r, c.g, c.b, 1 - u));
                return u < 1;
            }, go);
        }

        // 空中に広がる輪（縦向き。カメラに向けて）
        public static void AirRing(Vector3 p, float radius, Color c, float dur = 0.3f)
        {
            if (!I) return;
            var m = Mat.Fx(c, Mat.RingTex, true, 3f);
            var cam = Camera.main;
            var rot = cam ? Quaternion.LookRotation(cam.transform.position - p) * Quaternion.Euler(90, 0, 0) : Quaternion.identity;
            var go = Obj("airring", I.disc, m, p, rot, Vector3.one * 0.1f);
            float t = 0;
            Run(dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                float e = 1 - (1 - u) * (1 - u);
                go.transform.localScale = new Vector3(radius * e, 1, radius * e);
                m.SetColor("_Color", new Color(c.r, c.g, c.b, 1 - u));
                return u < 1;
            }, go);
        }

        // 空から落ちる稲妻（ギザギザの線を何本か重ねる）
        public static void Bolt(Vector3 ground, Color c, float width = 0.35f, float height = 26f)
        {
            if (!I) return;
            for (int k = 0; k < 2; k++)
            {
                var go = new GameObject("bolt");
                var lr = go.AddComponent<LineRenderer>();
                int n = 14;
                lr.positionCount = n;
                lr.useWorldSpace = true;
                var top = ground + new Vector3(UnityEngine.Random.Range(-3f, 3f), height, UnityEngine.Random.Range(-3f, 3f));
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)(n - 1);
                    var p = Vector3.Lerp(top, ground, u);
                    if (i > 0 && i < n - 1) p += new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)) * (0.9f - u * 0.5f);
                    lr.SetPosition(i, p);
                }
                float w = k == 0 ? width : width * 3.5f;
                lr.widthMultiplier = w;
                var m = Mat.Fx(k == 0 ? Color.white : c, k == 0 ? Mat.White : Mat.Streak, true, k == 0 ? 4f : 2.2f);
                lr.sharedMaterial = m;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                float t = 0;
                Run(dt =>
                {
                    t += dt;
                    float a = t < 0.05f ? 1 : Mathf.Clamp01(1 - (t - 0.05f) / 0.25f) * (0.6f + 0.4f * Mathf.Sin(t * 90));
                    lr.widthMultiplier = w * (0.6f + a * 0.4f);
                    m.SetColor("_Color", new Color(m.GetColor("_Color").r, m.GetColor("_Color").g, m.GetColor("_Color").b, a));
                    return t < 0.3f;
                }, go);
            }
            Glow(ground + Vector3.up * 0.5f, c, 2.2f);
            Ring(ground, 3.2f, c, 0.35f);
            Sparks(ground + Vector3.up * 0.3f, c, 14, 1.2f);
            Scorch(ground, 1.6f);
            Sky.Flash(0.6f);
        }

        // 焦げあと
        public static void Scorch(Vector3 p, float r)
        {
            if (!I) return;
            var m = Mat.Fx(new Color(0.08f, 0.05f, 0.12f, 0.6f), Mat.SoftGlow, false, 1f);
            var go = Obj("scorch", I.disc, m, new Vector3(p.x, 0.03f, p.z), Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0), new Vector3(r, 1, r));
            float t = 0;
            Run(dt => { t += dt; m.SetColor("_Color", new Color(0.08f, 0.05f, 0.12f, 0.6f * Mathf.Clamp01(1 - (t - 2) / 1.5f))); return t < 3.5f; }, go);
        }

        // 攻撃予告の円（delay 秒待ってから dur 秒かけて満ち、満ちたら onFire）
        public static void TelegraphCircle(Vector3 p, float r, float delay, float dur, Action onFire)
        {
            if (!I) return;
            var m = Mat.Telegraph(Mat.Danger, false);
            GameObject go = null;
            float t = -delay;
            Run(dt =>
            {
                t += dt;
                if (t < 0) return true;
                if (go == null) { go = Obj("tele", I.flatQuad, m, new Vector3(p.x, 0.05f, p.z), Quaternion.identity, new Vector3(r * 2, 1, r * 2)); }
                m.SetFloat("_Fill", Mathf.Clamp01(t / dur));
                m.SetFloat("_Alpha", Mathf.Clamp01(t * 6));
                if (t >= dur) { onFire?.Invoke(); Kill(go); return false; }
                return true;
            });
        }

        public static GameObject TelegraphBand(Vector3 from, Vector3 dir, float len, float width, float dur)
        {
            var m = Mat.Telegraph(Mat.Danger, true);
            var go = Obj("teleband", I.beamQuad, m, new Vector3(from.x, 0.05f, from.z), Quaternion.LookRotation(dir), new Vector3(width, 1, len));
            float t = 0;
            Run(dt =>
            {
                t += dt;
                m.SetFloat("_Fill", Mathf.Clamp01(t / dur));
                m.SetFloat("_Alpha", Mathf.Clamp01(t * 6) * (t > dur ? Mathf.Clamp01(1 - (t - dur) * 5) : 1));
                return t < dur + 0.2f;
            }, go);
            return go;
        }

        // 広がっていく衝撃波の壁（ジャンプでよける）。update が true を返す間続く
        public static GameObject ShockWall(Vector3 center, Color c, Func<float, float, bool> update)
        {
            var m = Mat.Fx(c, Mat.WallTex, true, 2.4f);
            var go = Obj("wall", I.wallMesh, m, new Vector3(center.x, 0.4f, center.z), Quaternion.identity, new Vector3(1, 0.8f, 1));
            var ringM = Mat.Fx(Color.white, Mat.RingTex, true, 3f);
            var ring = Obj("wallring", I.disc, ringM, center + Vector3.up * 0.05f, Quaternion.identity, Vector3.one);
            float r = 1, t = 0;
            Run(dt =>
            {
                t += dt;
                r += 11 * dt;
                go.transform.localScale = new Vector3(r, 0.8f + Mathf.Sin(t * 30) * 0.08f, r);
                ring.transform.localScale = new Vector3(r * 1.08f, 1, r * 1.08f);
                float a = Mathf.Clamp01(1 - r / 30);
                m.SetColor("_Color", new Color(c.r, c.g, c.b, a));
                ringM.SetColor("_Color", new Color(1, 1, 1, a));
                bool more = update(r, dt) && r < 30;
                if (!more) Kill(ring);
                return more;
            }, go);
            return go;
        }

        public static Mesh BeamMesh => I.beamQuad;
        public static Mesh DiscMesh => I.disc;

        // 火柱（杉山くん）
        public static void Pillar(Vector3 p, Color c, float r, float h, float dur)
        {
            if (!I) return;
            var m = Mat.Fx(c, Mat.WallTex, true, 2.2f);
            var go = Obj("pillar", I.wallMesh, m, new Vector3(p.x, h / 2, p.z), Quaternion.identity, new Vector3(r, h, r));
            float t = 0;
            Run(dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                float grow = Mathf.Min(1, t * 8);
                go.transform.localScale = new Vector3(r * (0.6f + 0.4f * grow) * (1 + Mathf.Sin(t * 40) * 0.04f), h * grow, r * (0.6f + 0.4f * grow));
                go.transform.position = new Vector3(p.x, h * grow / 2, p.z);
                go.transform.Rotate(0, 200 * dt, 0);
                m.SetColor("_Color", new Color(c.r, c.g, c.b, (1 - u * u)));
                if (Random.value < 0.9f) Embers(p + new Vector3(Random.Range(-r, r) * 0.6f, Random.Range(0, h * 0.7f), Random.Range(-r, r) * 0.6f), c, 2);
                return u < 1;
            }, go);
            Glow(p + Vector3.up, c, 2f);
        }

        // 爆発（炎）
        public static void Explosion(Vector3 p, Color c, float size)
        {
            Glow(p, c, 1.5f * size);
            Glow(p, Color.white, 0.8f * size);
            Sparks(p, c, (int)(18 * size), 1.2f * size);
            Debris(p, new Color(0.35f, 0.3f, 0.3f, 0.7f), (int)(10 * size));
            Embers(p, c, (int)(12 * size));
            Ring(new Vector3(p.x, 0, p.z), 3 * size, c, 0.35f);
        }

        // 斬撃の線（ともきの奥義）。中心を通るランダムな向きの光の線
        public static void SlashLine(Vector3 center, Color c, float len)
        {
            if (!I) return;
            var m = Mat.Fx(c, Mat.Streak, true, 3.5f);
            var dir = Random.onUnitSphere;
            var go = Obj("slash", I.beamQuad, m, center - dir * len / 2, Quaternion.LookRotation(dir), new Vector3(0.35f, 1, len));
            go.AddComponent<BeamFacer>();
            float t = 0;
            Run(dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / 0.22f);
                go.transform.localScale = new Vector3(0.35f * (1 - u) + 0.05f, 1, len * (0.6f + 0.4f * Mathf.Min(1, t * 20)));
                m.SetColor("_Color", new Color(c.r, c.g, c.b, 1 - u));
                return u < 1;
            }, go);
        }

        // 地面から突き出る氷のとげ（やましょう）
        public static void IceSpike(Vector3 p, float h)
        {
            if (!I) return;
            var m = Mat.Toon(new Color(0.75f, 0.92f, 1f), 0.02f, new Color(0.5f, 0.85f, 1f, 0.25f));
            var go = new GameObject("spike");
            go.transform.position = new Vector3(p.x, 0, p.z);
            go.transform.rotation = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0, 360f), Random.Range(-18f, 18f));
            var part = Mat.Part(go.transform, I.spikeMesh, m, new Vector3(0, 0.5f, 0), Vector3.one, default, false);
            _ = part;
            float t = 0;
            Run(dt =>
            {
                t += dt;
                float up = Mathf.Min(1, t * 12);
                float down = t > 1.1f ? Mathf.Clamp01(1 - (t - 1.1f) * 3) : 1;
                go.transform.localScale = new Vector3(0.6f * down, h * up * down, 0.6f * down);
                return t < 1.45f;
            }, go);
            Debris(new Vector3(p.x, 0.2f, p.z), new Color(0.85f, 0.95f, 1f, 0.8f), 3);
        }

        // 使い捨てのマテリアルを持つエフェクトは、消すときにマテリアルも消す
        static readonly HashSet<string> OwnMat = new HashSet<string> { "ring", "airring", "bolt", "scorch", "tele", "teleband", "wall", "wallring", "beam", "pillar", "slash", "spike", "wave3" };
        public static void Kill(GameObject go)
        {
            if (!go) return;
            if (OwnMat.Contains(go.name))
                foreach (var r in go.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial) Destroy(r.sharedMaterial);
            Destroy(go);
        }

        // ---------- 更新の仕組み ----------
        // update(dt) が false を返したら終わり。obj を渡すと終わったときに消す
        public static void Run(Func<float, bool> update, GameObject obj = null)
        {
            if (I) I.adding.Add(new Job { update = update, obj = obj });
        }
        public static void Later(float sec, Action fn)
        {
            if (I) I.timers.Add(new Timer { t = sec, fn = fn });
        }

        public static void ClearAll()
        {
            if (!I) return;
            foreach (var j in I.jobs) Kill(j.obj);
            foreach (var j in I.adding) Kill(j.obj);
            I.jobs.Clear(); I.adding.Clear(); I.timers.Clear();
            foreach (var ps in new[] { I.sparks, I.glow, I.debris, I.confetti, I.stars, I.embers }) ps.Clear();
            foreach (var t in GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t && t.parent == null && (t.name == "tele" || t.name == "teleband" || t.name == "bullet" || t.name == "orb" || t.name == "beam" || t.name == "arrow" || t.name == "rugby" || t.name == "report")) Kill(t.gameObject);
            I.hitStop = 0; I.slowT = 0; I.slowScale = 1;
        }

        public static void HitStop(float sec) { if (I) I.hitStop = Mathf.Max(I.hitStop, sec); }
        public static void Slow(float scale, float realSec)
        {
            if (!I) return;
            if (I.slowT > 0 && I.slowScale < scale) { I.slowT = Mathf.Max(I.slowT, realSec * 0.5f); return; }
            I.slowScale = scale; I.slowT = realSec;
        }

        void Update()
        {
            float rdt = Time.unscaledDeltaTime;
            if (hitStop > 0) hitStop -= rdt;
            if (slowT > 0) { slowT -= rdt; if (slowT <= 0) slowScale = 1; }
            float ts = Paused ? 0 : hitStop > 0 ? 0.03f : slowT > 0 ? slowScale : 1f;
            Time.timeScale = ts;

            float dt = Time.deltaTime;
            if (adding.Count > 0) { jobs.AddRange(adding); adding.Clear(); }
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                var j = jobs[i];
                bool keep;
                try { keep = j.update(dt); }
                catch (Exception e) { Debug.LogException(e); keep = false; }
                if (!keep)
                {
                    Kill(j.obj);
                    jobs.RemoveAt(i);
                }
            }
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                timers[i].t -= dt;
                if (timers[i].t <= 0)
                {
                    var fn = timers[i].fn;
                    timers.RemoveAt(i);
                    try { fn(); } catch (Exception e) { Debug.LogException(e); }
                }
            }
        }
    }
}
