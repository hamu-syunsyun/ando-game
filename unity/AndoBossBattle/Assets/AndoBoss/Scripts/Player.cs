using System.Collections.Generic;
using UnityEngine;

namespace AndoBoss
{
    // 操作キャラ（ともき・杉山くん・やましょう・らいと の4人で共通のしくみ）。
    // 見た目・攻撃力・スキル・奥義は CharDef で切り替える
    public class Player : MonoBehaviour
    {
        public float MaxHp => Def.MaxHp * Game.PlayerHpMul; // 難易度で増える
        public const float DodgeCost = 20f; // 回避1回のスタミナ（前は 25）
        public float SkillCdMax => Def.SkillCd * 0.75f; // 特技のクールタイムは全員 25% 短く
        public CharDef Def;

        // 状態（HPとスタミナはパーティ共通なので Game が持つ）
        public Vector3 Pos;
        public float Vy, Face, Inv, Dodge, DodgeAge, SkillCd, Energy, HurtT, BuffT, BurstT, DeadT, SwapInT, LockT;
        // 弓を構えて撃っている間（特技・奥義など、通常攻撃以外で矢を撃つとき）。ShootUp なら空に向ける
        public float ShootT; public bool ShootUp;
        // 槍の突き出し（特技などで外から動かすとき）
        public float ThrustT;
        Vector3 knock;
        public float Hp { get => Game.I.PartyHp; set => Game.I.PartyHp = value; }
        public float Stam { get => Game.I.PartyStam; set => Game.I.PartyStam = value; }
        public bool OnGround, Moving, Dead, Victory;
        Vector3 dodgeDir;
        bool perfectUsed;
        class Swing { public float t, dur, face; public int idx; public bool hit; }
        Swing swing;
        int comboIdx; float comboTimer; bool queued;
        float walkT, idleT, elemIcd;
        public bool CanAct => !Dead && !Victory && Game.I.State == Game.Mode.Battle && BurstT <= 0 && LockT <= 0 && HurtT <= 0.2f;
        int LastIdx => Def.SwingDur.Length - 1;

        // 見た目
        Transform body, hipL, hipR, armL, armR, head, swordPivot, scarfA, scarfB, pony;
        TrailRenderer trail;
        readonly List<Material> mats = new List<Material>();
        Material bladeMat;
        float flash; Color flashC = Color.white;
        Vector3 lastPos;
        float scarfSwing;

        public void Build(CharDef def)
        {
            Def = def;
            name = def.Name;
            body = Mat.Pivot(transform, "body", Vector3.zero);
            Material M(Color c, float o = 0.016f) { var m = Mat.Toon(c, o); mats.Add(m); return m; }
            var skin = M(def.Skin);
            var jacket = M(def.Jacket);
            var pants = M(def.Pants);
            var white = M(new Color(0.96f, 0.96f, 1f));
            var gold = M(new Color(1f, 0.8f, 0.35f), 0);
            var hair = M(def.Hair);
            var accent = M(def.Accent);
            var shoe = M(new Color(0.22f, 0.2f, 0.24f));
            var eyeM = M(def.Eye, 0);
            var black = M(new Color(0.1f, 0.08f, 0.14f), 0);
            float H = def.Height;

            // 脚（前より長めにして、頭身を上げてある）
            hipL = Mat.Pivot(body, "hipL", new Vector3(-0.12f, 0.9f * H, 0));
            hipR = Mat.Pivot(body, "hipR", new Vector3(0.12f, 0.9f * H, 0));
            foreach (var h in new[] { hipL, hipR })
            {
                Mat.Part(h, Mat.Frustum(0.075f, 0.1f, 0.85f * H, 10), pants, new Vector3(0, -0.42f * H, 0), Vector3.one);
                Mat.Part(h, Mat.Sphere, shoe, new Vector3(0, -0.85f * H, 0.05f), new Vector3(0.17f, 0.12f, 0.28f));
            }
            // 胴
            float ty = 0.9f * H;
            Mat.Part(body, Mat.Frustum(0.19f, 0.18f, 0.2f, 14), pants, new Vector3(0, ty + 0.05f, 0), Vector3.one);
            Mat.Part(body, Mat.Frustum(0.23f, 0.2f, 0.55f, 14), jacket, new Vector3(0, ty + 0.32f, 0), Vector3.one);
            Mat.Part(body, Mat.Frustum(0.27f, 0.22f, 0.22f, 14), jacket, new Vector3(0, ty + 0.05f, 0), Vector3.one);
            for (int i = 0; i < 3; i++) Mat.Part(body, Mat.Sphere, gold, new Vector3(0, ty + 0.46f - i * 0.13f, 0.2f + i * 0.008f), Vector3.one * 0.045f);
            Mat.Part(body, Mat.Frustum(0.14f, 0.12f, 0.08f, 12), white, new Vector3(0, ty + 0.6f, 0), Vector3.one);
            // マフラー（属性の色）
            Mat.Part(body, Mat.Frustum(0.18f, 0.15f, 0.1f, 14), accent, new Vector3(0, ty + 0.59f, 0), Vector3.one);
            scarfA = Mat.Pivot(body, "scarfA", new Vector3(0.06f, ty + 0.58f, -0.13f));
            scarfB = Mat.Pivot(body, "scarfB", new Vector3(-0.06f, ty + 0.58f, -0.13f));
            Mat.Part(scarfA, Mat.Cube, accent, new Vector3(0, -0.3f, 0), new Vector3(0.1f, 0.6f, 0.03f));
            Mat.Part(scarfB, Mat.Cube, accent, new Vector3(0, -0.24f, 0), new Vector3(0.1f, 0.48f, 0.03f));

            // 頭（前より小さめ＝少しリアル寄りの頭身）
            head = Mat.Pivot(body, "head", new Vector3(0, ty + 0.63f, 0));
            Mat.Part(head, Mat.Sphere, skin, new Vector3(0, 0.22f, 0), new Vector3(0.44f, 0.5f, 0.46f));
            Mat.Part(head, Mat.Sphere, skin, new Vector3(0, 0.04f, 0), new Vector3(0.14f, 0.14f, 0.14f)); // 首
            Mat.Part(head, Mat.Sphere, hair, new Vector3(0, 0.29f, -0.03f), new Vector3(0.5f, 0.5f, 0.5f));
            foreach (float x in new[] { -0.22f, 0.22f }) Mat.Part(head, Mat.Sphere, skin, new Vector3(x, 0.21f, -0.01f), new Vector3(0.06f, 0.1f, 0.06f)); // 耳
            if (def.Spiky)
            {
                // つんつん頭
                for (int i = 0; i < 7; i++)
                {
                    float a = i / 7f * Mathf.PI * 2;
                    Mat.Part(head, Mat.Frustum(0.09f, 0.0f, 0.22f, 5), hair, new Vector3(Mathf.Cos(a) * 0.15f, 0.47f, Mathf.Sin(a) * 0.15f - 0.03f), Vector3.one,
                        new Vector3(Mathf.Sin(a) * -35, 0, Mathf.Cos(a) * 35));
                }
            }
            for (int i = -2; i <= 2; i++)
                Mat.Part(head, Mat.Sphere, hair, new Vector3(i * 0.075f, 0.37f - Mathf.Abs(i) * 0.025f, 0.17f - Mathf.Abs(i) * 0.02f), new Vector3(0.13f, 0.2f, 0.09f), new Vector3(15, 0, i * -12));
            pony = Mat.Pivot(head, "pony", new Vector3(0, 0.34f, -0.22f));
            if (def.Id == 0)
            {
                Mat.Part(pony, Mat.Frustum(0.03f, 0.1f, 0.45f, 10), hair, new Vector3(0, -0.2f, -0.05f), Vector3.one, new Vector3(-20, 0, 0));
                Mat.Part(pony, Mat.Sphere, accent, Vector3.zero, Vector3.one * 0.08f);
                Mat.Part(head, Mat.Frustum(0.03f, 0.005f, 0.2f, 6), hair, new Vector3(0.02f, 0.56f, 0.05f), Vector3.one, new Vector3(-30, 0, -15));
            }
            else if (def.Id == 2)
            {
                // 長めの後ろ髪
                Mat.Part(pony, Mat.Cube, hair, new Vector3(0, -0.18f, 0.02f), new Vector3(0.4f, 0.4f, 0.1f));
            }
            // 目
            foreach (float x in new[] { -0.085f, 0.085f })
            {
                Mat.Part(head, Mat.Sphere, white, new Vector3(x, 0.22f, 0.195f), new Vector3(0.085f, 0.09f, 0.04f), default, false);
                Mat.Part(head, Mat.Sphere, eyeM, new Vector3(x, 0.217f, 0.208f), new Vector3(0.06f, 0.075f, 0.03f), default, false);
                Mat.Part(head, Mat.Sphere, black, new Vector3(x, 0.215f, 0.218f), new Vector3(0.028f, 0.035f, 0.015f), default, false);
                Mat.Part(head, Mat.Sphere, white, new Vector3(x + 0.016f, 0.237f, 0.222f), new Vector3(0.018f, 0.018f, 0.008f), default, false);
                Mat.Part(head, Mat.Cube, hair, new Vector3(x, 0.3f, 0.2f), new Vector3(0.085f, 0.016f, 0.02f), new Vector3(0, 0, x > 0 ? 8 : -8), false);
                if (def.Glasses) Mat.Part(head, Mat.Torus(0.05f, 0.008f, 16, 5), black, new Vector3(x, 0.22f, 0.23f), Vector3.one, default, false);
                if (def.Sleepy) Mat.Part(head, Mat.Sphere, skin, new Vector3(x, 0.245f, 0.212f), new Vector3(0.09f, 0.055f, 0.045f), default, false); // 半目
            }
            Mat.Part(head, Mat.Sphere, skin, new Vector3(0, 0.17f, 0.22f), new Vector3(0.04f, 0.06f, 0.04f), default, false); // 鼻
            Mat.Part(head, Mat.Cube, M(new Color(0.7f, 0.35f, 0.35f), 0), new Vector3(0, 0.09f, 0.215f), new Vector3(0.055f, 0.01f, 0.01f), default, false);

            // 腕
            armL = Mat.Pivot(body, "armL", new Vector3(-0.27f, ty + 0.55f, 0));
            armR = Mat.Pivot(body, "armR", new Vector3(0.27f, ty + 0.55f, 0));
            foreach (var a in new[] { armL, armR })
            {
                Mat.Part(a, Mat.Frustum(0.06f, 0.07f, 0.52f, 10), jacket, new Vector3(0, -0.26f, 0), Vector3.one);
                Mat.Part(a, Mat.Frustum(0.065f, 0.065f, 0.05f, 10), white, new Vector3(0, -0.51f, 0), Vector3.one);
                Mat.Part(a, Mat.Sphere, skin, new Vector3(0, -0.57f, 0), Vector3.one * 0.12f);
            }

            // 武器（+Z 方向に伸びる）
            swordPivot = Mat.Pivot(body, "weapon", new Vector3(0.27f, ty + 0.2f, 0.1f));
            var grip = Mat.Pivot(swordPivot, "grip", new Vector3(0, 0, 0.45f));
            var ec = def.ElemColor;
            Vector3 tipLocal;
            switch (def.Weapon)
            {
                case Weapon.Fist:
                    // 炎をまとった拳（ラグビーのグローブ風）
                    bladeMat = Mat.Toon(new Color(1f, 0.55f, 0.3f), 0.01f, new Color(1f, 0.45f, 0.15f, 0.5f));
                    mats.Add(bladeMat);
                    Mat.Part(grip, Mat.Sphere, bladeMat, Vector3.zero, Vector3.one * 0.2f);
                    tipLocal = new Vector3(0, 0, 0.05f);
                    break;
                case Weapon.Spear:
                    // 槍：長い柄＋氷の穂先（数式の飾りつき）
                    bladeMat = Mat.Toon(new Color(0.8f, 0.95f, 1f), 0.01f, new Color(0.55f, 0.85f, 1f, 0.35f));
                    mats.Add(bladeMat);
                    var shaft = M(new Color(0.2f, 0.22f, 0.3f), 0.01f);
                    Mat.Part(grip, Mat.Frustum(0.03f, 0.03f, 2.3f, 8), shaft, new Vector3(0, 0, 0.45f), Vector3.one, new Vector3(90, 0, 0));
                    Mat.Part(grip, Mat.Cube, gold, new Vector3(0, 0, 1.6f), new Vector3(0.3f, 0.045f, 0.05f));
                    Mat.Part(grip, Mat.Sphere, M(ec, 0), new Vector3(0, 0, 1.6f), Vector3.one * 0.08f);
                    Mat.Part(grip, Mat.Frustum(0.1f, 0.0f, 0.5f, 4), bladeMat, new Vector3(0, 0, 1.87f), new Vector3(1, 1, 0.3f), new Vector3(90, 0, 0));
                    Mat.Part(grip, Mat.Frustum(0.035f, 0.035f, 0.12f, 8), gold, new Vector3(0, 0, -0.72f), Vector3.one, new Vector3(90, 0, 0));
                    tipLocal = new Vector3(0, 0, 2.05f);
                    break;
                case Weapon.Bow:
                    // 弓：持ち手・上下の弓幹・弦（縦向き）
                    bladeMat = Mat.Toon(new Color(0.85f, 1f, 0.9f), 0.01f, new Color(0.5f, 1f, 0.7f, 0.3f));
                    mats.Add(bladeMat);
                    var wood = M(new Color(0.35f, 0.25f, 0.18f), 0.01f);
                    Mat.Part(grip, Mat.Frustum(0.035f, 0.035f, 0.22f, 8), wood, Vector3.zero, Vector3.one);
                    Mat.Part(grip, Mat.Frustum(0.03f, 0.015f, 0.62f, 8), bladeMat, new Vector3(0, 0.36f, 0.08f), Vector3.one, new Vector3(-18, 0, 0));
                    Mat.Part(grip, Mat.Frustum(0.015f, 0.03f, 0.62f, 8), bladeMat, new Vector3(0, -0.36f, 0.08f), Vector3.one, new Vector3(18, 0, 0));
                    Mat.Part(grip, Mat.Cube, white, new Vector3(0, 0, -0.03f), new Vector3(0.008f, 1.28f, 0.008f), default, false);
                    tipLocal = new Vector3(0, 0, 0.2f);
                    break;
                default:
                    Mat.Part(grip, Mat.Frustum(0.035f, 0.035f, 0.26f, 8), M(new Color(0.25f, 0.18f, 0.3f), 0.01f), new Vector3(0, 0, -0.08f), Vector3.one, new Vector3(90, 0, 0));
                    Mat.Part(grip, Mat.Cube, gold, new Vector3(0, 0, 0.07f), new Vector3(0.3f, 0.06f, 0.07f));
                    Mat.Part(grip, Mat.Sphere, M(ec, 0), new Vector3(0, 0, 0.07f), Vector3.one * 0.09f);
                    bladeMat = Mat.Toon(new Color(0.85f, 0.8f, 1f), 0.01f, new Color(0.7f, 0.5f, 1f, 0.3f));
                    mats.Add(bladeMat);
                    Mat.Part(grip, Mat.Cube, bladeMat, new Vector3(0, 0, 0.62f), new Vector3(0.1f, 0.025f, 1.05f));
                    Mat.Part(grip, Mat.Frustum(0.05f, 0.0f, 0.16f, 4), bladeMat, new Vector3(0, 0, 1.22f), new Vector3(1, 1, 0.25f), new Vector3(90, 0, 0));
                    tipLocal = new Vector3(0, 0, 1.2f);
                    break;
            }
            var tip = Mat.Pivot(grip, "tip", tipLocal);
            trail = tip.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.14f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = def.Weapon == Weapon.Fist ? 0.5f : def.Weapon == Weapon.Bow ? 0f : def.Weapon == Weapon.Spear ? 0.45f : 0.9f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(ec, 0.3f), new GradientColorKey(ec * 0.6f, 1) },
                      new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
            trail.colorGradient = g;
            trail.sharedMaterial = Mat.Fx(Color.white, Mat.White, true, 2f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;

            var sh = Mat.Part(transform, Mat.Disc(24), Mat.Fx(new Color(0, 0, 0, 0.35f), Mat.SoftGlow, false), new Vector3(0, 0.03f, 0), new Vector3(0.5f, 1, 0.5f), default, false);
            sh.name = "blob";
        }

        public void ResetState()
        {
            Pos = new Vector3(0, 0, -9);
            Vy = 0; Face = 0; Inv = 0; Dodge = 0; SkillCd = 0; Energy = 60;
            HurtT = 0; BuffT = 0; BurstT = 0; DeadT = 0; OnGround = true; Dead = false; Victory = false; SwapInT = 0; LockT = 0; knock = Vector3.zero;
            swing = null; comboIdx = 0; comboTimer = 0; queued = false; elemIcd = 0; ShootT = 0; ShootUp = false; ThrustT = 0;
            transform.position = Pos;
            body.localRotation = Quaternion.identity;
            body.localPosition = Vector3.zero;
            trail.Clear();
        }

        // 交代で出てきたとき
        public void EnterField(Vector3 pos, float face)
        {
            Pos = pos; Face = face; Vy = 0; OnGround = pos.y <= 0.01f;
            swing = null; Dodge = 0; queued = false; comboIdx = 0; knock = Vector3.zero; HurtT = 0; LockT = 0;
            SwapInT = 0.35f;
            Inv = Mathf.Max(Inv, 0.4f);
            lastPos = Pos;
            transform.position = Pos;
            trail.Clear();
            trail.emitting = false;
        }

        // 控えにいる間もクールタイムは進む
        public void TickOffField(float dt)
        {
            SkillCd = Mathf.Max(0, SkillCd - dt);
            BuffT = Mathf.Max(0, BuffT - dt);
            elemIcd = Mathf.Max(0, elemIcd - dt);
        }

        // ================= 行動 =================
        float FaceForAttack()
        {
            var B = Game.I.Boss;
            if (B.Alive && Flat(B.Pos - Pos).magnitude < 12) return Mathf.Atan2(B.Pos.x - Pos.x, B.Pos.z - Pos.z);
            return Game.I.Cam.Yaw;
        }

        void StartSwing()
        {
            swing = new Swing { t = 0, dur = Def.SwingDur[comboIdx], idx = comboIdx, face = FaceForAttack() };
            bool fin = comboIdx == LastIdx;
            if (Def.Weapon == Weapon.Bow) Sfx.Play("arrow", 0.7f, fin ? 0.85f : 1f, 0.08f);
            else Sfx.Play(fin ? "slash3" : "slash" + (comboIdx % 3), 0.8f, (Def.Weapon == Weapon.Fist ? 0.75f : Def.Weapon == Weapon.Spear ? 1.15f : 1f) * (fin ? 0.9f : 1f), 0.08f);
            trail.Clear();
            trail.emitting = true;
            queued = false;
        }

        // 通常攻撃の属性は、一定間隔でだけ付く（毎回付くと反応が起きすぎる）
        Elem NormalElem()
        {
            if (elemIcd > 0) return Elem.None;
            elemIcd = 2.5f;
            return Def.Elem;
        }

        void MeleeHit(Swing sw)
        {
            var G = Game.I; var B = G.Boss;
            if (Def.Weapon == Weapon.Bow)
            {
                // やましょうは「ねみー」と言いながら矢を撃つ
                bool last = sw.idx == LastIdx;
                G.Hud.WorldText(Pos + Vector3.up * 2.4f, last ? "ねみぃぃ……" : "ねみー", new Color(0.8f, 1f, 0.9f), 0.6f);
                Skills.Arrow(this, Def.SwingDmg[sw.idx] * Def.AtkMul, last ? 3 : 1, NormalElem());
                return;
            }
            Vector3 fwd = new Vector3(Mathf.Sin(Face), 0, Mathf.Cos(Face));
            var tipPos = Pos + fwd * 1.5f + Vector3.up * 1.1f;
            Fx.Sparks(tipPos, Color.Lerp(Def.ElemColor, Color.white, 0.4f), 5, 0.6f);
            bool fin = sw.idx == LastIdx;
            float d = Flat(B.Pos - Pos).magnitude;
            float dir = Mathf.Atan2(B.Pos.x - Pos.x, B.Pos.z - Pos.z);
            bool inArc = AngDiff(Face, dir) < (fin && Def.Weapon != Weapon.Fist ? Mathf.PI : 1.9f);
            // リーチ：弓以外は長め（剣と拳は +1.3、槍はさらに長い）
            float reach = Boss.Radius + (fin ? 3.2f : 2.5f) + 1.3f - (Def.Weapon == Weapon.Fist ? 0.3f : 0) + (Def.Weapon == Weapon.Spear ? 1.1f : 0);
            // 届く範囲が見えるように、剣圧（斬撃の光）を前に飛ばす
            if (Def.Weapon != Weapon.Fist) Fx.SlashLine(Pos + fwd * (Def.Weapon == Weapon.Spear ? 3.2f : 2.4f) + Vector3.up * 1.1f, Color.Lerp(Def.ElemColor, Color.white, 0.35f), fin ? 6.5f : 4f);
            if (B.Alive && d < reach && inArc && B.Y < 2.5f)
            {
                var hitPos = B.Pos + Vector3.up * 2.2f - Flat(B.Pos - Pos).normalized * Boss.Radius;
                G.DamageBoss(Def.SwingDmg[sw.idx] * Def.AtkMul, Game.HitKind.Normal, hitPos, NormalElem());
                if (fin) { G.Cam.Shake(0.25f); Fx.Ring(Pos + fwd * 1.5f, 3.5f, Def.ElemColor, 0.3f); }
            }
            else if (fin) Fx.Ring(Pos + fwd * 1.2f, 3.2f, Def.ElemColor, 0.3f);
            if (Def.Weapon == Weapon.Spear)
            {
                Fx.Sparks(Pos + fwd * 2.6f + Vector3.up * 1.2f, Color.Lerp(Def.ElemColor, Color.white, 0.5f), fin ? 14 : 5, 0.7f);
                // らいとは、ときどき自慢する
                if (fin && Random.value < 0.35f) G.Hud.WorldText(Pos + Vector3.up * 2.4f, Random.value < 0.5f ? "ぼくてんさいだから！" : "ぼくは数学の神だよ！", Def.ElemColor, 0.8f);
            }
            if (Def.Weapon == Weapon.Fist) { Fx.Explosion(Pos + fwd * 2.2f + Vector3.up * 1.1f, Def.ElemColor, fin ? 1.1f : 0.5f); Sfx.Play("fire", fin ? 0.6f : 0.3f, 1.3f); }
        }

        void TryDodge()
        {
            if (Dodge > 0 || Stam < DodgeCost) { if (Stam < DodgeCost) Game.I.Hud.NoStamina(); return; }
            Stam -= DodgeCost; Game.I.StamDelay = 0.6f;
            Dodge = 0.3f; DodgeAge = 0; perfectUsed = false;
            swing = null; trail.emitting = false;
            var mv = GameInput.MoveVector(Game.I.Cam.Yaw);
            dodgeDir = mv.sqrMagnitude > 0 ? mv : new Vector3(-Mathf.Sin(Face), 0, -Mathf.Cos(Face));
            Face = Mathf.Atan2(dodgeDir.x, dodgeDir.z);
            Sfx.Play("whoosh", 0.8f);
            Fx.Sparks(Pos + Vector3.up * 0.8f, Def.ElemColor, 8, 0.5f);
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
            SkillCd = SkillCdMax;
            swing = null; trail.emitting = false;
            Inv = Mathf.Max(Inv, 0.3f);
            Game.I.Hud.SkillName(Def.SkillName, Def.ElemColor);
            switch (Def.Id)
            {
                case 0: Skills.Report(this); break;
                case 1: Skills.RugbyPass(this); break;
                case 3: Skills.QED(this); break;
                default: Skills.NidoneArrow(this); break;
            }
        }

        void TryBurst()
        {
            if (Energy < 100) return;
            Energy = 0;
            BurstT = 1.25f;
            Inv = Mathf.Max(Inv, 3f);
            swing = null; trail.emitting = false;
            Game.I.StartBurst(this);
        }

        // ================= 毎フレーム =================
        public void Tick(float dt)
        {
            var G = Game.I;
            Inv = Mathf.Max(0, Inv - dt);
            HurtT = Mathf.Max(0, HurtT - dt);
            SkillCd = Mathf.Max(0, SkillCd - dt);
            BuffT = Mathf.Max(0, BuffT - dt);
            ShootT = Mathf.Max(0, ShootT - dt);
            ThrustT = Mathf.Max(0, ThrustT - dt);
            SwapInT = Mathf.Max(0, SwapInT - dt);
            LockT = Mathf.Max(0, LockT - dt);
            elemIcd = Mathf.Max(0, elemIcd - dt);
            if (G.State == Game.Mode.Battle && !Dead) Energy = Mathf.Min(100, Energy + dt * 1.2f);

            Vy -= 26 * dt;
            Pos.y += Vy * dt;
            if (Pos.y <= 0)
            {
                if (!OnGround && Vy < -6) { Sfx.Play("land", 0.5f); Fx.Debris(Pos, new Color(0.85f, 0.8f, 0.7f, 0.7f), 4); }
                Pos.y = 0; Vy = 0; OnGround = true;
            }

            Moving = false;
            if (knock.sqrMagnitude > 0.01f) { Pos += knock * dt; knock = Vector3.Lerp(knock, Vector3.zero, dt * 6); }
            float spd = G.SlowT > 0 ? 0.5f : 1f;
            if (BurstT > 0)
            {
                BurstT -= dt;
                if (BurstT <= 0) G.FireBurst(this);
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
                    Pos += dodgeDir * 19 * spd * dt;
                    if (Random.value < 0.6f) Fx.Embers(Pos + Vector3.up * Random.Range(0.3f, 1.5f), Def.ElemColor, 1);
                }
                else if (swing != null) Pos += mv * 1.2f * spd * dt;
                else if (mv.sqrMagnitude > 0)
                {
                    Pos += mv * 6.8f * Def.MoveMul * spd * dt;
                    Face = TurnTo(Face, Mathf.Atan2(mv.x, mv.z), dt * 14);
                    Moving = true;
                }

                if (swing != null)
                {
                    var sw = swing;
                    sw.t += dt;
                    Face = TurnTo(Face, sw.face, dt * 20);
                    if (sw.idx == LastIdx && sw.t < sw.dur * 0.4f) Pos += new Vector3(Mathf.Sin(Face), 0, Mathf.Cos(Face)) * 5f * dt;
                    if (!sw.hit && sw.t >= sw.dur * 0.45f) { sw.hit = true; MeleeHit(sw); }
                    if (sw.t >= sw.dur)
                    {
                        swing = null;
                        trail.emitting = false;
                        comboTimer = 0.55f;
                        comboIdx = (sw.idx + 1) % Def.SwingDur.Length;
                    }
                    else if (sw.t > sw.dur * 0.6f && (queued || GameInput.Held(GameInput.K.Attack)) && sw.idx < LastIdx)
                    {
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
            // ボスの体にめりこまない（ダブルのときは2人とも）
            foreach (var B in G.Bosses)
            {
                if (!B.gameObject.activeSelf) continue;
                var dv = Flat(Pos - B.Pos);
                float min = Boss.Radius + 0.45f;
                if (B.Alive && dv.magnitude < min && B.Y < 1.5f)
                {
                    var c = B.Pos + (dv.sqrMagnitude > 1e-4f ? dv.normalized : Vector3.back) * min;
                    Pos.x = c.x; Pos.z = c.z;
                }
            }

            if (Dead) DeadT += dt;
            Animate(dt);
        }

        // 攻撃を受けたとき。当たったら true
        public bool TakeHit(float amount) => TakeHit(amount, Game.I.Boss.Pos);
        public bool TakeHit(float amount, Vector3 from)
        {
            var G = Game.I;
            if (Dead || Victory || G.State != Game.Mode.Battle) return false;
            if (Dodge > 0)
            {
                // ジャスト回避は回避の出だし 0.1 秒だけ。無敵は 0.22 秒まで
                if (!perfectUsed && DodgeAge < 0.1f) { perfectUsed = true; G.PerfectDodge(); return false; }
                if (DodgeAge < 0.28f) return false;
            }
            if (Inv > 0) return false;
            amount *= Game.I.EnemyDmgMul;
            Hp = Mathf.Max(0, Hp - amount);
            Inv = 1.1f; HurtT = 0.4f;
            // ノックバック：攻撃の来た方向から吹き飛ばされる
            var away = Flat(Pos - from);
            if (away.sqrMagnitude < 0.01f) away = -Forward;
            knock = away.normalized * (7f + amount * 0.02f);
            Vy = Mathf.Max(Vy, 4.5f); OnGround = false;
            Face = Mathf.Atan2(-away.x, -away.z);
            Dodge = 0;
            flash = 1; flashC = new Color(1, 0.3f, 0.3f);
            swing = null; trail.emitting = false;
            G.OnPlayerHurt(amount);
            if (Hp <= 0) { Dead = true; G.OnPlayerDown(); }
            return true;
        }

        public Vector3 HandPos => swordPivot.position;
        public Vector3 Forward => new Vector3(Mathf.Sin(Face), 0, Mathf.Cos(Face));

        // ================= 見た目の動き =================
        void Animate(float dt)
        {
            transform.position = Pos;
            float moveSpeed = (Flat(Pos - lastPos).magnitude) / Mathf.Max(dt, 1e-4f);
            lastPos = Pos;
            idleT += dt;

            float lean = 0, bob = 0, legSwing = 0, armSwing = 0;
            Quaternion armLRot = Quaternion.Euler(0, 0, -8), armRRot = Quaternion.Euler(0, 0, 8);
            Quaternion swordRot = Quaternion.Euler(125, 0, -20);
            float spin = 0, ext = 0;
            bool bowHeld = false;
            // 弓を構える：左手で弓を前に出し、右手で弦を引く（pull 0〜1）
            void BowPose(float pull, bool up)
            {
                float lift = up ? -45 : 0;
                armLRot = Quaternion.Euler(-90 + lift, 0, 0);
                armRRot = Quaternion.Euler(-90 + lift, Mathf.Lerp(-20, 75, pull), 0);
                swordRot = Quaternion.Euler(lift, 0, 0);
                lean = up ? -12 : -4;
                bowHeld = true;
            }

            if (Dead)
            {
                float u = Mathf.Clamp01(DeadT / 0.6f);
                body.localRotation = Quaternion.Euler(0, Face * Mathf.Rad2Deg, 0) * Quaternion.Euler(-80 * u, 0, 0);
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
                float u = 1 - BurstT / 1.25f;
                armRRot = Quaternion.Euler(0, 0, 170);
                swordRot = Quaternion.Euler(-90, 0, 0);
                bob = Mathf.Sin(u * Mathf.PI) * 0.6f;
                if (Random.value < 0.8f) Fx.Sparks(transform.position + Vector3.up * 3.2f, Color.Lerp(Def.ElemColor, Color.white, 0.4f), 2, 0.6f);
            }
            else if (Def.Weapon == Weapon.Bow && ShootT > 0 && swing == null)
            {
                // 連射：弦を引いて放すのをくり返す
                BowPose(Mathf.Repeat(idleT * 7f, 1f), ShootUp);
            }
            else if (Def.Weapon == Weapon.Spear && ThrustT > 0 && swing == null)
            {
                // 特技の突進：槍をまっすぐ前へ
                swordRot = Quaternion.Euler(6, 0, 0);
                ext = 0.6f; lean = 25; legSwing = 30;
                armRRot = Quaternion.FromToRotation(Vector3.down, (Vector3.forward + Vector3.down * 0.3f).normalized);
            }
            else if (Dodge > 0)
            {
                lean = 35; legSwing = 40;
                swordRot = Quaternion.Euler(150, 0, 0);
            }
            else if (swing != null)
            {
                float u = Mathf.Clamp01(swing.t / swing.dur);
                float e = u < 0.45f ? Mathf.SmoothStep(0, 1, u / 0.45f) : 1;
                int pattern;
                bool fin = swing.idx == LastIdx;
                if (Def.Weapon == Weapon.Bow) pattern = 5;
                else if (Def.Weapon == Weapon.Spear) pattern = fin ? 3 : swing.idx == 2 ? 0 : 6;
                else if (fin) pattern = Def.Weapon == Weapon.Fist ? 2 : 3;
                else pattern = swing.idx % 2;
                switch (pattern)
                {
                    case 0: swordRot = Quaternion.Euler(0, Mathf.Lerp(110, -80, e), -30); lean = 10; break;
                    case 1: swordRot = Quaternion.Euler(0, Mathf.Lerp(-100, 90, e), 25); lean = 10; break;
                    case 2: swordRot = Quaternion.Euler(Mathf.Lerp(-120, 50, e), -5, 0); lean = Mathf.Lerp(-10, 25, e); break;
                    case 5: BowPose(u < 0.45f ? u / 0.45f : Mathf.Max(0, 1 - (u - 0.45f) * 6), false); break; // 弓を引いて放す
                    case 6:
                        // 槍の突き：引いてから前へ突き出す
                        float th = u < 0.3f ? -0.3f * (u / 0.3f) : Mathf.Sin(Mathf.Clamp01((u - 0.3f) / 0.5f) * Mathf.PI) * 0.8f;
                        swordRot = Quaternion.Euler(6, swing.idx % 2 == 0 ? -4 : 4, 0);
                        ext = th; lean = 8 + th * 18;
                        break;
                    default:
                        spin = Mathf.Lerp(0, 360, Mathf.SmoothStep(0, 1, Mathf.Clamp01(u / 0.55f)));
                        swordRot = Quaternion.Euler(10, 70, 0);
                        armLRot = Quaternion.Euler(0, 0, -70);
                        bob = Mathf.Sin(Mathf.Clamp01(u / 0.55f) * Mathf.PI) * 0.35f;
                        break;
                }
                if (!bowHeld)
                {
                    var dir = swordRot * Vector3.forward;
                    armRRot = Quaternion.FromToRotation(Vector3.down, (dir + Vector3.down * 0.3f).normalized);
                }
            }
            else if (!OnGround)
            {
                armLRot = Quaternion.Euler(0, 0, -50); armRRot = Quaternion.Euler(0, 0, 50);
                hipL.localRotation = Quaternion.Euler(-40, 0, 0);
                hipR.localRotation = Quaternion.Euler(-10, 0, 0);
            }
            else if (moveSpeed > 1f)
            {
                walkT += dt * 11 * (Game.I.SlowT > 0 ? 0.6f : 1f) * Mathf.Sqrt(Def.MoveMul);
                legSwing = Mathf.Sin(walkT) * 40;
                armSwing = Mathf.Sin(walkT) * 35;
                bob = Mathf.Abs(Mathf.Sin(walkT)) * 0.08f;
                lean = 12;
                if (Mathf.Sin(walkT) * Mathf.Sin(walkT - dt * 11) < 0) Sfx.Play("step", 0.3f, 1, 0.15f);
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
            if (swing == null && BurstT <= 0 && !bowHeld && ThrustT <= 0 && !Victory && !Dead && moveSpeed > 1f && OnGround)
            {
                armLRot = Quaternion.Euler(-armSwing, 0, -8);
                armRRot = Quaternion.Euler(armSwing * 0.5f, 0, 10);
            }
            bool snap = swing != null || bowHeld || ThrustT > 0;
            armL.localRotation = bowHeld ? Quaternion.Slerp(armL.localRotation, armLRot, dt * 30) : Quaternion.Slerp(armL.localRotation, armLRot, dt * 18);
            armR.localRotation = snap ? armRRot : Quaternion.Slerp(armR.localRotation, armRRot, dt * 18);
            swordPivot.localRotation = snap ? swordRot : Quaternion.Slerp(swordPivot.localRotation, swordRot, dt * 14);
            // 弓を構えている間は左手で持つ。それ以外は右手
            var holdArm = bowHeld ? armL : armR;
            swordPivot.localPosition = holdArm.localPosition + holdArm.localRotation * new Vector3(0, -0.57f, 0) - swordPivot.localRotation * new Vector3(0, 0, 0.45f - ext);

            float target = Mathf.Clamp(moveSpeed * 5, 0, 70) + Mathf.Sin(idleT * 5) * 6;
            scarfSwing = Mathf.Lerp(scarfSwing, target, dt * 6);
            scarfA.localRotation = Quaternion.Euler(scarfSwing + 10, 0, Mathf.Sin(idleT * 7) * 8);
            scarfB.localRotation = Quaternion.Euler(scarfSwing * 0.8f + 12, 0, -Mathf.Sin(idleT * 6) * 8);
            pony.localRotation = Quaternion.Euler(-scarfSwing * 0.5f, Mathf.Sin(idleT * 3) * 10, 0);
            head.localRotation = Quaternion.Euler(HurtT > 0 ? -15 : 0, 0, 0);

            // 交代で出てきた瞬間は少し大きく見せる
            transform.localScale = Vector3.one * (1 + SwapInT * 0.4f);

            var ec = Def.ElemColor;
            float glowA = 0.3f + (BuffT > 0 ? 0.4f + 0.15f * Mathf.Sin(idleT * 20) : 0) + (swing != null ? 0.2f : 0);
            bladeMat.SetColor("_Emission", new Color(ec.r, ec.g, ec.b, glowA));
            if (Def.Weapon == Weapon.Fist && Random.value < 0.35f) Fx.Embers(swordPivot.position + swordPivot.forward * 0.1f, ec, 1);
            if (BuffT > 0 && Random.value < 0.3f) Fx.Embers(transform.position + Vector3.up * Random.Range(0.5f, 1.8f) + Random.insideUnitSphere * 0.4f, ec, 1);

            flash = Mathf.MoveTowards(flash, 0, dt * 5);
            float blink = (Inv > 0 && HurtT <= 0 && BurstT <= 0 && SwapInT <= 0 && Game.I.State == Game.Mode.Battle && Mathf.Sin(Time.time * 50) > 0.6f) ? 0.35f : 0;
            float swapGlow = SwapInT > 0 ? SwapInT / 0.35f * 0.7f : 0;
            Mat.SetFlash(mats, Mathf.Max(Mathf.Max(flash, blink), swapGlow), flash > 0 ? flashC : swapGlow > 0 ? ec : Color.white);
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
