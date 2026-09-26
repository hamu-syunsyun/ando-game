using System.Collections.Generic;
using UnityEngine;

namespace AndoBoss
{
    // 主人公（雷元素の片手剣使いの受講生）
    public class Player : MonoBehaviour
    {
        public const float MaxHp = 1000f;
        static readonly float[] SwingDur = { 0.3f, 0.3f, 0.4f, 0.58f };
        static readonly float[] SwingDmg = { 45, 55, 70, 125 };

        // 状態
        public Vector3 Pos;
        public float Vy, Face, Hp, Stam, StamDelay, Inv, Dodge, DodgeAge, SkillCd, Energy, HurtT, BuffT, BurstT, DeadT;
        public bool OnGround, Moving, Dead, Victory;
        Vector3 dodgeDir;
        bool perfectUsed;
        class Swing { public float t, dur, face; public int idx; public bool hit; }
        Swing swing;
        int comboIdx; float comboTimer; bool queued;
        float walkT, stepT, idleT;
        public bool CanAct => !Dead && !Victory && Game.I.State == Game.Mode.Battle && BurstT <= 0;

        // 見た目
        Transform body, hipL, hipR, armL, armR, head, swordPivot, scarfA, scarfB, pony;
        TrailRenderer trail;
        readonly List<Material> mats = new List<Material>();
        Material bladeMat;
        float flash; Color flashC = Color.white;
        Vector3 lastPos;
        float scarfSwing;

        public void Build()
        {
            body = Mat.Pivot(transform, "body", Vector3.zero);
            Material M(Color c, float o = 0.018f) { var m = Mat.Toon(c, o); mats.Add(m); return m; }
            var skin = M(new Color(1f, 0.87f, 0.77f));
            var jacket = M(new Color(0.16f, 0.18f, 0.32f));
            var pants = M(new Color(0.12f, 0.12f, 0.2f));
            var white = M(new Color(0.96f, 0.96f, 1f));
            var gold = M(new Color(1f, 0.8f, 0.35f), 0);
            var hair = M(new Color(0.3f, 0.22f, 0.45f));
            var scarfM = M(Mat.Electro);
            var shoe = M(new Color(0.25f, 0.2f, 0.25f));
            var eyeM = M(new Color(0.55f, 0.3f, 0.95f), 0);
            var black = M(new Color(0.1f, 0.08f, 0.14f), 0);

            // 脚
            hipL = Mat.Pivot(body, "hipL", new Vector3(-0.13f, 0.86f, 0));
            hipR = Mat.Pivot(body, "hipR", new Vector3(0.13f, 0.86f, 0));
            foreach (var h in new[] { hipL, hipR })
            {
                Mat.Part(h, Mat.Frustum(0.085f, 0.11f, 0.8f, 10), pants, new Vector3(0, -0.4f, 0), Vector3.one);
                Mat.Part(h, Mat.Sphere, shoe, new Vector3(0, -0.8f, 0.05f), new Vector3(0.2f, 0.14f, 0.3f));
            }
            // 胴
            Mat.Part(body, Mat.Frustum(0.21f, 0.2f, 0.22f, 14), pants, new Vector3(0, 0.92f, 0), Vector3.one);
            Mat.Part(body, Mat.Frustum(0.25f, 0.2f, 0.52f, 14), jacket, new Vector3(0, 1.17f, 0), Vector3.one);
            // 学ランの裾（少し広がる）
            Mat.Part(body, Mat.Frustum(0.3f, 0.24f, 0.22f, 14), jacket, new Vector3(0, 0.9f, 0), Vector3.one);
            for (int i = 0; i < 3; i++) Mat.Part(body, Mat.Sphere, gold, new Vector3(0, 1.3f - i * 0.13f, 0.215f + i * 0.008f), Vector3.one * 0.05f);
            Mat.Part(body, Mat.Frustum(0.15f, 0.13f, 0.08f, 12), white, new Vector3(0, 1.44f, 0), Vector3.one);
            // マフラー
            Mat.Part(body, Mat.Frustum(0.19f, 0.16f, 0.11f, 14), scarfM, new Vector3(0, 1.43f, 0), Vector3.one);
            scarfA = Mat.Pivot(body, "scarfA", new Vector3(0.06f, 1.42f, -0.14f));
            scarfB = Mat.Pivot(body, "scarfB", new Vector3(-0.06f, 1.42f, -0.14f));
            Mat.Part(scarfA, Mat.Cube, scarfM, new Vector3(0, -0.3f, 0), new Vector3(0.1f, 0.6f, 0.03f));
            Mat.Part(scarfB, Mat.Cube, scarfM, new Vector3(0, -0.24f, 0), new Vector3(0.1f, 0.48f, 0.03f));

            // 頭
            head = Mat.Pivot(body, "head", new Vector3(0, 1.47f, 0));
            Mat.Part(head, Mat.Sphere, skin, new Vector3(0, 0.27f, 0), new Vector3(0.56f, 0.58f, 0.54f));
            Mat.Part(head, Mat.Sphere, hair, new Vector3(0, 0.33f, -0.04f), new Vector3(0.62f, 0.6f, 0.6f));
            // 前髪
            for (int i = -2; i <= 2; i++)
                Mat.Part(head, Mat.Sphere, hair, new Vector3(i * 0.09f, 0.44f - Mathf.Abs(i) * 0.03f, 0.2f - Mathf.Abs(i) * 0.02f), new Vector3(0.16f, 0.24f, 0.1f), new Vector3(15, 0, i * -12));
            // ポニーテール
            pony = Mat.Pivot(head, "pony", new Vector3(0, 0.42f, -0.26f));
            Mat.Part(pony, Mat.Frustum(0.03f, 0.12f, 0.5f, 10), hair, new Vector3(0, -0.22f, -0.05f), Vector3.one, new Vector3(-20, 0, 0));
            Mat.Part(pony, Mat.Sphere, scarfM, Vector3.zero, Vector3.one * 0.1f);
            // アホ毛
            Mat.Part(head, Mat.Frustum(0.035f, 0.005f, 0.22f, 6), hair, new Vector3(0.02f, 0.66f, 0.05f), Vector3.one, new Vector3(-30, 0, -15));
            // 目（アニメっぽい大きめの紫の目）
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                Mat.Part(head, Mat.Sphere, white, new Vector3(x, 0.26f, 0.235f), new Vector3(0.1f, 0.13f, 0.05f), default, false);
                Mat.Part(head, Mat.Sphere, eyeM, new Vector3(x, 0.255f, 0.25f), new Vector3(0.075f, 0.105f, 0.04f), default, false);
                Mat.Part(head, Mat.Sphere, black, new Vector3(x, 0.25f, 0.262f), new Vector3(0.035f, 0.05f, 0.02f), default, false);
                Mat.Part(head, Mat.Sphere, white, new Vector3(x + 0.02f, 0.285f, 0.268f), new Vector3(0.025f, 0.025f, 0.01f), default, false);
                Mat.Part(head, Mat.Cube, hair, new Vector3(x, 0.36f, 0.25f), new Vector3(0.1f, 0.018f, 0.02f), new Vector3(0, 0, x > 0 ? 8 : -8), false);
            }
            Mat.Part(head, Mat.Cube, M(new Color(0.75f, 0.35f, 0.35f), 0), new Vector3(0, 0.12f, 0.265f), new Vector3(0.06f, 0.012f, 0.01f), default, false);

            // 腕
            armL = Mat.Pivot(body, "armL", new Vector3(-0.28f, 1.37f, 0));
            armR = Mat.Pivot(body, "armR", new Vector3(0.28f, 1.37f, 0));
            foreach (var a in new[] { armL, armR })
            {
                Mat.Part(a, Mat.Frustum(0.065f, 0.075f, 0.5f, 10), jacket, new Vector3(0, -0.25f, 0), Vector3.one);
                Mat.Part(a, Mat.Frustum(0.07f, 0.07f, 0.05f, 10), white, new Vector3(0, -0.49f, 0), Vector3.one);
                Mat.Part(a, Mat.Sphere, skin, new Vector3(0, -0.56f, 0), Vector3.one * 0.13f);
            }

            // 剣（雷をまとった片手剣）。剣は +Z 方向に伸びる
            swordPivot = Mat.Pivot(body, "sword", new Vector3(0.28f, 1.1f, 0.1f));
            var grip = Mat.Pivot(swordPivot, "grip", new Vector3(0, 0, 0.45f));
            Mat.Part(grip, Mat.Frustum(0.035f, 0.035f, 0.26f, 8), M(new Color(0.25f, 0.18f, 0.3f), 0.01f), new Vector3(0, 0, -0.08f), Vector3.one, new Vector3(90, 0, 0));
            Mat.Part(grip, Mat.Cube, gold, new Vector3(0, 0, 0.07f), new Vector3(0.3f, 0.06f, 0.07f));
            Mat.Part(grip, Mat.Sphere, M(Mat.Electro, 0), new Vector3(0, 0, 0.07f), Vector3.one * 0.09f);
            bladeMat = Mat.Toon(new Color(0.85f, 0.8f, 1f), 0.012f, new Color(0.7f, 0.5f, 1f, 0.35f));
            mats.Add(bladeMat);
            Mat.Part(grip, Mat.Cube, bladeMat, new Vector3(0, 0, 0.62f), new Vector3(0.1f, 0.025f, 1.05f));
            Mat.Part(grip, Mat.Frustum(0.05f, 0.0f, 0.16f, 4), bladeMat, new Vector3(0, 0, 1.22f), new Vector3(1, 1, 0.25f), new Vector3(90, 0, 0));
            var tip = Mat.Pivot(grip, "tip", new Vector3(0, 0, 1.2f));
            var root = Mat.Pivot(grip, "troot", new Vector3(0, 0, 0.25f));
            trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.9f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Mat.Electro, 0.3f), new GradientColorKey(new Color(0.4f, 0.2f, 1f), 1) },
                      new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            trail.sharedMaterial = Mat.Fx(Color.white, Mat.White, true, 2.2f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
            _ = root;

            // 足元の影（丸）
            var sh = Mat.Part(transform, Mat.Disc(24), Mat.Fx(new Color(0, 0, 0, 0.35f), Mat.SoftGlow, false), new Vector3(0, 0.03f, 0), new Vector3(0.5f, 1, 0.5f), default, false);
            sh.name = "blob";
        }

        public void ResetState()
        {
            Pos = new Vector3(0, 0, -9);
            Vy = 0; Face = 0; Hp = MaxHp; Stam = 100; StamDelay = 0; Inv = 0; Dodge = 0; SkillCd = 0; Energy = 40;
            HurtT = 0; BuffT = 0; BurstT = 0; DeadT = 0; OnGround = true; Dead = false; Victory = false;
            swing = null; comboIdx = 0; comboTimer = 0; queued = false;
            transform.position = Pos;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
            trail.Clear();
        }

        // ================= 行動 =================
        float FaceForAttack()
        {
            var B = Game.I.Boss;
            if (B.Alive && Flat(B.Pos - Pos).magnitude < 12) return Mathf.Atan2(B.Pos.x - Pos.x, B.Pos.z - Pos.z);
            float y = Game.I.Cam.Yaw;
            return y;
        }

        void StartSwing()
        {
            swing = new Swing { t = 0, dur = SwingDur[comboIdx], idx = comboIdx, face = FaceForAttack() };
            Sfx.Play(comboIdx == 3 ? "slash3" : "slash" + comboIdx, 0.8f, comboIdx == 3 ? 0.9f : 1f, 0.08f);
            trail.Clear();
            trail.emitting = true;
            queued = false;
        }

        void MeleeHit(Swing sw)
        {
            var G = Game.I; var B = G.Boss;
            Vector3 fwd = new Vector3(Mathf.Sin(Face), 0, Mathf.Cos(Face));
            var tipPos = Pos + fwd * 1.5f + Vector3.up * 1.1f;
            Fx.Sparks(tipPos, Mat.ElectroLight, 5, 0.6f);
            float d = Flat(B.Pos - Pos).magnitude;
            float dir = Mathf.Atan2(B.Pos.x - Pos.x, B.Pos.z - Pos.z);
            bool inArc = AngDiff(Face, dir) < (sw.idx == 3 ? Mathf.PI : 1.45f);
            float reach = Boss.Radius + (sw.idx == 3 ? 3.2f : 2.5f);
            if (B.Alive && d < reach && inArc && B.Y < 2.5f)
            {
                var hitPos = B.Pos + Vector3.up * 2.2f - Flat(B.Pos - Pos).normalized * Boss.Radius;
                G.DamageBoss(SwingDmg[sw.idx], Game.HitKind.Normal, hitPos);
                if (sw.idx == 3) { Game.I.Cam.Shake(0.25f); Fx.Ring(Pos + fwd * 1.5f, 3.5f, Mat.Electro, 0.3f); }
            }
            else if (sw.idx == 3) Fx.Ring(Pos + fwd * 1.2f, 3.2f, Mat.Electro, 0.3f);
        }

        void TryDodge()
        {
            if (Dodge > 0 || Stam < 25) { if (Stam < 25) Game.I.Hud.NoStamina(); return; }
            Stam -= 25; StamDelay = 0.8f;
            Dodge = 0.3f; DodgeAge = 0; perfectUsed = false;
            swing = null; trail.emitting = false;
            var mv = GameInput.MoveVector(Game.I.Cam.Yaw);
            dodgeDir = mv.sqrMagnitude > 0 ? mv : new Vector3(-Mathf.Sin(Face), 0, -Mathf.Cos(Face));
            Face = Mathf.Atan2(dodgeDir.x, dodgeDir.z);
            Sfx.Play("whoosh", 0.8f);
            Fx.Sparks(Pos + Vector3.up * 0.8f, Mat.ElectroLight, 8, 0.5f);
        }

        void TryJump()
        {
            if (!OnGround) return;
            Vy = 8.8f; OnGround = false;
            Sfx.Play("jump", 0.6f);
            Fx.Debris(Pos + Vector3.up * 0.1f, new Color(0.85f, 0.8f, 0.7f, 0.7f), 5);
        }

        void TrySkill()
        {
            if (SkillCd > 0) return;
            var G = Game.I; var B = G.Boss;
            SkillCd = 6f;
            swing = null; trail.emitting = false;
            Inv = Mathf.Max(Inv, 0.35f);
            Fx.Ring(Pos, 6.2f, Mat.Electro, 0.35f, 3f);
            Fx.Ring(Pos, 4f, Color.white, 0.25f, 3f);
            Fx.Glow(Pos + Vector3.up, Mat.Electro, 2.5f);
            Fx.Sparks(Pos + Vector3.up, Mat.ElectroLight, 26, 1.3f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2 + Random.value;
                Fx.Bolt(Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.5f, Mat.Electro, 0.18f, 6f);
            }
            Sfx.Play("skill", 1f);
            G.Cam.Shake(0.2f);
            G.Cam.FovPunch(-4);
            PostFX.I?.Chroma(0.15f);
            if (B.Alive && Flat(B.Pos - Pos).magnitude < 6.2f + Boss.Radius && B.Y < 3)
            {
                G.DamageBoss(170, Game.HitKind.Skill, B.Pos + Vector3.up * 2.4f);
                G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 3);
            }
        }

        void TryBurst()
        {
            if (Energy < 100) return;
            var G = Game.I;
            Energy = 0;
            BurstT = 1.25f;
            Inv = Mathf.Max(Inv, 3f);
            swing = null; trail.emitting = false;
            G.StartBurst();
        }

        // ================= 毎フレーム =================
        public void Tick(float dt)
        {
            var G = Game.I;
            Inv = Mathf.Max(0, Inv - dt);
            HurtT = Mathf.Max(0, HurtT - dt);
            SkillCd = Mathf.Max(0, SkillCd - dt);
            BuffT = Mathf.Max(0, BuffT - dt);
            if (G.State == Game.Mode.Battle && !Dead) Energy = Mathf.Min(100, Energy + dt * 1.5f);
            if (StamDelay > 0) StamDelay -= dt; else Stam = Mathf.Min(100, Stam + dt * 32);

            Vy -= 26 * dt;
            Pos.y += Vy * dt;
            if (Pos.y <= 0)
            {
                if (!OnGround && Vy < -6) { Sfx.Play("land", 0.5f); Fx.Debris(Pos, new Color(0.85f, 0.8f, 0.7f, 0.7f), 4); }
                Pos.y = 0; Vy = 0; OnGround = true;
            }

            Moving = false;
            if (BurstT > 0)
            {
                BurstT -= dt;
                if (BurstT <= 0) Game.I.FireBurst();
            }
            else if (CanAct)
            {
                if (GameInput.Down(GameInput.K.Dodge)) TryDodge();
                if (GameInput.Down(GameInput.K.Jump) && Dodge <= 0) TryJump();
                if (GameInput.Down(GameInput.K.Skill)) TrySkill();
                if (GameInput.Down(GameInput.K.Burst)) TryBurst();
                if (GameInput.Down(GameInput.K.Attack)) queued = true;

                var mv = GameInput.MoveVector(G.Cam.Yaw);
                if (Dodge > 0)
                {
                    Dodge -= dt; DodgeAge += dt;
                    Pos += dodgeDir * 19 * dt;
                    if (Random.value < 0.6f) Fx.Embers(Pos + Vector3.up * Random.Range(0.3f, 1.5f), Mat.Electro, 1);
                }
                else if (swing != null) Pos += mv * 1.2f * dt;
                else if (mv.sqrMagnitude > 0)
                {
                    Pos += mv * 6.8f * dt;
                    Face = TurnTo(Face, Mathf.Atan2(mv.x, mv.z), dt * 14);
                    Moving = true;
                }

                if (swing != null)
                {
                    var sw = swing;
                    sw.t += dt;
                    Face = TurnTo(Face, sw.face, dt * 20);
                    // 4段目は前に踏み込む
                    if (sw.idx == 3 && sw.t < sw.dur * 0.4f) Pos += new Vector3(Mathf.Sin(Face), 0, Mathf.Cos(Face)) * 5f * dt;
                    if (!sw.hit && sw.t >= sw.dur * 0.45f) { sw.hit = true; MeleeHit(sw); }
                    if (sw.t >= sw.dur)
                    {
                        swing = null;
                        trail.emitting = false;
                        comboTimer = 0.55f;
                        comboIdx = (sw.idx + 1) % 4;
                    }
                    else if (sw.t > sw.dur * 0.6f && (queued || GameInput.Held(GameInput.K.Attack)) && sw.idx < 3)
                    {
                        // キャンセルして次の段へ（テンポよく連打できるように）
                        trail.emitting = false;
                        comboIdx = sw.idx + 1;
                        StartSwing();
                    }
                }
                else
                {
                    comboTimer -= dt;
                    if (comboTimer <= 0) comboIdx = 0;
                    if ((queued || GameInput.Held(GameInput.K.Attack)) && Dodge <= 0) StartSwing();
                }
            }
            else
            {
                if (swing != null) { swing = null; trail.emitting = false; }
                Dodge = 0;
            }

            // アリーナの外に出ない・ボスにめりこまない
            var flat = Flat(Pos);
            if (flat.magnitude > World.ArenaR - 0.6f)
            {
                var c = flat.normalized * (World.ArenaR - 0.6f);
                Pos.x = c.x; Pos.z = c.z;
            }
            var B = G.Boss;
            var dv = Flat(Pos - B.Pos);
            float min = Boss.Radius + 0.45f;
            if (B.Alive && dv.magnitude < min && B.Y < 1.5f)
            {
                var c = B.Pos + (dv.sqrMagnitude > 1e-4f ? dv.normalized : Vector3.back) * min;
                Pos.x = c.x; Pos.z = c.z;
            }

            if (Dead) DeadT += dt;
            Animate(dt);
        }

        // 攻撃を受けたとき。当たったら true
        public bool TakeHit(float amount)
        {
            var G = Game.I;
            if (Dead || Victory || G.State != Game.Mode.Battle) return false;
            if (Dodge > 0)
            {
                if (!perfectUsed && DodgeAge < 0.24f) { perfectUsed = true; G.PerfectDodge(); }
                return false;
            }
            if (Inv > 0) return false;
            Hp = Mathf.Max(0, Hp - amount);
            Inv = 0.7f; HurtT = 0.45f;
            flash = 1; flashC = new Color(1, 0.3f, 0.3f);
            swing = null; trail.emitting = false;
            G.OnPlayerHurt(amount);
            if (Hp <= 0) { Dead = true; G.OnPlayerDown(); }
            return true;
        }

        // ================= 見た目の動き =================
        void Animate(float dt)
        {
            transform.position = Pos;
            float moveSpeed = (Flat(Pos - lastPos).magnitude) / Mathf.Max(dt, 1e-4f);
            lastPos = Pos;
            body.localRotation = Quaternion.Euler(0, Face * Mathf.Rad2Deg, 0);
            idleT += dt;

            float lean = 0, bob = 0, legSwing = 0, armSwing = 0;
            Quaternion armLRot = Quaternion.Euler(0, 0, -8), armRRot = Quaternion.Euler(0, 0, 8);
            Quaternion swordRot = Quaternion.Euler(125, 0, -20);
            float spin = 0;

            if (Dead)
            {
                float u = Mathf.Clamp01(DeadT / 0.6f);
                body.localRotation *= Quaternion.Euler(-80 * u, 0, 0);
                body.localPosition = new Vector3(0, 0.15f * u, 0);
                swordRot = Quaternion.Euler(170, 0, -40);
            }
            else if (Victory)
            {
                armRRot = Quaternion.Euler(0, 0, 160 + Mathf.Sin(idleT * 3) * 10);
                swordRot = Quaternion.Euler(-80, 0, 0);
                bob = Mathf.Abs(Mathf.Sin(idleT * 4)) * 0.2f;
            }
            else if (BurstT > 0)
            {
                // 剣を天にかかげる
                float u = 1 - BurstT / 1.25f;
                armRRot = Quaternion.Euler(0, 0, 170);
                swordRot = Quaternion.Euler(-90, 0, 0);
                bob = Mathf.Sin(u * Mathf.PI) * 0.6f;
                if (Random.value < 0.8f) Fx.Sparks(transform.position + Vector3.up * 3.2f, Mat.ElectroLight, 2, 0.6f);
            }
            else if (Dodge > 0)
            {
                lean = 35;
                legSwing = 40;
                swordRot = Quaternion.Euler(150, 0, 0);
            }
            else if (swing != null)
            {
                float u = Mathf.Clamp01(swing.t / swing.dur);
                float e = u < 0.45f ? Mathf.SmoothStep(0, 1, u / 0.45f) : 1;
                switch (swing.idx)
                {
                    case 0: swordRot = Quaternion.Euler(0, Mathf.Lerp(110, -80, e), -30); lean = 10; break;
                    case 1: swordRot = Quaternion.Euler(0, Mathf.Lerp(-100, 90, e), 25); lean = 10; break;
                    case 2: swordRot = Quaternion.Euler(Mathf.Lerp(-120, 50, e), -5, 0); lean = Mathf.Lerp(-10, 25, e); break;
                    default:
                        spin = Mathf.Lerp(0, 360, Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / 0.55f)));
                        swordRot = Quaternion.Euler(10, 70, 0);
                        armLRot = Quaternion.Euler(0, 0, -70);
                        bob = Mathf.Sin(Mathf.Clamp01(u / 0.55f) * Mathf.PI) * 0.35f;
                        break;
                }
                // 剣の向きに右腕を合わせる
                var dir = swordRot * Vector3.forward;
                armRRot = Quaternion.FromToRotation(Vector3.down, (dir + Vector3.down * 0.3f).normalized);
            }
            else if (!OnGround)
            {
                legSwing = 0;
                armLRot = Quaternion.Euler(0, 0, -50); armRRot = Quaternion.Euler(0, 0, 50);
                hipL.localRotation = Quaternion.Euler(-40, 0, 0);
                hipR.localRotation = Quaternion.Euler(-10, 0, 0);
            }
            else if (moveSpeed > 1f)
            {
                walkT += dt * 11;
                legSwing = Mathf.Sin(walkT) * 40;
                armSwing = Mathf.Sin(walkT) * 35;
                bob = Mathf.Abs(Mathf.Sin(walkT)) * 0.08f;
                lean = 12;
                stepT -= dt;
                if (Mathf.Sin(walkT) * Mathf.Sin(walkT - dt * 11) < 0) Sfx.Play("step", 0.35f, 1, 0.15f);
            }
            else
            {
                bob = Mathf.Sin(idleT * 2.2f) * 0.015f;
                armLRot = Quaternion.Euler(Mathf.Sin(idleT * 2.2f) * 3, 0, -8);
            }

            if (HurtT > 0) lean -= 20 * (HurtT / 0.45f);

            if (!Dead)
            {
                body.localPosition = new Vector3(0, bob, 0);
                body.localRotation = Quaternion.Euler(lean, Face * Mathf.Rad2Deg + spin, 0);
                if (OnGround || Dodge > 0)
                {
                    hipL.localRotation = Quaternion.Euler(legSwing, 0, 0);
                    hipR.localRotation = Quaternion.Euler(-legSwing, 0, 0);
                }
            }
            if (swing == null && BurstT <= 0 && !Victory && !Dead && moveSpeed > 1f && OnGround)
            {
                armLRot = Quaternion.Euler(-armSwing, 0, -8);
                armRRot = Quaternion.Euler(armSwing * 0.5f, 0, 10);
            }
            armL.localRotation = Quaternion.Slerp(armL.localRotation, armLRot, dt * 18);
            armR.localRotation = swing != null ? armRRot : Quaternion.Slerp(armR.localRotation, armRRot, dt * 18);
            swordPivot.localRotation = swing != null ? swordRot : Quaternion.Slerp(swordPivot.localRotation, swordRot, dt * 14);
            // 剣の根元は右手の位置に合わせる
            swordPivot.localPosition = armR.localPosition + armR.localRotation * new Vector3(0, -0.56f, 0) - swordPivot.localRotation * new Vector3(0, 0, 0.45f);

            // マフラーとポニーテールは動きに遅れてなびく
            float target = Mathf.Clamp(moveSpeed * 5, 0, 70) + Mathf.Sin(idleT * 5) * 6;
            scarfSwing = Mathf.Lerp(scarfSwing, target, dt * 6);
            scarfA.localRotation = Quaternion.Euler(scarfSwing + 10, 0, Mathf.Sin(idleT * 7) * 8);
            scarfB.localRotation = Quaternion.Euler(scarfSwing * 0.8f + 12, 0, -Mathf.Sin(idleT * 6) * 8);
            pony.localRotation = Quaternion.Euler(-scarfSwing * 0.5f, Mathf.Sin(idleT * 3) * 10, 0);
            head.localRotation = Quaternion.Euler(HurtT > 0 ? -15 : 0, 0, 0);

            // 光る剣：バフ中は強く光る
            float glowA = 0.35f + (BuffT > 0 ? 0.5f + 0.2f * Mathf.Sin(idleT * 20) : 0) + (swing != null ? 0.3f : 0);
            bladeMat.SetColor("_Emission", new Color(0.7f, 0.5f, 1f, glowA));
            if (BuffT > 0 && Random.value < 0.3f) Fx.Embers(transform.position + Vector3.up * Random.Range(0.5f, 1.8f) + Random.insideUnitSphere * 0.4f, Mat.Electro, 1);

            // 点滅（無敵中）・被弾フラッシュ
            flash = Mathf.MoveTowards(flash, 0, dt * 5);
            float blink = (Inv > 0 && HurtT <= 0 && BurstT <= 0 && Game.I.State == Game.Mode.Battle && Mathf.Sin(Time.time * 50) > 0.6f) ? 0.4f : 0;
            Mat.SetFlash(mats, Mathf.Max(flash, blink), flash > 0 ? flashC : Color.white);
        }

        // ---- 小物 ----
        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        public static float TurnTo(float a, float target, float k)
        {
            float d = Mathf.DeltaAngle(a * Mathf.Rad2Deg, target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return a + d * Mathf.Min(1, k);
        }
        public static float AngDiff(float a, float b) => Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, b * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
    }
}
