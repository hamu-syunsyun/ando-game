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
            { "wall", new[] { "英検準2級も取れないの？論外。", "英検の壁、越えられる？" } },
            { "hakai", new[] { "北の破壊神、と呼ばれています。", "ここから先は、破壊の時間。" } },
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
            var hairM = M(new Color(0.22f, 0.22f, 0.26f));
            var dark = M(new Color(0.05f, 0.04f, 0.07f), 0);

            // 長い脚
            legL = Mat.Pivot(inner, "legL", new Vector3(-0.32f, 1.8f, 0));
            legR = Mat.Pivot(inner, "legR", new Vector3(0.32f, 1.8f, 0));
            foreach (var l in new[] { legL, legR })
            {
                Mat.Part(l, Mat.Frustum(0.17f, 0.22f, 1.8f, 12), coat, new Vector3(0, -0.9f, 0), Vector3.one);
                Mat.Part(l, Mat.Sphere, dark, new Vector3(0, -1.78f, 0.14f), new Vector3(0.38f, 0.2f, 0.7f));
            }
            // すそが広がったロングコート
            Mat.Part(inner, Mat.Frustum(1.15f, 0.75f, 2.8f, 20), coat, new Vector3(0, 2.8f, 0), Vector3.one);
            Mat.Part(inner, Mat.Cube, shirt, new Vector3(0, 3.7f, 0.72f), new Vector3(0.42f, 1.0f, 0.1f), new Vector3(-8, 0, 0));
            Mat.Part(inner, Mat.Cube, tie, new Vector3(0, 3.6f, 0.78f), new Vector3(0.16f, 0.85f, 0.08f), new Vector3(-8, 0, 0));
            // 高い襟
            Mat.Part(inner, Mat.Frustum(0.62f, 0.72f, 0.7f, 16, true), coat, new Vector3(0, 4.35f, -0.05f), Vector3.one);

            headT = Mat.Pivot(inner, "head", new Vector3(0, 4.45f, 0));
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.72f, 0), new Vector3(1.45f, 1.65f, 1.45f));
            // オールバックの髪
            Mat.Part(headT, Mat.Sphere, hairM, new Vector3(0, 0.95f, -0.18f), new Vector3(1.5f, 1.45f, 1.45f));
            Mat.Part(headT, Mat.Sphere, hairM, new Vector3(0, 1.25f, 0.2f), new Vector3(1.2f, 0.55f, 1.0f), new Vector3(-15, 0, 0));
            // くぼんだ目のまわり（影）と、赤く光る目
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                Mat.Part(headT, Mat.Sphere, M(new Color(0.35f, 0.3f, 0.38f), 0), new Vector3(x, 0.78f, 0.6f), new Vector3(0.36f, 0.22f, 0.12f), default, false);
                Mat.Part(headT, Mat.Cube, dark, new Vector3(x, 0.98f, 0.64f), new Vector3(0.36f, 0.05f, 0.05f), new Vector3(0, 0, x < 0 ? -25 : 25), false);
            }
            eyeGlow = Mat.Fx(new Color(1, 0.15f, 0.2f, 0.9f), Mat.Glow, true, 3f);
            foreach (float x in new[] { -0.27f, 0.27f })
            {
                var e = Mat.Part(headT, Mat.Quad, eyeGlow, new Vector3(x, 0.78f, 0.7f), Vector3.one * 0.45f, default, false);
                e.AddComponent<Billboard>();
            }
            Mat.Part(headT, Mat.Sphere, skin, new Vector3(0, 0.6f, 0.72f), new Vector3(0.14f, 0.26f, 0.14f));
            // 薄い笑みの口
            Mat.Part(headT, Mat.Cube, M(new Color(0.35f, 0.1f, 0.12f), 0), new Vector3(0, 0.3f, 0.66f), new Vector3(0.4f, 0.04f, 0.04f), new Vector3(0, 0, -6), false);

            // 長い腕
            Transform MkArm(float x)
            {
                var p = Mat.Pivot(inner, "arm", new Vector3(x, 4.0f, 0));
                Mat.Part(p, Mat.Frustum(0.15f, 0.2f, 1.9f, 12), coat, new Vector3(0, -0.92f, 0), Vector3.one);
                Mat.Part(p, Mat.Sphere, skin, new Vector3(0, -1.95f, 0), Vector3.one * 0.36f);
                return p;
            }
            armL = MkArm(-0.95f); armR = MkArm(0.95f);

            // 死神の鎌（赤ペン）：長い柄と、赤く光る曲がった刃
            var pole = M(new Color(0.15f, 0.12f, 0.14f), 0.02f);
            Mat.Part(armR, Mat.Frustum(0.07f, 0.07f, 4.4f, 8), pole, new Vector3(0, -1.95f, 1.0f), Vector3.one, new Vector3(90, 0, 0));
            var blade = Mat.Toon(SugaRed, 0.03f, new Color(1f, 0.1f, 0.15f, 0.5f)); mats.Add(blade);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 6f;
                var pos = new Vector3(0, -1.95f + a * 1.4f * 0.2f + Mathf.Sin(a * Mathf.PI * 0.8f) * 1.3f, 3.2f - a * 1.7f + Mathf.Sin(a * 2.2f) * 0.2f);
                Mat.Part(armR, Mat.Cube, blade, pos, new Vector3(0.06f, 0.35f - a * 0.2f, 0.55f), new Vector3(-a * 70 + 20, 0, 0));
            }
            stickTip = Mat.Pivot(armR, "tip", new Vector3(0, -1.95f, 3.2f));

            // 周りを回る英語の辞書
            bookOrbit = Mat.Pivot(inner, "bookOrbit", new Vector3(0, 4.0f, 0));
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
            var opts = new List<string> { "count", "scythe", "words", "morau", "wall" };
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
            G.Say(SPick("count"));
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
            G.Say(SPick("scythe"));
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
            G.Say(SPick("words"));
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
            G.Say(SPick("morau"), 3f);
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
            G.Say(SPick("wall"), 3f);
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

        // ---- 北の破壊神（本気モードの必殺）：空から破壊の柱を5回落とす ----
        Func<float, bool> AtkHakai()
        {
            var G = Game.I;
            G.Say(SPick("hakai"), 3f);
            G.OnHakai();
            Pose = "raise";
            lockFace = false;
            int n = 5;
            for (int i = 0; i < n; i++)
            {
                int k = i;
                Fx.Later(0.9f + k * 0.85f, () =>
                {
                    if (!Alive || Game.I.State != Game.Mode.Battle) return;
                    var target = ClampArena(Player.Flat(Game.I.Player.Pos));
                    Fx.TelegraphCircle(target, 4f, 0, 0.8f, () =>
                    {
                        Fx.Pillar(target, SugaPurple, 4f, 30f, 0.9f);
                        Fx.Explosion(target + Vector3.up, SugaPurple, 1.8f);
                        Fx.Bolt(target, SugaPurple, 0.8f, 40);
                        Sfx.Play("boom", 1f, 0.8f);
                        Sfx.Play("thunder", 0.7f, 0.7f);
                        Game.I.Cam.Shake(0.6f);
                        PostFX.I?.Chroma(0.2f);
                        var pp = Game.I.Player.Pos;
                        if (Player.Flat(pp - target).magnitude < 4f && pp.y < 3) Game.I.Player.TakeHit(200, target);
                        // 着弾点から衝撃波（ジャンプでよける）
                        bool hitW = false;
                        Fx.ShockWall(target, SugaPurple, (r, dt) =>
                        {
                            var p2 = Game.I.Player;
                            float d = Player.Flat(p2.Pos - target).magnitude;
                            if (!hitW && Mathf.Abs(d - r) < 0.6f && p2.Pos.y < 0.55f) hitW = p2.TakeHit(120, target);
                            return r < 14 && Alive;
                        });
                    });
                });
            }
            float t = 0, total = 1.0f + n * 0.85f + 1.2f;
            return dt =>
            {
                t += dt;
                Y = t < total - 0.4f ? Mathf.Lerp(Y, 4.5f, dt * 2) : Mathf.Lerp(Y, 0, dt * 10);
                if (Random.value < 0.5f) Fx.Embers(Pos + Vector3.up * (Y + Random.Range(0f, 5f)) + Random.insideUnitSphere * 2, SugaPurple, 1);
                if (t >= total) { Y = 0; Sfx.Play("stomp", 1f, 0.8f); Game.I.Cam.Shake(0.4f); return false; }
                return true;
            };
        }
    }
}
