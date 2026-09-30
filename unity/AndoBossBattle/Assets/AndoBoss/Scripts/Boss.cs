using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AndoBoss
{
    // ボス：電気回路担当・安東先生／英語担当・菅原先生（どちらも架空の人物）
    // 菅原先生の見た目・攻撃・セリフは BossSuga.cs
    public partial class Boss : MonoBehaviour
    {
        public const float MaxHp = 16000f;
        // ダブル（2人同時）のときは1人あたりのHPを減らす
        public const float DoubleHp = 11000f;
        public float HpMax = MaxHp;
        public Vector3 HomePos = new Vector3(0, 0, 7);
        public const float Radius = 1.7f;
        public const float MaxTough = 4200f;

        public int Kind; // 0 = 安東先生, 1 = 菅原先生
        public bool IsSuga => Kind == 1;
        public string Name => IsSuga ? "菅原先生" : "安東先生";
        public Vector3 Pos;
        public float Y, Face, Hp, LagHp, Tough, BreakT, Flash, SinkT, FreezeT, DefDownT;
        public float ProvenT; // らいとの奥義「証明済み」：受けるダメージ1.5倍
        public Elem Aura; public float AuraT;
        public int Phase = 1;
        public bool Alive => Hp > 0;
        public bool Broken => BreakT > 0;
        public bool PendingPhase;
        public string Pose = "idle";
        bool said75, said25, lockFace, walking;
        float sansouCd = 20f, practiceCd;
        float restT, animT, walkT;
        string lastAtk;
        Func<float, bool> atk;

        // 見た目
        Transform inner, armL, armR, legL, legR, headT, bookOrbit, stickTip;
        GameObject aura, dizzy, iceBlock;
        Material auraMat, eyeGlow;
        readonly List<Material> mats = new List<Material>();
        ParticleSystem auraPs;

        public void Build()
        {
            BuildModel();
            BuildCommon();
        }

        // ボスを切り替える（モデルを作り直す）
        public void SetKind(int k)
        {
            if (k == Kind && inner != null) return;
            Kind = k;
            if (dizzy) dizzy.transform.SetParent(transform, false);
            BuildModel();
            if (dizzy) { dizzy.transform.SetParent(headT, false); dizzy.transform.localPosition = new Vector3(0, 2.1f, 0); }
            ResetState();
        }

        void BuildModel()
        {
            if (inner != null) Destroy(inner.gameObject);
            foreach (var m in mats) if (m) Destroy(m);
            mats.Clear();
            if (IsSuga) { BuildSuga(); return; }
            inner = Mat.Pivot(transform, "inner", Vector3.zero);
            Material M(Color c, float o = 0.04f) { var m = Mat.Toon(c, o); mats.Add(m); return m; }
            var skin = M(new Color(0.95f, 0.82f, 0.69f));
            var suit = M(new Color(0.23f, 0.25f, 0.3f));
            var shirt = M(new Color(0.96f, 0.96f, 0.96f));
            var tie = M(new Color(0.75f, 0.22f, 0.18f));
            var hairM = M(new Color(0.93f, 0.93f, 0.95f)); // 白髪（はげてはいない）
            var black = M(new Color(0.12f, 0.12f, 0.12f), 0);

            legL = Mat.Pivot(inner, "legL", new Vector3(-0.4f, 1.4f, 0));
            legR = Mat.Pivot(inner, "legR", new Vector3(0.4f, 1.4f, 0));
            foreach (var l in new[] { legL, legR })
            {
                Mat.Part(l, Mat.Frustum(0.24f, 0.28f, 1.4f, 12), suit, new Vector3(0, -0.7f, 0), Vector3.one);
                Mat.Part(l, Mat.Sphere, black, new Vector3(0, -1.38f, 0.12f), new Vector3(0.5f, 0.25f, 0.75f));
            }
            Mat.Part(inner, Mat.Frustum(1.05f, 0.85f, 2.0f, 18), suit, new Vector3(0, 2.3f, 0), Vector3.one);
            Mat.Part(inner, Mat.Cube, shirt, new Vector3(0, 2.75f, 0.86f), new Vector3(0.55f, 1.1f, 0.1f), new Vector3(-6, 0, 0));
            Mat.Part(inner, Mat.Cube, tie, new Vector3(0, 2.65f, 0.93f), new Vector3(0.2f, 0.9f, 0.08f), new Vector3(-6, 0, 0));
            // 襟
            Mat.Part(inner, Mat.Cube, suit, new Vector3(-0.32f, 2.95f, 0.8f), new Vector3(0.28f, 0.7f, 0.08f), new Vector3(-8, 0, -25));
            Mat.Part(inner, Mat.Cube, suit, new Vector3(0.32f, 2.95f, 0.8f), new Vector3(0.28f, 0.7f, 0.08f), new Vector3(-8, 0, 25));

            headT = Mat.Pivot(inner, "head", new Vector3(0, 3.3f, 0));
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.8f, 0), Vector3.one * 1.9f);
            // 髪（頭頂は少し薄め、横は厚め）
            Mat.Part(headT, Mat.Sphere, hairM, new Vector3(0, 0.98f, -0.1f), new Vector3(2.02f, 1.7f, 1.98f));
            // 前髪とふくらみ（ふさふさの白髪）
            for (int i = -3; i <= 3; i++)
                Mat.Part(headT, Mat.Sphere, hairM, new Vector3(i * 0.24f, 1.62f - Mathf.Abs(i) * 0.06f, 0.55f - Mathf.Abs(i) * 0.08f), new Vector3(0.5f, 0.42f, 0.4f), new Vector3(20, 0, i * -8));
            for (int i = 0; i < 5; i++)
                Mat.Part(headT, Mat.Sphere, hairM, new Vector3((i - 2) * 0.35f, 1.35f, -0.75f), new Vector3(0.6f, 0.8f, 0.5f));
            foreach (float x in new[] { -0.85f, 0.85f })
                Mat.Part(headT, Mat.Sphere, hairM, new Vector3(x, 0.75f, -0.1f), new Vector3(0.42f, 0.7f, 0.7f));
            // メガネ
            var frameMesh = Mat.Torus(0.24f, 0.045f, 20, 6);
            foreach (float x in new[] { -0.33f, 0.33f })
            {
                Mat.Part(headT, frameMesh, black, new Vector3(x, 0.82f, 0.86f), Vector3.one, default, false);
                Mat.Part(headT, Mat.Sphere, black, new Vector3(x, 0.8f, 0.84f), Vector3.one * 0.14f, default, false);
                Mat.Part(headT, Mat.Cube, M(new Color(0.8f, 0.8f, 0.82f), 0), new Vector3(x, 1.15f, 0.84f), new Vector3(0.36f, 0.09f, 0.08f), new Vector3(0, 0, x < 0 ? -20 : 20), false);
            }
            // レンズの光（第2形態で赤く光る）
            eyeGlow = Mat.Fx(new Color(1, 0.2f, 0.2f, 0), Mat.Glow, true, 3f);
            foreach (float x in new[] { -0.33f, 0.33f })
            {
                var e = Mat.Part(headT, Mat.Quad, eyeGlow, new Vector3(x, 0.82f, 0.92f), Vector3.one * 0.6f, default, false);
                e.AddComponent<Billboard>();
            }
            Mat.Part(headT, Mat.Cube, black, new Vector3(0, 0.84f, 0.9f), new Vector3(0.2f, 0.04f, 0.04f), default, false);
            Mat.Part(headT, Mat.Cube, M(new Color(0.48f, 0.23f, 0.18f), 0), new Vector3(0, 0.32f, 0.86f), new Vector3(0.4f, 0.06f, 0.06f), new Vector3(0, 0, 3), false);
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.62f, 0.93f), new Vector3(0.18f, 0.22f, 0.18f));

            Transform MkArm(float x)
            {
                var p = Mat.Pivot(inner, "arm", new Vector3(x, 3.05f, 0));
                Mat.Part(p, Mat.Frustum(0.2f, 0.23f, 1.5f, 12), suit, new Vector3(0, -0.72f, 0), Vector3.one);
                Mat.Part(p, Mat.Frustum(0.21f, 0.21f, 0.12f, 12), shirt, new Vector3(0, -1.46f, 0), Vector3.one);
                Mat.Part(p, Mat.Sphere, skin, new Vector3(0, -1.6f, 0), Vector3.one * 0.48f);
                return p;
            }
            armL = MkArm(-1.05f); armR = MkArm(1.05f);
            // 指示棒
            var stick = Mat.Part(armR, Mat.Frustum(0.05f, 0.035f, 2.4f, 8), M(new Color(0.42f, 0.29f, 0.17f), 0.02f), new Vector3(0, -1.6f, 1.1f), Vector3.one, new Vector3(90, 0, 0));
            _ = stick;
            stickTip = Mat.Pivot(armR, "tip", new Vector3(0, -1.6f, 2.3f));
            Mat.Part(stickTip, Mat.Sphere, M(new Color(0.95f, 0.85f, 0.4f), 0.02f), Vector3.zero, Vector3.one * 0.14f);

            // 周りを回る教科書「電気回路 安東 著」
            bookOrbit = Mat.Pivot(inner, "bookOrbit", new Vector3(0, 3.2f, 0));
            var book = Mat.Pivot(bookOrbit, "book", new Vector3(2.4f, 0, 0));
            var coverTex = BookCover();
            Mat.Part(book, Mat.Cube, M(new Color(0.96f, 0.94f, 0.88f), 0.02f), Vector3.zero, new Vector3(0.9f, 1.25f, 0.2f));
            var cover = Mat.Toon(Color.white, 0, null, coverTex); mats.Add(cover);
            Mat.Part(book, Mat.Quad, cover, new Vector3(0, 0, -0.105f), new Vector3(0.92f, 1.27f, 1), default, false);
            var back = Mat.Toon(new Color(0.7f, 0.2f, 0.18f)); mats.Add(back);
            Mat.Part(book, Mat.Quad, back, new Vector3(0, 0, 0.105f), new Vector3(0.92f, 1.27f, 1), new Vector3(0, 180, 0), false);
        }

        void BuildCommon()
        {
            // 第2形態のオーラ
            auraMat = Mat.Fx(new Color(0.7f, 0.45f, 1f, 0.22f), Mat.SoftGlow, true, 1.5f);
            aura = Mat.Part(transform, Mat.Quad, auraMat, new Vector3(0, 2.6f, 0), Vector3.one * 8, default, false);
            aura.AddComponent<Billboard>();
            aura.SetActive(false);
            var psGo = new GameObject("auraPs");
            psGo.transform.SetParent(transform, false);
            psGo.transform.localPosition = new Vector3(0, 2.2f, 0);
            auraPs = psGo.AddComponent<ParticleSystem>();
            auraPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var mm = auraPs.main; mm.loop = true; mm.startLifetime = 0.8f; mm.startSpeed = 1.5f; mm.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            mm.startColor = new ParticleSystem.MinMaxGradient(Mat.Electro, new Color(1f, 0.5f, 0.8f)); mm.simulationSpace = ParticleSystemSimulationSpace.World; mm.gravityModifier = -0.4f;
            var em = auraPs.emission; em.rateOverTime = 40;
            var sh = auraPs.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.6f;
            var col = auraPs.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat.Fx(Color.white, Mat.Glow, true, 2.5f);

            // ブレイク中に頭の上を回る星
            dizzy = new GameObject("dizzy");
            dizzy.transform.SetParent(headT, false);
            dizzy.transform.localPosition = new Vector3(0, 2.1f, 0);
            var starM = Mat.Fx(Mat.Gold, Mat.Star, true, 2.5f);
            for (int i = 0; i < 4; i++)
            {
                float a = i / 4f * Mathf.PI * 2;
                var s = Mat.Part(dizzy.transform, Mat.Quad, starM, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.1f, Vector3.one * 0.7f, default, false);
                s.AddComponent<Billboard>();
            }
            dizzy.SetActive(false);

            // 氷づけ（今は使っていないが、演出用に残してある）
            iceBlock = new GameObject("ice");
            iceBlock.transform.SetParent(transform, false);
            var iceM = Mat.Fx(new Color(0.6f, 0.9f, 1f, 0.35f), Mat.White, false, 1f);
            var iceE = Mat.Fx(new Color(0.7f, 0.95f, 1f, 0.5f), Mat.WallTex, true, 1.5f);
            for (int i = 0; i < 5; i++)
            {
                var sc = new Vector3(Random.Range(1.6f, 2.4f), Random.Range(3f, 5.5f), Random.Range(1.6f, 2.4f));
                Mat.Part(iceBlock.transform, Mat.Cube, iceM, new Vector3(Random.Range(-0.8f, 0.8f), sc.y / 2, Random.Range(-0.8f, 0.8f)), sc, new Vector3(Random.Range(-10, 10), Random.Range(0, 90), Random.Range(-10, 10)), false);
            }
            Mat.Part(iceBlock.transform, Mat.Frustum(1, 1, 1, 24, false, true), iceE, new Vector3(0, 2.8f, 0), new Vector3(2.3f, 5.6f, 2.3f), default, false);
            iceBlock.SetActive(false);

            var blob = Mat.Part(transform, Mat.Disc(32), Mat.Fx(new Color(0, 0, 0, 0.4f), Mat.SoftGlow, false), new Vector3(0, 0.03f, 0), new Vector3(2.4f, 1, 2.4f), default, false);
            blob.name = "blob";
        }

        static Texture2D BookCover()
        {
            // 表紙：赤地に白い帯と稲妻マーク（文字はUIではないのでフォントを使わず模様で）
            return Mat.MakeTex(128, (x, y) =>
            {
                var red = new Color(0.72f, 0.2f, 0.18f);
                if (y > 0.62f && y < 0.8f) return new Color(0.97f, 0.95f, 0.9f);
                // 稲妻
                float cx = x - 0.5f, cy = y - 0.36f;
                bool bolt = (cy > 0 && cy < 0.16f && Mathf.Abs(cx - cy * 0.6f + 0.02f) < 0.05f) || (cy <= 0 && cy > -0.16f && Mathf.Abs(cx - cy * 0.6f - 0.02f) < 0.05f);
                if (bolt) return new Color(1f, 0.85f, 0.3f);
                if (y > 0.08f && y < 0.1f) return new Color(0.97f, 0.95f, 0.9f);
                return red;
            });
        }

        public void ResetState()
        {
            HpMax = (Game.IsDouble ? DoubleHp : MaxHp) * Game.BossHpMul;
            Pos = HomePos; Y = 0; Face = Mathf.PI; Hp = HpMax; LagHp = HpMax; Tough = MaxTough; BreakT = 0; Flash = 0; SinkT = 0;
            Phase = 1; PendingPhase = false; Pose = "idle"; FreezeT = 0; DefDownT = 0; ProvenT = 0; Aura = Elem.None; AuraT = 0; hakaiCd = 2f; PoseU = 0; sansouCd = 20f; practiceCd = 0; said75 = said25 = false; lockFace = false; walking = false;
            restT = 1.2f; lastAtk = null; atk = null;
            aura.SetActive(false);
            dizzy.SetActive(false);
            iceBlock.SetActive(false);
            auraPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            eyeGlow.SetColor("_Color", new Color(1, 0.2f, 0.2f, 0));
            inner.localRotation = Quaternion.identity;
            inner.localPosition = Vector3.zero;
            transform.position = Pos;
        }

        // ================= ダメージ・状態変化 =================
        public void OnDamaged(float dmg, float toughDmg)
        {
            Flash = 0.12f;
            float r = Hp / HpMax;
            if (Phase == 1 && r <= 0.5f) PendingPhase = true;
            if (!said75 && r <= 0.75f) { said75 = true; Speak(Line("hp75")); }
            if (!said25 && r <= 0.25f) { said25 = true; Speak(Line("hp25")); }
            if (!Broken && Hp > 0)
            {
                Tough -= toughDmg;
                if (Tough <= 0) StartBreak();
            }
        }

        void StartBreak()
        {
            Tough = 0;
            BreakT = 5f;
            atk = null; lockFace = false; Y = 0;
            Pose = "dizzy";
            dizzy.SetActive(true);
            Game.I.OnBreak(this);
        }

        public void Die()
        {
            atk = null; Pose = "defeat"; BreakT = 0; dizzy.SetActive(false);
            FreezeT = 0; iceBlock.SetActive(false);
            auraPs.Stop();
        }

        // ================= 行動 =================
        public void Tick(float dt)
        {
            var G = Game.I; var P = G.Player;
            Flash = Mathf.Max(0, Flash - dt);
            LagHp = Mathf.Lerp(LagHp, Hp, Mathf.Min(1, dt * 2.5f));
            animT += dt;

            if (!Alive)
            {
                SinkT += dt;
                Animate(dt);
                return;
            }
            if (G.State != Game.Mode.Battle || P.Dead)
            {
                if (G.State != Game.Mode.Battle) { Y = 0; }
                Animate(dt);
                return;
            }

            AuraT -= dt;
            DefDownT = Mathf.Max(0, DefDownT - dt);
            ProvenT = Mathf.Max(0, ProvenT - dt);
            if (ProvenT > 0 && Random.value < 0.35f) Fx.Stars(Pos + Vector3.up * Random.Range(1f, 5f) + Random.insideUnitSphere * 1.2f, new Color(0.6f, 0.88f, 1f), 1);
            if (AuraT <= 0) Aura = Elem.None;
            if (FreezeT > 0)
            {
                // 氷づけ：何もできない
                FreezeT -= dt;
                if (FreezeT <= 0) Freeze(0);
                Animate(0);
                return;
            }
            if (BreakT > 0)
            {
                BreakT -= dt;
                if (Random.value < 0.15f) Fx.Sparks(Pos + Vector3.up * Random.Range(1f, 4f), Mat.Gold, 2, 0.4f);
                if (BreakT <= 0)
                {
                    Tough = MaxTough * (Phase == 2 ? 1.2f : 1f);
                    Aura = Elem.None;
                    dizzy.SetActive(false);
                    Pose = "idle";
                    restT = 0.6f;
                    Speak(Line("breakEnd"));
                }
                Animate(dt);
                return;
            }

            float d = Player.Flat(P.Pos - Pos).magnitude;
            if (!lockFace) Face = Player.TurnTo(Face, Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z), dt * 3);

            if (atk != null)
            {
                if (!atk(dt))
                {
                    atk = null; Pose = "idle"; lockFace = false;
                    restT = (Phase == 2 ? Random.Range(0.45f, 0.85f) : Random.Range(0.8f, 1.4f)) * Game.BossRestMul;
                }
                Animate(dt);
                return;
            }
            if (PendingPhase)
            {
                PendingPhase = false;
                Phase = 2;
                atk = AtkRoar();
                Animate(dt);
                return;
            }
            restT -= dt;
            sansouCd -= dt;
            walking = false;
            if (d > 6)
            {
                float sp = (Phase == 2 ? 3.4f : 2.3f) * dt;
                Pos += Player.Flat(P.Pos - Pos).normalized * sp;
                walking = true;
            }
            if (restT <= 0)
            {
                if (IsSuga) { PickSugaAttack(); Animate(dt); return; }
                var opts = new List<string> { "lightning", "shots", "slam", "iyaiya", "trans" };
                if (Phase == 2) { opts.Add("laser"); opts.Add("spiral"); opts.Add("iyaiya"); }
                // 必殺「三相交流」はしばらく間をあけて使う
                if (sansouCd <= 0) { opts.Clear(); opts.Add("sansou"); }
                opts.RemoveAll(k => k == lastAtk);
                var pick = opts[Random.Range(0, opts.Count)];
                lastAtk = pick;
                walking = false;
                switch (pick)
                {
                    case "lightning": atk = AtkLightning(); break;
                    case "shots": atk = AtkShots(); break;
                    case "slam": atk = AtkSlam(); break;
                    case "iyaiya": atk = AtkIyaiya(); break;
                    case "trans": atk = AtkTrans(); break;
                    case "sansou": atk = AtkSansou(); sansouCd = Phase == 2 ? 16f : 26f; break;
                    case "laser": atk = AtkLaser(); break;
                    default: atk = AtkSpiral(); break;
                }
            }
            Animate(dt);
        }

        // セリフは全部秋田弁
        static readonly Dictionary<string, string[]> Lines = new Dictionary<string, string[]>
        {
            { "lightning", new[] { "抜き打ちの小テストだど！", "雷さ打たれだみてぇに覚えれ！" } },
            { "shots", new[] { "抵抗したって無駄だど。", "カラーコード、読めるが？" } },
            { "slam", new[] { "再履修だ！", "跳んでよげれ！" } },
            { "iyaiya", new[] { "いやいやいやいや！！", "いやいや、そうでねって！", "いやいやいや、違うべ！" } },
            { "trans", new[] { "トランスで電圧下げでやる！", "足、重ぐなったべ？" } },
            { "sansou", new[] { "必殺、三相交流！！", "120度ずつずれでるの、わがるが？" } },
            { "laser", new[] { "オームの法則ビームだ！", "V＝IR、覚えだべ？" } },
            { "spiral", new[] { "キルヒホッフの渦だ。", "入った電流は、ちゃんと出でいぐんだど。" } },
        };

        // 攻撃が当たったときに、たまに言う
        public void OnHitPlayer()
        {
            if (Time.time < practiceCd) return;
            if (Random.value < 0.35f) { practiceCd = Time.time + 9; Speak(Line("hit")); }
        }

        public void Freeze(float sec)
        {
            FreezeT = sec;
            if (sec > 0) { atk = null; lockFace = false; Y = 0; Pose = "idle"; }
            iceBlock.SetActive(sec > 0);
            // 菅原先生は背が高いので、氷も縦に大きく
            iceBlock.transform.localScale = IsSuga ? new Vector3(0.85f, 1.3f, 0.85f) : Vector3.one;
        }
        // 動作確認用：指定した攻撃をすぐに出す（スクリーンショットの自動撮影で使う）
        public void DebugAttack(string name)
        {
            restT = 99; walking = false;
            switch (name)
            {
                case "sansou": atk = AtkSansou(); break;
                case "hakai": atk = AtkHakai(); break;
                case "listen": atk = AtkListen(); break;
                case "redpen": atk = AtkRedPen(); break;
                case "sheets": atk = AtkSheets(); break;
            }
        }

        // セリフは自分の名前で言う（ダブルのときにどっちが話したか分かるように）
        void Speak(string text, float sec = 2.6f) => Game.I.Say(text, sec, Name);

        static string Pick(string k) { var a = Lines[k]; return a[Random.Range(0, a.Length)]; }

        // 場面ごとの決まったセリフ（ボスごと）
        static readonly Dictionary<string, string> AndoOne = new Dictionary<string, string>
        {
            { "intro", "……おめ、おれがら単位取るつもりだが？" },
            { "hp75", "まだまだ単位はやれねど。" },
            { "hp25", "……なかなか、やるでねが。" },
            { "phase2", "おれ、もう知らねがらな！" },
            { "break", "な……おれの理論が……！" },
            { "breakEnd", "……今のは見ねがったことにするがらな。" },
            { "win", "……しかたねな。単位、認めるべ。" },
            { "lose", "へば、また来年な。" },
            { "timeup", "時間だ。答案、回収するど。" },
            { "hit", "練習問題と同じでねが！" },
        };
        public string Line(string key) => IsSuga ? SugaOne[key] : AndoOne[key];

        Func<float, bool> AtkLightning()
        {
            var G = Game.I;
            Speak(Pick("lightning"));
            Pose = "raise";
            int n = Phase == 2 ? 7 : 4;
            float gap = Phase == 2 ? 0.14f : 0.2f;
            Sfx.Play("warn", 0.5f);
            for (int i = 0; i < n; i++)
            {
                var P = G.Player;
                var pos = i == 0 ? new Vector3(P.Pos.x, 0, P.Pos.z) : new Vector3(P.Pos.x + Random.Range(-5f, 5f), 0, P.Pos.z + Random.Range(-5f, 5f));
                pos = ClampArena(pos);
                Fx.TelegraphCircle(pos, 2.3f, i * gap, 1.0f, () =>
                {
                    Fx.Bolt(pos, Mat.Electro);
                    Sfx.Play("thunder", 0.7f, 1, 0.15f);
                    var pp = Game.I.Player.Pos;
                    float dd = Player.Flat(pp - pos).magnitude;
                    if (dd < 2.3f && pp.y < 3) Game.I.Player.TakeHit(170, pos);
                    if (dd < 9) Game.I.Cam.Shake(0.18f);
                });
            }
            float t = 0;
            return dt => (t += dt) < 1.2f + n * gap;
        }

        Func<float, bool> AtkShots()
        {
            var G = Game.I;
            Speak(Pick("shots"));
            Pose = "point";
            int waves = Phase == 2 ? 3 : 2, k = Phase == 2 ? 7 : 5;
            float t = 0; int fired = 0;
            return dt =>
            {
                t += dt;
                if (fired < waves && t > 0.55f + fired * 0.55f)
                {
                    var P = Game.I.Player;
                    float bas = Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z) + (fired % 2 == 1 ? 0.09f : 0);
                    for (int i = 0; i < k; i++) Shoot(bas + (i - (k - 1) / 2f) * 0.2f, Phase == 2 ? 15 : 12, 1.1f);
                    fired++;
                    Sfx.Play("shoot", 0.7f);
                    Fx.Glow(stickTip.position, Mat.Danger, 0.8f);
                }
                return t < 0.9f + waves * 0.55f;
            };
        }

        Func<float, bool> AtkSlam()
        {
            var G = Game.I;
            Speak(Pick("slam"));
            Pose = "slam";
            int waves = Phase == 2 ? 2 : 1;
            float t = 0; bool landed = false;
            Sfx.Play("jump", 0.9f, 0.5f);
            return dt =>
            {
                t += dt;
                if (t < 0.75f) Y = Mathf.Sin(t / 0.75f * Mathf.PI / 2) * 6;
                else if (t < 0.95f) Y = 6 * (1 - (t - 0.75f) / 0.2f);
                else
                {
                    Y = 0;
                    if (!landed)
                    {
                        landed = true;
                        Game.I.Cam.Shake(0.6f);
                        Sfx.Play("boom", 1f);
                        Fx.Debris(Pos + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.64f), 26);
                        Fx.Ring(Pos, 6, Color.white, 0.3f);
                        PostFX.I?.Chroma(0.2f);
                        for (int i = 0; i < waves; i++)
                        {
                            var c = Pos;
                            Fx.Later(i * 0.55f, () => Shockwave(c));
                        }
                    }
                }
                return t < 2.0f + (waves - 1) * 0.55f;
            };
        }

        void Shockwave(Vector3 center)
        {
            bool hit = false;
            Sfx.Play("wave", 0.9f);
            Fx.ShockWall(center, Mat.Electro, (r, dt) =>
            {
                var P = Game.I.Player;
                float d = Player.Flat(P.Pos - center).magnitude;
                if (!hit && Mathf.Abs(d - r) < 0.6f && P.Pos.y < 0.55f) hit = P.TakeHit(190, center);
                return Alive;
            });
        }

        // いやいや攻撃：首を横にふりながら、ドタバタ暴れて跳ね回る
        Func<float, bool> AtkIyaiya()
        {
            var G = Game.I;
            Speak(Pick("iyaiya"));
            Pose = "iyaiya";
            int hops = Phase == 2 ? 6 : 4;
            float hopDur = Phase == 2 ? 0.46f : 0.58f;
            int done = 0; float t = 0;
            Vector3 from = Pos, to = Pos;
            void NextHop()
            {
                from = Pos;
                var pp = Player.Flat(Game.I.Player.Pos) + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f));
                var d = pp - Player.Flat(Pos);
                if (d.magnitude > 7) d = d.normalized * 7;
                to = ClampArena(Player.Flat(Pos) + d);
                Fx.TelegraphCircle(to, 3.8f, 0, hopDur - 0.04f, null);
                if (Random.value < 0.5f) Game.I.Hud.WorldText(Pos + Vector3.up * 6, "いやいや！", new Color(1f, 0.6f, 0.6f), 0.9f);
            }
            NextHop();
            return dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / hopDur);
                Pos = Vector3.Lerp(from, to, u);
                Y = Mathf.Sin(u * Mathf.PI) * 2.2f;
                if (u > 0.05f) Face = Mathf.Atan2(to.x - from.x, to.z - from.z) + Mathf.Sin(animT * 20) * 0.4f;
                lockFace = true;
                if (u >= 1)
                {
                    Y = 0;
                    Sfx.Play("stomp", 1f, Random.Range(0.9f, 1.1f));
                    Game.I.Cam.Shake(0.35f);
                    Fx.Debris(Pos + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.64f), 14);
                    Fx.Ring(Pos, 4.2f, Color.white, 0.25f);
                    var pp = Game.I.Player.Pos;
                    if (Player.Flat(pp - Pos).magnitude < 3.8f && pp.y < 1.5f) Game.I.Player.TakeHit(150, Pos);
                    done++; t = 0;
                    if (done >= hops) { lockFace = false; return false; }
                    NextHop();
                }
                return true;
            };
        }

        // トランス攻撃：変圧器を投げる。磁気の波に当たると移動速度が下がる
        Func<float, bool> AtkTrans()
        {
            var G = Game.I;
            Speak(Pick("trans"));
            Pose = "point";
            var target = ClampArena(Player.Flat(G.Player.Pos));
            var start = StickTip;
            const float fly = 1.0f;
            Fx.TelegraphCircle(target, 3.2f, 0, fly, null);
            Sfx.Play("whoosh", 0.8f, 0.6f);
            var tr = new GameObject("trans");
            var core = Mat.ToonShared(new Color(0.35f, 0.37f, 0.4f), 0.03f);
            var coil = Mat.ToonShared(new Color(0.8f, 0.45f, 0.2f), 0.03f);
            Mat.Part(tr.transform, Mat.Cube, core, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 0.5f), default, true);
            Mat.Part(tr.transform, Mat.Frustum(0.32f, 0.32f, 0.9f, 14), coil, new Vector3(-0.42f, 0.6f, 0), Vector3.one, default, true);
            Mat.Part(tr.transform, Mat.Frustum(0.32f, 0.32f, 0.9f, 14), coil, new Vector3(0.42f, 0.6f, 0), Vector3.one, default, true);
            var glowM = Mat.Fx(new Color(0.4f, 1f, 0.6f, 0.7f), Mat.Glow, true, 2.5f);
            var glow = Mat.Part(tr.transform, Mat.Quad, glowM, new Vector3(0, 0.8f, 0), Vector3.one * 3, default, false);
            glow.AddComponent<Billboard>();
            tr.transform.position = start;
            float t = 0; bool landed = false; int pulses = 0;
            Fx.Run(dt =>
            {
                t += dt;
                if (!landed)
                {
                    float u = Mathf.Clamp01(t / fly);
                    tr.transform.position = Vector3.Lerp(start, target, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 5;
                    tr.transform.Rotate(300 * dt, 200 * dt, 0);
                    if (u >= 1)
                    {
                        landed = true; t = 0;
                        tr.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
                        tr.transform.position = target;
                        Sfx.Play("stomp", 0.8f, 1.2f);
                        Sfx.Play("trans", 0.8f);
                        Fx.Debris(target + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.64f), 10);
                        var pp = Game.I.Player.Pos;
                        if (Player.Flat(pp - target).magnitude < 3.2f && pp.y < 1.5f && Game.I.Player.TakeHit(90, target)) Game.I.ApplySlow(5f);
                    }
                    return true;
                }
                glowM.SetColor("_Color", new Color(0.4f, 1f, 0.6f, 0.4f + 0.3f * Mathf.Sin(t * 20)));
                if (pulses < 2 && t > 0.2f + pulses * 0.8f)
                {
                    pulses++;
                    Sfx.Play("wave", 0.6f, 1.4f);
                    bool hit = false;
                    float r = 0.5f;
                    var ringM = Mat.Fx(new Color(0.4f, 1f, 0.6f, 1f), Mat.RingTex, true, 3f);
                    var ring = Fx.Obj("ring", Fx.DiscMesh, ringM, target + Vector3.up * 0.1f, Quaternion.identity, Vector3.one);
                    Fx.Run(dt2 =>
                    {
                        r += 9 * dt2;
                        ring.transform.localScale = new Vector3(r / 0.86f, 1, r / 0.86f);
                        ringM.SetColor("_Color", new Color(0.4f, 1f, 0.6f, Mathf.Clamp01(1 - r / 7)));
                        var pp = Game.I.Player.Pos;
                        float d = Player.Flat(pp - target).magnitude;
                        if (!hit && Mathf.Abs(d - r) < 0.7f && pp.y < 0.8f) { hit = true; if (Game.I.Player.TakeHit(80, target)) Game.I.ApplySlow(5f); }
                        return r < 7;
                    }, ring);
                }
                return t < 2.2f;
            }, tr);
            float at = 0;
            return dt => (at += dt) < 1.5f;
        }

        // 必殺「三相交流」：120度ずつずれた3本の正弦波を撃ち出す
        Func<float, bool> AtkSansou()
        {
            var G = Game.I;
            Speak(Pick("sansou"), 3f);
            G.OnSansou();
            Pose = "raise";
            lockFace = true;
            Sfx.Play("warn", 0.8f, 0.8f);
            Sfx.Play("hum3", 1f);
            Sfx.Play("laserCharge", 0.8f, 0.7f);
            Color[] cols = { new Color(1f, 0.3f, 0.3f), new Color(1f, 0.85f, 0.25f), new Color(0.35f, 0.6f, 1f) };
            var P = G.Player;
            float aim = Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z);
            Face = aim;
            var fwd = new Vector3(Mathf.Sin(aim), 0, Mathf.Cos(aim));
            var side = new Vector3(fwd.z, 0, -fwd.x);
            var origin = Player.Flat(Pos) + fwd * 1.2f;
            // 3本の正弦波の「壁」。120度ずつずれていて、波は横に流れていく（すき間も動く）
            float A = Phase == 2 ? 8.5f : 7.5f, omega = Phase == 2 ? 3.2f : 2.3f, life = Phase == 2 ? 3.6f : 3.0f;
            const float lambda = 18f, H = 7f, startup = 0.55f, grow = 30f, maxLen = 46f;
            var root = new GameObject("wave3");
            var meshes = new Mesh[3];
            var mats = new Material[3];
            for (int k = 0; k < 3; k++)
            {
                meshes[k] = new Mesh(); meshes[k].MarkDynamic();
                mats[k] = Mat.Fx(new Color(cols[k].r, cols[k].g, cols[k].b, 1f), SineWallTex, true, 1.9f);
                var go = new GameObject("ph" + k);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[k];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mats[k];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            float t = 0; bool boomed = false;
            Fx.Run(dt =>
            {
                t += dt;
                bool warm = t < startup;
                float front = warm ? maxLen : Mathf.Min(maxLen, (t - startup) * grow);
                float h = warm ? 0.12f : Mathf.Min(H, 0.12f + (t - startup) * 45f);
                if (t > startup + life) h *= Mathf.Clamp01(1 - (t - startup - life) / 0.35f);
                float wt = t * omega;
                for (int k = 0; k < 3; k++)
                {
                    BuildSineWall(meshes[k], origin, fwd, side, A, lambda, k * Mathf.PI * 2 / 3 - wt, front, h);
                    float a = warm ? 0.6f + 0.4f * Mathf.Sin(t * 40) : 0.85f;
                    mats[k].SetColor("_Color", new Color(cols[k].r, cols[k].g, cols[k].b, a));
                }
                if (!warm && !boomed)
                {
                    boomed = true;
                    Sfx.Play("laser", 1f, 0.75f);
                    Sfx.Play("thunder", 0.8f, 0.8f);
                    Game.I.Cam.Shake(0.6f);
                    PostFX.I?.Chroma(0.2f);
                }
                // 当たり判定：プレイヤーの位置での、3本の波の横位置と比べる
                if (!warm && h > 1.2f && Alive)
                {
                    var pl = Game.I.Player;
                    var rel = Player.Flat(pl.Pos) - origin;
                    float sp = Vector3.Dot(rel, fwd), lp = Vector3.Dot(rel, side);
                    if (sp > 0 && sp < front && pl.Pos.y < h)
                        for (int k = 0; k < 3; k++)
                        {
                            float lat = A * Mathf.Sin(2 * Mathf.PI * sp / lambda + k * Mathf.PI * 2 / 3 - wt);
                            if (Mathf.Abs(lp - lat) < 0.85f && pl.TakeHit(150, origin + fwd * sp + side * lat))
                            {
                                Fx.Sparks(pl.Pos + Vector3.up, cols[k], 16, 1.2f);
                                break;
                            }
                        }
                }
                if (Random.value < 0.6f) Fx.Embers(origin + fwd * Random.Range(2f, front) + side * Random.Range(-A, A) + Vector3.up * Random.Range(0f, h), cols[Random.Range(0, 3)], 1);
                bool more = t < startup + life + 0.35f && Alive && Game.I.State == Game.Mode.Battle;
                if (!more) foreach (var m in meshes) Destroy(m);
                return more;
            }, root);
            float at = 0;
            return dt =>
            {
                at += dt;
                if (at > startup) Pose = "point";
                for (int k = 0; k < 3; k++) if (Random.value < 0.5f) Fx.Embers(StickTip + Random.insideUnitSphere * 0.6f, cols[k], 1);
                if (at > startup + life) { lockFace = false; return false; }
                return true;
            };
        }

        // 三相交流の壁のテクスチャ：上下のふちが明るく、縦じまが流れる
        static Texture2D sineWallTex;
        static Texture2D SineWallTex => sineWallTex != null ? sineWallTex : (sineWallTex = MakeSineWallTex());
        static Texture2D MakeSineWallTex()
        {
            var tex = Mat.MakeTex(64, (x, y) =>
            {
                // 中はうすく（向こうが透けて見える）、上下のふちと縦じまだけ明るく
                float edge = Mathf.Max(Mathf.Pow(1 - y, 7f), Mathf.Pow(y, 10f));
                float stripe = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(x * Mathf.PI * 12), 8f);
                float a = 0.2f + 0.8f * edge + 0.3f * stripe;
                return new Color(1, 1, 1, Mathf.Clamp01(a));
            });
            tex.wrapMode = TextureWrapMode.Repeat;
            return tex;
        }

        // 正弦波にそって立つ縦の帯を、毎フレーム作りなおす（ワールド座標）
        static void BuildSineWall(Mesh mesh, Vector3 origin, Vector3 fwd, Vector3 side, float A, float lambda, float phase, float len, float h)
        {
            const float step = 0.5f;
            int n = Mathf.Max(2, Mathf.CeilToInt(len / step) + 1);
            var v = new Vector3[n * 2]; var uv = new Vector2[n * 2]; var tri = new int[(n - 1) * 6];
            for (int i = 0; i < n; i++)
            {
                float s = Mathf.Min(len, i * step);
                var p = origin + fwd * s + side * (A * Mathf.Sin(2 * Mathf.PI * s / lambda + phase));
                v[i * 2] = new Vector3(p.x, 0.02f, p.z);
                v[i * 2 + 1] = new Vector3(p.x, h, p.z);
                uv[i * 2] = new Vector2(s / 6f, 0);
                uv[i * 2 + 1] = new Vector2(s / 6f, 1);
                if (i < n - 1)
                {
                    int b = i * 6, q = i * 2;
                    tri[b] = q; tri[b + 1] = q + 1; tri[b + 2] = q + 2;
                    tri[b + 3] = q + 1; tri[b + 4] = q + 3; tri[b + 5] = q + 2;
                }
            }
            mesh.Clear();
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = tri;
            mesh.RecalculateBounds();
        }

        // 第2形態：指示棒から極太ビームを出して薙ぎ払う
        Func<float, bool> AtkLaser()
        {
            var G = Game.I;
            Speak(Pick("laser"));
            Pose = "point";
            lockFace = true;
            var P = G.Player;
            float aim = Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z);
            float sweep = 75 * Mathf.Deg2Rad * (Random.value < 0.5f ? 1 : -1);
            float a0 = aim - sweep, a1 = aim + sweep;
            Face = a0;
            const float len = 34;
            Sfx.Play("laserCharge", 0.9f);
            // 予告：細い線
            var warnM = Mat.Fx(new Color(1f, 0.3f, 0.3f, 0.8f), Mat.Streak, true, 3f);
            var warn = Fx.Obj("beam", Fx.BeamMesh, warnM, Vector3.zero, Quaternion.identity, new Vector3(0.15f, 1, len));
            GameObject beam = null, beamCore = null; Material bm = null, bc = null;
            float t = 0; bool sfx = false;
            return dt =>
            {
                t += dt;
                var origin = new Vector3(Pos.x, 1.15f, Pos.z);
                if (t < 1.0f)
                {
                    // 狙いを定める
                    warn.transform.SetPositionAndRotation(origin, Quaternion.Euler(0, a0 * Mathf.Rad2Deg, 0));
                    warnM.SetColor("_Color", new Color(1, 0.3f, 0.3f, 0.4f + 0.4f * Mathf.Sin(t * 30)));
                    if (Random.value < 0.8f) Fx.Embers(stickTip.position + Random.insideUnitSphere * 0.8f, Mat.Danger, 1);
                    Face = a0;
                    return true;
                }
                if (warn) Fx.Kill(warn);
                if (!sfx) { sfx = true; Sfx.Play("laser", 1f); Game.I.Cam.Shake(0.3f); PostFX.I?.Chroma(0.25f); }
                float u = Mathf.Clamp01((t - 1.0f) / 2.0f);
                float a = Mathf.Lerp(a0, a1, Mathf.SmoothStep(0, 1, u));
                Face = a;
                if (beam == null)
                {
                    bm = Mat.Fx(new Color(1f, 0.35f, 0.5f, 1), Mat.Streak, true, 3f);
                    bc = Mat.Fx(Color.white, Mat.Streak, true, 4f);
                    beam = Fx.Obj("beam", Fx.BeamMesh, bm, origin, Quaternion.identity, new Vector3(2.2f, 1, len));
                    beamCore = Fx.Obj("beam", Fx.BeamMesh, bc, origin, Quaternion.identity, new Vector3(0.7f, 1, len));
                    beam.AddComponent<BeamFacer>(); beamCore.AddComponent<BeamFacer>();
                }
                var rot = Quaternion.Euler(0, a * Mathf.Rad2Deg, 0);
                beam.transform.SetPositionAndRotation(origin, rot);
                beamCore.transform.SetPositionAndRotation(origin, rot);
                float wob = 1 + Mathf.Sin(t * 60) * 0.12f;
                beam.transform.localScale = new Vector3(2.2f * wob, 1, len);
                // 当たり判定：ビームの線分とプレイヤーの距離
                var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                var pp = Game.I.Player.Pos;
                var rel = Player.Flat(pp - origin);
                float along = Vector3.Dot(rel, dir);
                float side = (rel - dir * along).magnitude;
                if (along > 0 && along < len && side < 1.0f && pp.y < 1.3f) Game.I.Player.TakeHit(150, origin + dir * along);
                // 地面を焦がす火花
                var end = origin + dir * Mathf.Min(len, World.ArenaR * 2);
                if (Random.value < 0.7f) Fx.Sparks(origin + dir * Random.Range(2f, 20f) + Vector3.down, new Color(1f, 0.5f, 0.6f), 2, 0.6f);
                _ = end;
                if (u >= 1)
                {
                    Fx.Kill(beam); Fx.Kill(beamCore);
                    lockFace = false;
                    return false;
                }
                return true;
            };
        }

        // 第2形態：回りながら3方向にらせん状に弾をまく
        Func<float, bool> AtkSpiral()
        {
            var G = Game.I;
            Speak(Pick("spiral"));
            Pose = "spin";
            lockFace = true;
            float t = 0, shotT = 0, ang = Random.value * Mathf.PI * 2;
            return dt =>
            {
                t += dt;
                if (t > 0.5f && t < 3.1f)
                {
                    shotT -= dt;
                    ang += dt * 1.9f;
                    Face = ang;
                    if (shotT <= 0)
                    {
                        shotT = 0.11f;
                        for (int k = 0; k < 3; k++) Shoot(ang + k * Mathf.PI * 2 / 3, 9, 1.1f);
                        Sfx.Play("shoot", 0.4f, 1.3f);
                    }
                }
                if (t >= 3.4f) { lockFace = false; return false; }
                return true;
            };
        }

        Func<float, bool> AtkRoar()
        {
            var G = Game.I;
            Speak(Line("phase2"), 2.6f);
            Pose = "roar";
            aura.SetActive(true);
            auraPs.Play();
            G.OnPhase2(this);
            Fx.Ring(Pos, 14, Mat.Electro, 0.8f, 3f);
            float t = 0;
            return dt =>
            {
                t += dt;
                if (Random.value < 0.5f) Fx.Sparks(Pos + Vector3.up * Random.Range(1f, 5f) + Random.insideUnitSphere * 2, Mat.ElectroLight, 2, 0.8f);
                return t < 2.0f;
            };
        }

        void Shoot(float angle, float speed, float y)
        {
            var g = new GameObject("bullet");
            var body = Mat.Part(g.transform, Mat.Cylinder, Mat.ToonShared(new Color(0.91f, 0.83f, 0.64f), 0.02f), Vector3.zero, new Vector3(0.44f, 0.8f, 0.44f), new Vector3(90, 0, 0), false);
            _ = body;
            Mat.Part(g.transform, Mat.Cylinder, Mat.ToonShared(new Color(0.72f, 0.75f, 0.8f)), Vector3.zero, new Vector3(0.08f, 1.6f, 0.08f), new Vector3(90, 0, 0), false);
            Color[] bands = { new Color(0.55f, 0.3f, 0.15f), Color.black, new Color(0.9f, 0.2f, 0.2f), new Color(0.95f, 0.55f, 0.1f), new Color(0.2f, 0.5f, 0.9f) };
            for (int i = 0; i < 3; i++)
                Mat.Part(g.transform, Mat.Cylinder, Mat.ToonShared(bands[Random.Range(0, bands.Length)]), new Vector3(0, 0, -0.2f + i * 0.2f), new Vector3(0.47f, 0.09f, 0.47f), new Vector3(90, 0, 0), false);
            var glow = Mat.Part(g.transform, Mat.Quad, Mat.FxShared(new Color(1f, 0.4f, 0.35f, 0.9f), Mat.Glow, true, 2.5f), Vector3.zero, Vector3.one * 1.6f, default, false);
            glow.AddComponent<Billboard>();
            var v = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * speed;
            g.transform.position = new Vector3(Pos.x, y, Pos.z) + v.normalized * 1.8f;
            g.transform.rotation = Quaternion.LookRotation(v);
            float t = 0; float spin = 0;
            Fx.Run(dt =>
            {
                t += dt;
                if (g == null) return false;
                g.transform.position += v * dt;
                spin += dt * 800;
                g.transform.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(0, 0, spin);
                var P = Game.I.Player;
                var p = g.transform.position;
                if (new Vector2(p.x - P.Pos.x, p.z - P.Pos.z).magnitude < 0.8f && Mathf.Abs(P.Pos.y + 1 - p.y) < 1.1f)
                {
                    if (P.TakeHit(110, p - v.normalized)) { Fx.Sparks(p, Mat.Danger, 10, 0.8f); return false; }
                }
                return t < 3.5f && Alive;
            }, g);
        }

        static Vector3 ClampArena(Vector3 p, float margin = 1f)
        {
            var f = Player.Flat(p);
            if (f.magnitude > World.ArenaR - margin) f = f.normalized * (World.ArenaR - margin);
            return f;
        }

        // ================= 見た目の動き =================
        void Animate(float dt)
        {
            transform.position = new Vector3(Pos.x, Y, Pos.z);
            transform.rotation = Quaternion.Euler(0, Face * Mathf.Rad2Deg, 0);
            if (bookOrbit) bookOrbit.localRotation = Quaternion.Euler(0, animT * (Phase == 2 ? 220 : 90), 0);
            if (bookOrbit) bookOrbit.GetChild(0).localRotation = Quaternion.Euler(Mathf.Sin(animT * 2) * 15, 90, 0);

            Quaternion aL = Quaternion.Euler(0, 0, -6), aR = Quaternion.Euler(0, 0, 6);
            float lean = 0, lL = 0, lR = 0, sink = 0, headTilt = 0, spinBody = 0, headYaw = 0;
            float breath = Mathf.Sin(animT * 2) * 0.04f;

            switch (Pose)
            {
                case "raise": aL = Quaternion.Euler(0, 0, -150); aR = Quaternion.Euler(0, 0, 150); break;
                case "point": aR = Quaternion.Euler(-90, 0, 0); lean = 5; break;
                case "slam":
                    aL = Quaternion.Euler(0, 0, -160); aR = Quaternion.Euler(0, 0, 160);
                    if (Y <= 0.01f) { aL = Quaternion.Euler(-40, 0, -40); aR = Quaternion.Euler(-40, 0, 40); lean = 20; sink = -0.3f; }
                    break;
                case "scythe":
                    // 鎌を大きく振り回す
                    aR = Quaternion.Euler(-70, 0, Mathf.Lerp(90, -70, PoseU));
                    aL = Quaternion.Euler(-30, 0, -40);
                    spinBody = Mathf.Lerp(-50, 50, PoseU); lean = 10;
                    break;
                case "iyaiya":
                    // 首を横にぶんぶん振って、手足をばたばた
                    headYaw = Mathf.Sin(animT * 24) * 35;
                    aL = Quaternion.Euler(Mathf.Sin(animT * 23) * 80, 0, -70 + Mathf.Sin(animT * 17) * 50);
                    aR = Quaternion.Euler(Mathf.Cos(animT * 21) * 80, 0, 70 + Mathf.Cos(animT * 19) * 50);
                    lL = Mathf.Sin(animT * 26) * 40; lR = -lL; lean = Mathf.Sin(animT * 13) * 10;
                    break;
                case "spin": aL = Quaternion.Euler(0, 0, -90); aR = Quaternion.Euler(0, 0, 90); break;
                case "roar": aL = Quaternion.Euler(-30, 0, -110); aR = Quaternion.Euler(-30, 0, 110); lean = -12; headTilt = -20; break;
                case "dizzy":
                    lean = 15 + Mathf.Sin(animT * 3) * 5; headTilt = Mathf.Sin(animT * 4) * 20; sink = -0.4f;
                    aL = Quaternion.Euler(20, 0, -20); aR = Quaternion.Euler(20, 0, 20);
                    if (dizzy.activeSelf) dizzy.transform.localRotation = Quaternion.Euler(0, animT * 240, 0);
                    break;
                case "defeat":
                    float u = Mathf.Clamp01(SinkT / 1.2f);
                    lean = Mathf.Lerp(0, 25, u); sink = Mathf.Lerp(0, -1.4f, u); headTilt = 25 * u;
                    aL = Quaternion.Euler(-10, 0, -15); aR = Quaternion.Euler(-10, 0, 15);
                    lL = lR = -70 * u;
                    break;
                default:
                    if (walking)
                    {
                        walkT += dt * 6;
                        lL = Mathf.Sin(walkT) * 25; lR = -lL;
                        aL = Quaternion.Euler(-lL * 0.6f, 0, -6); aR = Quaternion.Euler(-lR * 0.6f, 0, 6);
                    }
                    else
                    {
                        // 指示棒をトントンする
                        aR = Quaternion.Euler(-35 + Mathf.Sin(animT * 3) * 10, 0, 12);
                    }
                    break;
            }
            float k = Mathf.Min(1, dt * 12);
            armL.localRotation = Quaternion.Slerp(armL.localRotation, aL, k);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, aR, k);
            legL.localRotation = Quaternion.Slerp(legL.localRotation, Quaternion.Euler(lL, 0, 0), k);
            legR.localRotation = Quaternion.Slerp(legR.localRotation, Quaternion.Euler(lR, 0, 0), k);
            inner.localRotation = Quaternion.Slerp(inner.localRotation, Quaternion.Euler(lean, spinBody, 0), k);
            inner.localPosition = new Vector3(0, Mathf.Lerp(inner.localPosition.y, sink + breath, k), 0);
            headT.localRotation = Quaternion.Slerp(headT.localRotation, Quaternion.Euler(headTilt, headYaw, Mathf.Sin(animT * 1.3f) * 3), Pose == "iyaiya" ? 1 : k);

            // 被弾フラッシュ・第2形態の光
            Color fc = Broken ? new Color(1f, 0.85f, 0.4f) : Color.white;
            Mat.SetFlash(mats, Flash > 0 ? 0.75f : 0, fc);
            if (Phase == 2 && Alive)
            {
                float p = 0.5f + 0.5f * Mathf.Sin(animT * 6);
                eyeGlow.SetColor("_Color", new Color(1, 0.2f, 0.25f, 0.6f + 0.4f * p));
                auraMat.SetColor("_Color", new Color(0.75f, 0.45f, 1f, 0.16f + 0.1f * p));
                aura.transform.localScale = Vector3.one * (7.5f + p);
            }
            else if (!Alive) eyeGlow.SetColor("_Color", new Color(1, 0.2f, 0.2f, 0));
        }

        public Vector3 HeadPos => new Vector3(Pos.x, Y + (IsSuga ? 5.9f : 4.6f), Pos.z);
        public Vector3 StickTip => stickTip.position;
    }

    // ビームの板をカメラの方にひねって、横から見ても細くならないようにする
    public class BeamFacer : MonoBehaviour
    {
        void LateUpdate()
        {
            var c = Camera.main;
            if (!c) return;
            var fwd = transform.forward;
            var toCam = c.transform.position - transform.position;
            var up = Vector3.Cross(fwd, Vector3.Cross(toCam, fwd)).normalized;
            if (up.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(fwd, up);
        }
    }
}
