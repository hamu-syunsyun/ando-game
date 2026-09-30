using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AndoBoss
{
    // 英語担当・菅原先生（架空の人物）。あだ名は「死神」「北の破壊神」。
    // 授業で問題をまちがえるたびに「カウント」がつき、5つたまると単位がなくなる。
    // その「カウント」をモチーフにした攻撃をする
    public partial class Boss
    {
        float hakaiCd = 2f;
        public float PoseU; // 鎌を振る動きの進み具合（0〜1）

        static readonly Dictionary<string, string> SugaOne = new Dictionary<string, string>
        {
            { "intro", "……英語の菅原です。カウント、いくつ欲しい？" },
            { "hp75", "へえ。少しはやるね。" },
            { "hp25", "……まだカウントは残ってるよ？" },
            { "phase2", "……さて。本当のカウントを始めようか。" },
            { "break", "……っ、スペルミスか……！" },
            { "breakEnd", "今のは、ノーカウントにしてあげる。" },
            { "win", "……Good job. 単位、あげるよ。" },
            { "lose", "カウント5つ分の働きだったね。" },
            { "timeup", "時間切れ。答案、集めます。" },
            { "hit", "（カウントを）もらってみる？？" },
            { "count5", "はい、カウント5。単位、なくなりました。" },
        };

        static readonly Dictionary<string, string[]> SugaLines = new Dictionary<string, string[]>
        {
            { "count", new[] { "はい、カウント。", "カウント、追加ね。", "間違えたね？カウント。" } },
            { "scythe", new[] { "単位、刈り取っちゃおうか。", "死神って呼ばれてるの、知ってる？" } },
            { "words", new[] { "抜き打ち英単語テスト、始めます。", "スペル、ちゃんと書ける？" } },
            { "morau", new[] { "（カウントを）もらってみる？？", "カウント、もらってみる？？" } },
            { "wall", new[] { "英検準2級も取れない人は……ピーーーーッ（自主規制）", "英検準2級も取れないの？論外。", "英検の壁、越えられる？" } },
            { "hakai", new[] { "北の破壊神、と呼ばれています。", "ここから先は、破壊の時間。" } },
            { "sheets", new[] { "答案、返すね。……赤いのは全部カウント。", "はい、返却。何点だった？" } },
            { "listen", new[] { "Listen carefully.", "リスニング、始めます。聞き逃したらカウント。" } },
            { "redpen", new[] { "赤ペン添削の時間です。", "ここも、ここも、まちがい。" } },
        };
        static string SPick(string k) { var a = SugaLines[k]; return a[Random.Range(0, a.Length)]; }

        static readonly Color SugaRed = new Color(1f, 0.18f, 0.25f);
        static readonly Color SugaPurple = new Color(0.55f, 0.3f, 0.9f);

        // ================= 見た目 =================
        void BuildSuga()
        {
            inner = Mat.Pivot(transform, "inner", Vector3.zero);
            Material M(Color c, float o = 0.04f) { var m = Mat.Toon(c, o); mats.Add(m); return m; }
            var skin = M(new Color(0.9f, 0.86f, 0.84f));      // 青白い肌
            var coat = M(new Color(0.09f, 0.08f, 0.12f));     // 黒いロングコート
            var shirt = M(new Color(0.85f, 0.85f, 0.88f));
            var tie = M(new Color(0.55f, 0.08f, 0.12f));
            var hairM = M(new Color(0.94f, 0.94f, 0.96f));   // 白髪
            var dark = M(new Color(0.05f, 0.04f, 0.07f), 0);

            // 背が高くて横は細い、ひょろっと細長い体形
            // 長い脚
            legL = Mat.Pivot(inner, "legL", new Vector3(-0.22f, 2.0f, 0));
            legR = Mat.Pivot(inner, "legR", new Vector3(0.22f, 2.0f, 0));
            foreach (var l in new[] { legL, legR })
            {
                Mat.Part(l, Mat.Frustum(0.12f, 0.16f, 2.0f, 12), coat, new Vector3(0, -1.0f, 0), Vector3.one);
                Mat.Part(l, Mat.Sphere, dark, new Vector3(0, -1.98f, 0.12f), new Vector3(0.3f, 0.18f, 0.62f));
            }
            // 細身のロングコート
            Mat.Part(inner, Mat.Frustum(0.72f, 0.5f, 3.0f, 20), coat, new Vector3(0, 3.2f, 0), Vector3.one);
            Mat.Part(inner, Mat.Cube, shirt, new Vector3(0, 4.15f, 0.5f), new Vector3(0.3f, 1.0f, 0.1f), new Vector3(-5, 0, 0));
            Mat.Part(inner, Mat.Cube, tie, new Vector3(0, 4.05f, 0.55f), new Vector3(0.12f, 0.85f, 0.08f), new Vector3(-5, 0, 0));
            // 襟
            Mat.Part(inner, Mat.Frustum(0.44f, 0.5f, 0.55f, 16, true), coat, new Vector3(0, 4.72f, -0.03f), Vector3.one);

            // 細長い顔（メガネはなし）
            headT = Mat.Pivot(inner, "head", new Vector3(0, 4.85f, 0));
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.85f, 0), new Vector3(1.05f, 1.8f, 1.15f));
            // あご
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.2f, 0.12f), new Vector3(0.62f, 0.55f, 0.72f));
            // 白髪（後ろになでつけた髪）
            Mat.Part(headT, Mat.Sphere, hairM, new Vector3(0, 1.2f, -0.14f), new Vector3(1.13f, 1.35f, 1.18f));
            Mat.Part(headT, Mat.Sphere, hairM, new Vector3(0, 1.66f, 0.08f), new Vector3(0.98f, 0.42f, 0.95f), new Vector3(-14, 0, 0));
            foreach (float x in new[] { -0.5f, 0.5f })
                Mat.Part(headT, Mat.Sphere, hairM, new Vector3(x, 1.05f, -0.12f), new Vector3(0.24f, 0.6f, 0.7f));
            // 目のまわりの影・細い眉・赤く光る目
            foreach (float x in new[] { -0.2f, 0.2f })
            {
                Mat.Part(headT, Mat.Sphere, M(new Color(0.55f, 0.48f, 0.52f), 0), new Vector3(x, 0.98f, 0.5f), new Vector3(0.26f, 0.14f, 0.1f), default, false);
                Mat.Part(headT, Mat.Cube, hairM, new Vector3(x, 1.14f, 0.52f), new Vector3(0.26f, 0.045f, 0.05f), new Vector3(0, 0, x < 0 ? -18 : 18), false);
            }
            eyeGlow = Mat.Fx(new Color(1, 0.15f, 0.2f, 0.8f), Mat.Glow, true, 3f);
            foreach (float x in new[] { -0.2f, 0.2f })
            {
                var e = Mat.Part(headT, Mat.Quad, eyeGlow, new Vector3(x, 0.98f, 0.58f), Vector3.one * 0.32f, default, false);
                e.AddComponent<Billboard>();
            }
            // 高い鼻
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.74f, 0.57f), new Vector3(0.11f, 0.32f, 0.14f));
            // 薄い笑みの口
            Mat.Part(headT, Mat.Cube, M(new Color(0.4f, 0.15f, 0.16f), 0), new Vector3(0, 0.38f, 0.5f), new Vector3(0.3f, 0.035f, 0.04f), new Vector3(0, 0, -6), false);

            // 長くて細い腕
            const float hand = -2.05f;
            Transform MkArm(float x)
            {
                var p = Mat.Pivot(inner, "arm", new Vector3(x, 4.5f, 0));
                Mat.Part(p, Mat.Frustum(0.11f, 0.15f, 2.0f, 12), coat, new Vector3(0, -0.98f, 0), Vector3.one);
                Mat.Part(p, Mat.Sphere, skin, new Vector3(0, hand, 0), Vector3.one * 0.3f);
                return p;
            }
            armL = MkArm(-0.66f); armR = MkArm(0.66f);

            // 死神の鎌（赤ペン）：長い柄と、赤く光る曲がった刃
            var pole = M(new Color(0.15f, 0.12f, 0.14f), 0.02f);
            Mat.Part(armR, Mat.Frustum(0.07f, 0.07f, 4.4f, 8), pole, new Vector3(0, hand, 1.0f), Vector3.one, new Vector3(90, 0, 0));
            var blade = Mat.Toon(SugaRed, 0.03f, new Color(1f, 0.1f, 0.15f, 0.5f)); mats.Add(blade);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 6f;
                var pos = new Vector3(0, hand + a * 1.4f * 0.2f + Mathf.Sin(a * Mathf.PI * 0.8f) * 1.3f, 3.2f - a * 1.7f + Mathf.Sin(a * 2.2f) * 0.2f);
                Mat.Part(armR, Mat.Cube, blade, pos, new Vector3(0.06f, 0.35f - a * 0.2f, 0.55f), new Vector3(-a * 70 + 20, 0, 0));
            }
            stickTip = Mat.Pivot(armR, "tip", new Vector3(0, hand, 3.2f));

            // 周りを回る英語の辞書
            bookOrbit = Mat.Pivot(inner, "bookOrbit", new Vector3(0, 4.3f, 0));
            var book = Mat.Pivot(bookOrbit, "book", new Vector3(2.4f, 0, 0));
            Mat.Part(book, Mat.Cube, M(new Color(0.95f, 0.93f, 0.86f), 0.02f), Vector3.zero, new Vector3(0.9f, 1.25f, 0.3f));
            var cover = Mat.Toon(Color.white, 0, null, DictCover()); mats.Add(cover);
            Mat.Part(book, Mat.Quad, cover, new Vector3(0, 0, -0.155f), new Vector3(0.92f, 1.27f, 1), default, false);
            var back = Mat.Toon(new Color(0.1f, 0.14f, 0.35f)); mats.Add(back);
            Mat.Part(book, Mat.Quad, back, new Vector3(0, 0, 0.155f), new Vector3(0.92f, 1.27f, 1), new Vector3(0, 180, 0), false);
        }

        static Texture2D DictCover()
        {
            // 紺色の表紙に金色の枠と「EN」っぽい模様
            return Mat.MakeTex(128, (x, y) =>
            {
                var navy = new Color(0.1f, 0.14f, 0.35f);
                var gold = new Color(0.95f, 0.8f, 0.35f);
                if (x < 0.06f || x > 0.94f || y < 0.05f || y > 0.95f) return gold;
                float ex = x - 0.3f, nx = x - 0.62f, cy = y - 0.62f;
                bool e = (Mathf.Abs(ex) < 0.12f && Mathf.Abs(cy) < 0.14f) && (ex < -0.07f || Mathf.Abs(cy) > 0.1f || Mathf.Abs(cy) < 0.02f);
                bool n = Mathf.Abs(cy) < 0.14f && (Mathf.Abs(nx + 0.1f) < 0.03f || Mathf.Abs(nx - 0.1f) < 0.03f || Mathf.Abs(nx + cy * 0.7f) < 0.03f);
                if (e || n) return gold;
                if (y > 0.2f && y < 0.23f) return gold;
                return navy;
            });
        }

        // ================= 攻撃の選び方 =================
        void PickSugaAttack()
        {
            if (Phase == 2) hakaiCd -= 1;
            var opts = new List<string> { "count", "scythe", "words", "morau", "wall", "sheets", "listen", "redpen" };
            if (Phase == 2) { opts.Add("count"); opts.Add("wall"); }
            opts.RemoveAll(k => k == lastAtk);
            if (Phase == 2 && hakaiCd <= 0) { opts.Clear(); opts.Add("hakai"); hakaiCd = 5; }
            var pick = opts[Random.Range(0, opts.Count)];
            lastAtk = pick;
            walking = false;
            switch (pick)
            {
                case "count": atk = AtkCount(); break;
                case "scythe": atk = AtkScythe(); break;
                case "words": atk = AtkWords(); break;
                case "morau": atk = AtkMorau(); break;
                case "wall": atk = AtkWall(); break;
                case "sheets": atk = AtkSheets(); break;
                case "listen": atk = AtkListen(); break;
                case "redpen": atk = AtkRedPen(); break;
                default: atk = AtkHakai(); break;
            }
        }

        // 当たったらカウントがつく攻撃
        static bool HitWithCount(float dmg, Vector3 from, int count = 1)
        {
            var G = Game.I;
            if (!G.Player.TakeHit(dmg, from)) return false;
            G.AddCount(count);
            return true;
        }

        // ---- カウント弾：赤い「×」を扇形に撃つ。当たるとカウント ----
        Func<float, bool> AtkCount()
        {
            var G = Game.I;
            Speak(SPick("count"));
            Pose = "point";
            int waves = Phase == 2 ? 4 : 3, k = Phase == 2 ? 7 : 5;
            float t = 0; int fired = 0;
            return dt =>
            {
                t += dt;
                if (fired < waves && t > 0.6f + fired * 0.6f)
                {
                    var P = Game.I.Player;
                    float bas = Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z) + (fired % 2 == 1 ? 0.12f : 0);
                    for (int i = 0; i < k; i++) CrossShot(bas + (i - (k - 1) / 2f) * 0.24f, Phase == 2 ? 12 : 10);
                    fired++;
                    Sfx.Play("shoot", 0.7f, 0.8f);
                    Fx.Glow(StickTip, SugaRed, 0.8f);
                }
                return t < 1.0f + waves * 0.6f;
            };
        }

        void CrossShot(float angle, float speed)
        {
            var g = new GameObject("bullet");
            var red = Mat.FxShared(new Color(SugaRed.r, SugaRed.g, SugaRed.b, 1f), Mat.White, true, 3f);
            Mat.Part(g.transform, Mat.Cube, red, Vector3.zero, new Vector3(1.0f, 0.18f, 0.18f), new Vector3(0, 0, 45), false);
            Mat.Part(g.transform, Mat.Cube, red, Vector3.zero, new Vector3(1.0f, 0.18f, 0.18f), new Vector3(0, 0, -45), false);
            var glow = Mat.Part(g.transform, Mat.Quad, Mat.FxShared(new Color(1f, 0.2f, 0.25f, 0.8f), Mat.Glow, true, 2.5f), Vector3.zero, Vector3.one * 1.8f, default, false);
            glow.AddComponent<Billboard>();
            var v = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * speed;
            g.transform.position = new Vector3(Pos.x, 1.2f, Pos.z) + v.normalized * 1.8f;
            float t = 0, spin = 0;
            Fx.Run(dt =>
            {
                t += dt;
                if (g == null) return false;
                g.transform.position += v * dt;
                spin += dt * 400;
                g.transform.rotation = Quaternion.LookRotation(v) * Quaternion.Euler(0, 0, spin);
                var p = g.transform.position;
                var P = Game.I.Player;
                if (new Vector2(p.x - P.Pos.x, p.z - P.Pos.z).magnitude < 0.85f && P.Pos.y < 1.8f)
                {
                    if (HitWithCount(70, p - v.normalized)) { Fx.Sparks(p, SugaRed, 12, 0.8f); return false; }
                }
                return t < 3.5f && Alive;
            }, g);
        }

        // ---- 死神の鎌：まわり全体を大きく刈り取る。範囲の外に逃げる ----
        Func<float, bool> AtkScythe()
        {
            var G = Game.I;
            Speak(SPick("scythe"));
            Pose = "scythe";
            PoseU = 0;
            int swings = Phase == 2 ? 2 : 1;
            float[] radius = { 7.5f, 11f };
            Sfx.Play("warn", 0.6f, 0.7f);
            for (int i = 0; i < swings; i++)
            {
                int k = i;
                var c = Pos;
                Fx.TelegraphCircle(c, radius[k], k * 1.1f, 1.1f, () =>
                {
                    var center = Pos;
                    Sfx.Play("slash3", 1f, 0.55f);
                    Sfx.Play("wave", 0.8f, 0.7f);
                    Fx.Ring(center, radius[k], SugaRed, 0.35f, 3f);
                    for (int s = 0; s < 6; s++) Fx.SlashLine(center + Vector3.up * 1.2f, s % 2 == 0 ? SugaRed : Color.white, radius[k] * 2);
                    Game.I.Cam.Shake(0.4f);
                    var pp = Game.I.Player.Pos;
                    if (Player.Flat(pp - center).magnitude < radius[k] && pp.y < 2.5f) HitWithCount(170, center);
                });
            }
            float t = 0, total = 1.2f + swings * 1.1f;
            return dt =>
            {
                t += dt;
                PoseU = Mathf.PingPong(t / 1.1f, 1);
                return t < total;
            };
        }

        // ---- 抜き打ち英単語テスト：巨大なアルファベットが降ってくる ----
        Func<float, bool> AtkWords()
        {
            var G = Game.I;
            Speak(SPick("words"));
            Pose = "raise";
            const string letters = "ENGLISHCOUNTDEATH";
            int n = Phase == 2 ? 9 : 6;
            float gap = Phase == 2 ? 0.16f : 0.24f;
            Sfx.Play("warn", 0.5f, 0.8f);
            for (int i = 0; i < n; i++)
            {
                var P = G.Player;
                var pos = i == 0 ? Player.Flat(P.Pos) : Player.Flat(P.Pos) + new Vector3(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
                pos = ClampArena(pos);
                char ch = letters[Random.Range(0, letters.Length)];
                Fx.TelegraphCircle(pos, 2.2f, i * gap, 1.1f, () => DropLetter(pos, ch));
            }
            float t = 0;
            return dt => (t += dt) < 1.3f + n * gap;
        }

        void DropLetter(Vector3 pos, char ch)
        {
            var go = new GameObject("letter");
            var tm = go.AddComponent<TextMesh>();
            tm.text = ch.ToString();
            tm.font = Game.I.Hud.BigFont;
            tm.fontSize = 120;
            tm.characterSize = 0.09f;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.9f, 0.9f, 1f);
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            go.AddComponent<Billboard>();
            float y = 16, t = 0; bool landed = false;
            Fx.Run(dt =>
            {
                t += dt;
                if (!landed)
                {
                    y -= dt * 60;
                    if (y <= 0)
                    {
                        y = 0; landed = true; t = 0;
                        Sfx.Play("stomp", 0.9f, 1.1f);
                        Fx.Debris(pos + Vector3.up * 0.3f, new Color(0.8f, 0.75f, 0.64f), 12);
                        Fx.Ring(pos, 3f, SugaPurple, 0.3f);
                        Game.I.Cam.Shake(0.25f);
                        var pp = Game.I.Player.Pos;
                        if (Player.Flat(pp - pos).magnitude < 2.2f && pp.y < 3) Game.I.Player.TakeHit(140, pos);
                    }
                }
                else y = -t * 3; // ゆっくり地面に沈む
                go.transform.position = new Vector3(pos.x, y, pos.z);
                return !landed || t < 1.2f;
            }, go);
        }

        // ---- もらってみる？？：カウントの魂がしつこく追いかけてくる。触れるとカウント2つ ----
        Func<float, bool> AtkMorau()
        {
            var G = Game.I;
            Speak(SPick("morau"), 3f);
            Pose = "point";
            var orb = new GameObject("bullet");
            orb.transform.position = StickTip;
            Mat.Part(orb.transform, Mat.Sphere, Mat.ToonShared(new Color(0.12f, 0.08f, 0.16f), 0.03f), Vector3.zero, Vector3.one * 0.9f);
            var glow = Mat.Part(orb.transform, Mat.Quad, Mat.FxShared(new Color(0.7f, 0.2f, 1f, 0.8f), Mat.Glow, true, 3f), Vector3.zero, Vector3.one * 3f, default, false);
            glow.AddComponent<Billboard>();
            var label = new GameObject("label");
            label.transform.SetParent(orb.transform, false);
            label.transform.localPosition = new Vector3(0, 0.9f, 0);
            var tm = label.AddComponent<TextMesh>();
            tm.text = "+2"; tm.font = G.Hud.BigFont; tm.fontSize = 80; tm.characterSize = 0.05f;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = SugaRed;
            label.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            label.AddComponent<Billboard>();
            float speed = Phase == 2 ? 5.4f : 4.2f;
            float t = 0;
            Sfx.Play("laserCharge", 0.6f, 0.6f);
            Fx.Run(dt =>
            {
                t += dt;
                if (orb == null) return false;
                var P = Game.I.Player;
                var target = P.Pos + Vector3.up * 1.2f;
                var to = target - orb.transform.position;
                orb.transform.position += to.normalized * speed * dt + Vector3.up * Mathf.Sin(t * 5) * 0.02f;
                if (Random.value < 0.4f) Fx.Embers(orb.transform.position, new Color(0.7f, 0.2f, 1f), 1);
                if (to.magnitude < 1.2f && HitWithCount(60, orb.transform.position, 2))
                {
                    Fx.Sparks(orb.transform.position, new Color(0.7f, 0.2f, 1f), 20, 1f);
                    return false;
                }
                if (t > 6.5f || !Alive) { Fx.Glow(orb.transform.position, new Color(0.7f, 0.2f, 1f), 1f); return false; }
                return true;
            }, orb);
            float at = 0;
            return dt => (at += dt) < 1.4f;
        }

        // ---- 英検の壁：すき間のある文字の壁が押し寄せる。すき間を通り抜ける ----
        Func<float, bool> AtkWall()
        {
            var G = Game.I;
            var line = SPick("wall");
            Speak(line, 3f);
            // 言っちゃいけないことは放送禁止の「ピー」音でかき消す
            if (line.Contains("ピー")) Fx.Later(0.55f, () => Sfx.Play("bleep", 0.45f));
            Pose = "roar";
            int walls = Phase == 2 ? 4 : 3;
            for (int i = 0; i < walls; i++)
            {
                int k = i;
                Fx.Later(0.6f + k * 1.5f, () => { if (Alive && Game.I.State == Game.Mode.Battle) SpawnWall(); });
            }
            float t = 0;
            return dt => (t += dt) < 1.2f + walls * 1.5f;
        }

        void SpawnWall()
        {
            var P = Game.I.Player;
            // 壁はプレイヤーの向こう側からではなく、ボス側からプレイヤーに向かって進む
            var dir = Player.Flat(P.Pos - Pos);
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.back;
            var side = new Vector3(dir.z, 0, -dir.x);
            float startAlong = -World.ArenaR - 1;
            float gapCenter = Random.Range(-8f, 8f) + Vector3.Dot(Player.Flat(P.Pos), side) * 0.5f;
            const float gapW = 4.2f, speed = 9f;
            var root = new GameObject("ewall");
            var block = Mat.ToonShared(new Color(0.25f, 0.22f, 0.35f), 0.03f);
            const string letters = "EIKENJUNNIKYU";
            int idx = 0;
            // プレイヤーは壁の +Z 側から見るので、文字は +Z 面に裏返して貼り、並びも右から左に置く
            for (float x = World.ArenaR + 2; x >= -World.ArenaR - 2; x -= 2.1f)
            {
                if (Mathf.Abs(x - gapCenter) < gapW / 2) continue;
                var b = Mat.Part(root.transform, Mat.Cube, block, new Vector3(x, 1.6f, 0), new Vector3(1.9f, 3.2f, 0.8f), default, true);
                var lt = new GameObject("l");
                lt.transform.SetParent(b.transform, false);
                lt.transform.localPosition = new Vector3(0, -0.2f, 0.55f);
                lt.transform.localRotation = Quaternion.Euler(0, 180, 0);
                lt.transform.localScale = new Vector3(1 / 1.9f, 1 / 3.2f, 1 / 0.8f);
                var tm = lt.AddComponent<TextMesh>();
                tm.text = letters[idx++ % letters.Length].ToString();
                tm.font = Game.I.Hud.BigFont; tm.fontSize = 100; tm.characterSize = 0.1f;
                tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(1f, 0.85f, 0.4f);
                lt.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            }
            // すき間の目印（地面の光る帯）
            var mark = Fx.Obj("ring", Fx.BeamMesh, Mat.Fx(new Color(0.4f, 1f, 0.6f, 0.6f), Mat.Streak, true, 2f), Vector3.zero, Quaternion.identity, new Vector3(gapW, 1, 3));
            mark.transform.SetParent(root.transform, false);
            mark.transform.localPosition = new Vector3(gapCenter, 0.05f, -1.5f);
            root.transform.rotation = Quaternion.LookRotation(dir);
            float along = startAlong;
            bool hit = false;
            Sfx.Play("rumble", 0.9f, 0.8f);
            Fx.Run(dt =>
            {
                along += speed * dt;
                root.transform.position = dir * along;
                var pp = Game.I.Player.Pos;
                float pa = Vector3.Dot(Player.Flat(pp), dir), ps = Vector3.Dot(Player.Flat(pp), side);
                if (!hit && Mathf.Abs(pa - along) < 0.9f && Mathf.Abs(ps - gapCenter) > gapW / 2 - 0.3f && pp.y < 3.3f)
                    hit = HitWithCount(140, pp - dir);
                if (Random.value < 0.4f) Fx.Debris(dir * along + side * Random.Range(-15f, 15f), new Color(0.8f, 0.75f, 0.64f), 1);
                return along < World.ArenaR + 2 && Alive;
            }, root);
        }

        // ---- 答案返却：赤い×のついた答案が弧を描いて飛び、ブーメランのように戻ってくる ----
        Func<float, bool> AtkSheets()
        {
            Speak(SPick("sheets"), 2.8f);
            Pose = "point";
            int n = Phase == 2 ? 5 : 3;
            var P = Game.I.Player;
            float bas = Mathf.Atan2(P.Pos.x - Pos.x, P.Pos.z - Pos.z);
            Sfx.Play("paper", 1f, 0.8f);
            for (int i = 0; i < n; i++)
            {
                int k = i;
                Fx.Later(0.35f + k * 0.18f, () => { if (Alive && Game.I.State == Game.Mode.Battle) ThrowSheet(bas + (k - (n - 1) / 2f) * 0.45f, k % 2 == 0 ? 1 : -1); });
            }
            float t = 0;
            return dt => (t += dt) < 1.2f + n * 0.18f;
        }

        void ThrowSheet(float angle, int curve)
        {
            var g = new GameObject("sheet");
            Mat.Part(g.transform, Mat.Cube, Mat.ToonShared(new Color(0.97f, 0.97f, 0.94f), 0.01f), Vector3.zero, new Vector3(1.1f, 0.03f, 1.5f), default, false);
            var red = Mat.FxShared(new Color(SugaRed.r, SugaRed.g, SugaRed.b, 1f), Mat.White, true, 2.5f);
            Mat.Part(g.transform, Mat.Cube, red, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.02f, 0.12f), new Vector3(0, 45, 0), false);
            Mat.Part(g.transform, Mat.Cube, red, new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.02f, 0.12f), new Vector3(0, -45, 0), false);
            var glow = Mat.Part(g.transform, Mat.Quad, Mat.FxShared(new Color(1f, 0.3f, 0.35f, 0.6f), Mat.Glow, true, 2f), Vector3.zero, Vector3.one * 2.2f, default, false);
            glow.AddComponent<Billboard>();
            var origin = new Vector3(Pos.x, 1.2f, Pos.z);
            var dir = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            var side = new Vector3(dir.z, 0, -dir.x) * curve;
            const float T = 2.4f, R = 16f;
            float t = 0; bool hit = false;
            Fx.Run(dt =>
            {
                t += dt;
                float u = t / T;
                var p = origin + dir * (Mathf.Sin(u * Mathf.PI) * R) + side * (Mathf.Sin(u * Mathf.PI * 2) * 3.5f);
                g.transform.position = p;
                g.transform.rotation = Quaternion.Euler(0, t * 900, 0);
                if (Random.value < 0.3f) Fx.Sparks(p, SugaRed, 1, 0.3f);
                var P = Game.I.Player;
                if (!hit && new Vector2(p.x - P.Pos.x, p.z - P.Pos.z).magnitude < 1.0f && P.Pos.y < 1.9f)
                    hit = HitWithCount(90, p);
                return u < 1 && Alive;
            }, g);
        }

        // ---- リスニングテスト：音の輪が何重にも広がる。ジャンプでよける ----
        Func<float, bool> AtkListen()
        {
            Speak(SPick("listen"), 2.8f);
            Pose = "raise";
            int waves = Phase == 2 ? 4 : 3;
            for (int i = 0; i < waves; i++)
            {
                int k = i;
                Fx.Later(0.7f + k * 0.75f, () =>
                {
                    if (!Alive || Game.I.State != Game.Mode.Battle) return;
                    var center = Player.Flat(Pos);
                    bool hit = false;
                    Sfx.Play("wave", 0.8f, 1.3f - k * 0.08f);
                    Sfx.Play("whisper" + Random.Range(0, 3), 0.5f, 1.2f);
                    Game.I.Hud.WorldText(Pos + Vector3.up * 5.5f, k % 2 == 0 ? "Listen!" : "♪", SugaPurple, 1f);
                    Fx.ShockWall(center, SugaPurple, (r, dt) =>
                    {
                        var P = Game.I.Player;
                        float d = Player.Flat(P.Pos - center).magnitude;
                        if (!hit && Mathf.Abs(d - r) < 0.6f && P.Pos.y < 0.55f) hit = HitWithCount(110, center);
                        return Alive;
                    });
                });
            }
            float t = 0;
            return dt => (t += dt) < 1.0f + waves * 0.75f;
        }

        // ---- 赤ペン添削：アリーナに赤ペンの線が引かれ、線にそって爆発する ----
        Func<float, bool> AtkRedPen()
        {
            Speak(SPick("redpen"), 2.8f);
            Pose = "point";
            int n = Phase == 2 ? 5 : 3;
            var P = Game.I.Player;
            Sfx.Play("warn", 0.6f, 0.9f);
            for (int i = 0; i < n; i++)
            {
                int k = i;
                Fx.Later(k * 0.3f, () =>
                {
                    if (!Alive || Game.I.State != Game.Mode.Battle) return;
                    var pp = Player.Flat(Game.I.Player.Pos);
                    float a = Random.value * Mathf.PI;
                    var dir = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var nrm = new Vector3(dir.z, 0, -dir.x);
                    // 1本目はプレイヤーの真上、あとは少しずらす
                    var center = pp + nrm * (k == 0 ? 0 : Random.Range(-4f, 4f));
                    var from = center - dir * 26f;
                    Fx.TelegraphBand(from, dir, 52f, 2.2f, 1.0f);
                    Fx.Later(1.0f, () =>
                    {
                        if (!Alive || Game.I.State != Game.Mode.Battle) return;
                        Sfx.Play("slash3", 0.8f, 0.7f);
                        Sfx.Play("explode", 0.5f, 1.3f);
                        for (float s = -24; s <= 24; s += 3f)
                        {
                            var q = center + dir * s;
                            if (q.magnitude > World.ArenaR) continue;
                            Fx.Explosion(q + Vector3.up * 0.4f, SugaRed, 0.45f);
                        }
                        Fx.SlashLine(center + Vector3.up * 0.6f, SugaRed, 20f);
                        Game.I.Cam.Shake(0.25f);
                        var pl = Game.I.Player.Pos;
                        var rel = Player.Flat(pl) - center;
                        float dist = Mathf.Abs(Vector3.Dot(rel, nrm));
                        if (dist < 1.3f && pl.y < 1.8f) HitWithCount(120, Player.Flat(pl) - nrm * Mathf.Sign(Vector3.Dot(rel, nrm)));
                    });
                });
            }
            float t = 0;
            return dt => (t += dt) < 1.3f + n * 0.3f;
        }

        // ---- 北の破壊神（本気モードの必殺）：空から破壊の柱を5回落とす ----
        Func<float, bool> AtkHakai()
        {
            var G = Game.I;
            Speak(SPick("hakai"), 3f);
            G.OnHakai();
            Pose = "raise";
            lockFace = false;
            // 「正」の字（カウント5）の5画が、巨大な壁になって順番に落ちてくる。画と画のすき間が安全地帯。
            // 最後に5画がそろうと中心から衝撃波（ジャンプでよける）
            var P = G.Player;
            var center = ClampArena(Player.Flat(P.Pos), 6f);
            var up = Player.Flat(P.Pos - Pos);
            up = up.sqrMagnitude > 0.01f ? up.normalized : Vector3.forward;
            var right = new Vector3(up.z, 0, -up.x);
            const float S = 11f, W = 2.6f, H = 9f, gap = 0.34f, warn = 0.5f;
            // 画の始点と終点（字の中心が原点、x が右、y が上）
            Vector2[,] strokes =
            {
                { new Vector2(-0.95f, 0.9f), new Vector2(0.95f, 0.9f) },
                { new Vector2(0f, 0.9f), new Vector2(0f, -0.9f) },
                { new Vector2(0f, 0.05f), new Vector2(0.7f, 0.05f) },
                { new Vector2(-0.6f, 0.05f), new Vector2(-0.6f, -0.9f) },
                { new Vector2(-1.05f, -0.9f), new Vector2(1.05f, -0.9f) },
            };
            Vector3 W2(Vector2 c) => center + right * (c.x * S) + up * (c.y * S);
            for (int i = 0; i < 5; i++)
            {
                int k = i;
                var a = W2(strokes[k, 0]); var b = W2(strokes[k, 1]);
                Fx.Later(k * gap, () =>
                {
                    if (!Alive || Game.I.State != Game.Mode.Battle) return;
                    var dir = Player.Flat(b - a); float len = dir.magnitude; dir /= len;
                    Fx.TelegraphBand(a - dir * W * 0.5f, dir, len + W, W, warn);
                    Sfx.Play("warn", 0.4f, 1.2f + k * 0.05f);
                    Fx.Later(warn, () => SlamStroke(a, b, W, H, k));
                });
            }
            // 5画そろったら、字の中心から大きな衝撃波
            Fx.Later(4 * gap + warn + 0.5f, () =>
            {
                if (!Alive || Game.I.State != Game.Mode.Battle) return;
                Game.I.Hud.WorldText(center + Vector3.up * 6f, "カウント５", SugaRed, 1.6f);
                Sfx.Play("boom", 1f, 0.6f);
                Sfx.Play("roar", 0.6f, 0.6f);
                Game.I.Cam.Shake(0.8f);
                PostFX.I?.Flash(new Color(0.7f, 0.4f, 1f), 0.4f);
                bool hitW = false;
                Fx.ShockWall(center, SugaPurple, (r, dt) =>
                {
                    var p2 = Game.I.Player;
                    float d = Player.Flat(p2.Pos - center).magnitude;
                    if (!hitW && Mathf.Abs(d - r) < 0.7f && p2.Pos.y < 0.6f) hitW = HitWithCount(130, center);
                    return r < 22 && Alive;
                });
            });
            float t = 0, total = 4 * gap + warn + 1.6f;
            return dt =>
            {
                t += dt;
                Y = t < total - 0.4f ? Mathf.Lerp(Y, 4.5f, dt * 3) : Mathf.Lerp(Y, 0, dt * 10);
                if (Random.value < 0.5f) Fx.Embers(Pos + Vector3.up * (Y + Random.Range(0f, 5f)) + Random.insideUnitSphere * 2, SugaPurple, 1);
                if (t >= total) { Y = 0; Sfx.Play("stomp", 1f, 0.8f); Game.I.Cam.Shake(0.4f); return false; }
                return true;
            };
        }

        // 「正」の1画：空から巨大な壁が落ちてきて、しばらく残る
        void SlamStroke(Vector3 a, Vector3 b, float w, float h, int k)
        {
            if (!Alive || Game.I.State != Game.Mode.Battle) return;
            var mid = (a + b) * 0.5f;
            var dir = Player.Flat(b - a); float len = dir.magnitude; dir /= len;
            var m = Mat.Fx(new Color(SugaPurple.r, SugaPurple.g, SugaPurple.b, 0.75f), Mat.WallTex, true, 1.7f);
            var core = Mat.Fx(new Color(1f, 0.35f, 0.45f, 0.9f), Mat.WallTex, true, 1.6f);
            var go = Fx.Obj("pillar", Mat.Cube, m, new Vector3(mid.x, h * 0.5f, mid.z), Quaternion.LookRotation(dir), new Vector3(w, h, len + w));
            var inner = Mat.Part(go.transform, Mat.Cube, core, Vector3.zero, new Vector3(0.35f, 1f, 1f), default, false);
            _ = inner;
            Sfx.Play("boom", 0.9f, 0.85f + k * 0.04f);
            Sfx.Play("stomp", 0.8f, 0.7f);
            Game.I.Cam.Shake(0.5f);
            PostFX.I?.Chroma(0.2f);
            for (float s = 0; s <= len; s += 3f) Fx.Debris(a + dir * s + Vector3.up * 0.3f, new Color(0.35f, 0.3f, 0.4f), 3);
            Fx.Bolt(mid, SugaPurple, 0.5f, 30);
            bool hit = false;
            float t = 0;
            Fx.Run(dt =>
            {
                t += dt;
                // 空から落ちてきて、1.1秒のこって、しずんで消える
                float drop = Mathf.Clamp01(t / 0.12f);
                float sink = t > 1.1f ? Mathf.Clamp01((t - 1.1f) / 0.3f) : 0;
                go.transform.position = new Vector3(mid.x, h * 0.5f + (1 - drop) * 14f - sink * h, mid.z);
                if (!hit && drop >= 1 && sink < 0.5f && Alive)
                {
                    var pl = Game.I.Player;
                    var rel = Player.Flat(pl.Pos) - a;
                    float along = Vector3.Dot(rel, dir), across = Mathf.Abs(Vector3.Dot(rel, new Vector3(dir.z, 0, -dir.x)));
                    if (along > -w * 0.5f && along < len + w * 0.5f && across < w * 0.5f + 0.4f && pl.Pos.y < h)
                        hit = HitWithCount(160, pl.Pos - new Vector3(dir.z, 0, -dir.x) * Mathf.Sign(Vector3.Dot(rel, new Vector3(dir.z, 0, -dir.x))));
                }
                return t < 1.4f;
            }, go);
        }
    }
}
