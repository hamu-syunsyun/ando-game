using UnityEngine;

namespace AndoBoss
{
    // 3人の特技（E）と奥義（Q）
    public static class Skills
    {
        static Game G => Game.I;
        static Boss B => Game.I.Boss;

        // ================= 特技 =================

        // ともき「レポート提出」：雷をまとったレポートを投げつける。刺さるとバババババッと連続ヒット
        public static void Report(Player p)
        {
            var ec = p.Def.ElemColor;
            Sfx.Play("paper", 1f);
            var go = new GameObject("report");
            var pos = p.HandPos + Vector3.up * 0.3f;
            go.transform.position = pos;
            // 紙の束（白い板＋赤い綴じひも）と、まとった雷
            Mat.Part(go.transform, Mat.Cube, Mat.ToonShared(new Color(0.97f, 0.97f, 0.94f), 0.01f), Vector3.zero, new Vector3(0.5f, 0.05f, 0.7f), default, false);
            Mat.Part(go.transform, Mat.Cube, Mat.ToonShared(new Color(0.8f, 0.2f, 0.2f)), new Vector3(-0.2f, 0.03f, 0), new Vector3(0.04f, 0.02f, 0.72f), default, false);
            var glow = Mat.Part(go.transform, Mat.Quad, Mat.FxShared(new Color(ec.r, ec.g, ec.b, 0.9f), Mat.Glow, true, 3f), Vector3.zero, Vector3.one * 1.8f, default, false);
            glow.AddComponent<Billboard>();
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.2f; tr.widthMultiplier = 0.5f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(ec, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            tr.sharedMaterial = Mat.FxShared(Color.white, Mat.White, true, 2f);

            float t = 0; bool stuck = false; Vector3 offset = Vector3.zero;
            var dir = p.Forward;
            Fx.Run(dt =>
            {
                t += dt;
                if (!stuck)
                {
                    Vector3 target = B.Alive ? B.Pos + Vector3.up * (B.Y + 2.4f) : go.transform.position + dir * 5;
                    var to = target - go.transform.position;
                    float step = 26 * dt;
                    go.transform.Rotate(0, 900 * dt, 0, Space.World);
                    if (Random.value < 0.5f) Fx.Sparks(go.transform.position, ec, 1, 0.4f);
                    if (B.Alive && to.magnitude < Mathf.Max(0.7f, step) && Player.Flat(B.Pos - p.Pos).magnitude < 16)
                    {
                        stuck = true;
                        offset = go.transform.position - B.Pos;
                        Multi(ec);
                        return true;
                    }
                    go.transform.position += (B.Alive && Player.Flat(B.Pos - p.Pos).magnitude < 16 ? to.normalized : dir) * step;
                    return t < 1.2f;
                }
                go.transform.position = B.Pos + offset + Random.insideUnitSphere * 0.08f;
                return t < 2.0f && B.Alive;
            }, go);
        }

        static void Multi(Color ec)
        {
            G.Hud.WorldText(B.Pos + Vector3.up * 5.5f, "バババババッ！！", ec, 1.2f);
            G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 3);
            for (int i = 0; i < 10; i++)
            {
                int k = i;
                Fx.Later(k * 0.07f, () =>
                {
                    if (!B.Alive) return;
                    var hp = B.Pos + Vector3.up * (B.Y + 2.4f) + Random.insideUnitSphere * 0.9f;
                    G.DamageBoss(30, Game.HitKind.Skill, hp, k == 0 ? Elem.Electro : Elem.None, true);
                    Sfx.Play("multihit", 0.8f, 1 + Random.Range(-0.1f, 0.15f));
                    Fx.Sparks(hp, Color.Lerp(ec, Color.white, 0.4f), 8, 1f);
                    if (k % 3 == 0) Fx.Bolt(B.Pos + Random.insideUnitSphere * 1.2f, ec, 0.2f, 7f);
                    G.Cam.Shake(0.12f);
                });
            }
            Fx.Later(0.8f, () =>
            {
                if (!B.Alive) return;
                G.DamageBoss(150, Game.HitKind.Skill, B.Pos + Vector3.up * (B.Y + 2.8f), Elem.Electro);
                Fx.Bolt(B.Pos, ec, 0.45f);
                Fx.Ring(B.Pos, 5, ec, 0.35f);
                Sfx.Play("thunder", 0.7f, 1.2f);
                G.Cam.Shake(0.3f);
            });
        }

        // 杉山くん「炎のロングパス」：燃えさかるラグビーボールを全力で投げる。
        // 着弾で大爆発＋火柱の輪。当たるとボスの防御力が8秒間下がる
        public static void RugbyPass(Player p)
        {
            var ec = p.Def.ElemColor;
            Sfx.Play("whoosh", 1f, 0.6f);
            Sfx.Play("fire", 0.8f, 0.8f);
            G.Cam.FovPunch(-5);
            G.Cam.Shake(0.2f);
            Fx.Explosion(p.HandPos, ec, 0.8f);
            var ball = new GameObject("rugby");
            Mat.Part(ball.transform, Mat.Sphere, Mat.ToonShared(new Color(0.55f, 0.3f, 0.15f), 0.02f), Vector3.zero, new Vector3(0.55f, 0.55f, 0.9f));
            Mat.Part(ball.transform, Mat.Cube, Mat.ToonShared(Color.white), new Vector3(0, 0.27f, 0), new Vector3(0.06f, 0.03f, 0.5f), default, false);
            var glow = Mat.Part(ball.transform, Mat.Quad, Mat.FxShared(new Color(ec.r, ec.g, ec.b, 1f), Mat.Glow, true, 3.5f), Vector3.zero, Vector3.one * 3.2f, default, false);
            glow.AddComponent<Billboard>();
            var tr = ball.AddComponent<TrailRenderer>();
            tr.time = 0.35f; tr.widthMultiplier = 1.4f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(ec, 0.3f), new GradientColorKey(new Color(0.6f, 0.1f, 0f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            tr.sharedMaterial = Mat.FxShared(Color.white, Mat.White, true, 2.5f);
            var start = p.HandPos + Vector3.up * 0.5f;
            var target = B.Alive && Player.Flat(B.Pos - p.Pos).magnitude < 20 ? B.Pos + Vector3.up * 2f : p.Pos + p.Forward * 12;
            float t = 0; const float dur = 0.6f;
            Fx.Run(dt =>
            {
                t += dt;
                float u = Mathf.Clamp01(t / dur);
                var tgt = B.Alive ? Vector3.Lerp(target, B.Pos + Vector3.up * 2f, u) : target;
                ball.transform.position = Vector3.Lerp(start, tgt, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 4f;
                ball.transform.rotation = Quaternion.LookRotation((tgt - start).normalized) * Quaternion.Euler(0, 0, t * 1200);
                Fx.Embers(ball.transform.position, ec, 5);
                if (Random.value < 0.5f) Fx.Sparks(ball.transform.position, ec, 2, 0.5f);
                if (u < 1) return true;

                // ---- 着弾：ド派手に ----
                var hp = ball.transform.position;
                var ground = Player.Flat(hp);
                Fx.Explosion(hp, ec, 3f);
                Fx.Explosion(hp + Vector3.up, Color.white, 1.2f);
                Fx.Pillar(ground, ec, 3.5f, 14f, 1.0f);
                for (int i = 0; i < 6; i++)
                {
                    int k = i;
                    Fx.Later(0.08f * k, () =>
                    {
                        float a = k / 6f * Mathf.PI * 2;
                        var pp = ground + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4.5f;
                        Fx.Pillar(pp, ec, 1.4f, 7f, 0.8f);
                        Fx.Explosion(pp + Vector3.up * 0.5f, ec, 0.7f);
                        Sfx.Play("fire", 0.5f, 1.1f + k * 0.05f);
                    });
                }
                Fx.Ring(ground, 9f, ec, 0.5f, 3f);
                Fx.Ring(ground, 6f, Color.white, 0.3f, 3f);
                Fx.Scorch(ground, 4f);
                Sfx.Play("explode", 1f, 0.9f);
                Sfx.Play("boom", 0.8f, 0.8f);
                G.Cam.Shake(0.7f);
                PostFX.I?.Flash(new Color(1f, 0.6f, 0.3f), 0.5f);
                PostFX.I?.Radial(0.5f);
                Sky.Flash(0.5f);
                Fx.HitStop(0.08f);
                G.Hud.WorldText(hp + Vector3.up * 3f, "トライ！！", ec, 1.5f);
                if (B.Alive && Player.Flat(B.Pos - hp).magnitude < 4.5f + Boss.Radius)
                {
                    G.DamageBoss(230, Game.HitKind.Skill, B.Pos + Vector3.up * 2.4f, Elem.Pyro);
                    B.DefDownT = 8f;
                    Fx.Later(0.25f, () => G.Hud.WorldText(B.Pos + Vector3.up * 5f, "防御ダウン！", new Color(1f, 0.6f, 0.3f), 1.1f));
                    G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 2);
                }
                return false;
            }, ball);
        }

        // やましょうの通常攻撃：風をまとった矢（count 本を少し広げて撃つ）
        public static void Arrow(Player p, float dmg, int count, Elem elem)
        {
            var ec = p.Def.ElemColor;
            for (int i = 0; i < count; i++)
            {
                int k = i;
                float spread = (k - (count - 1) / 2f) * 0.12f;
                Shoot(p.HandPos + Vector3.up * 0.1f, p.Face + spread, ec, 1f, 45f, target =>
                {
                    G.DamageBoss(dmg, Game.HitKind.Normal, target, k == 0 ? elem : Elem.None);
                });
            }
        }

        static void Shoot(Vector3 from, float face, Color c, float size, float speed, System.Action<Vector3> onHit)
        {
            var go = new GameObject("arrow");
            go.transform.position = from;
            Mat.Part(go.transform, Mat.Cylinder, Mat.ToonShared(new Color(0.9f, 0.9f, 0.85f)), Vector3.zero, new Vector3(0.05f, 0.8f, 0.05f) * size, new Vector3(90, 0, 0), false);
            Mat.Part(go.transform, Mat.Frustum(0.07f, 0f, 0.2f, 6), Mat.ToonShared(new Color(0.7f, 0.75f, 0.8f)), new Vector3(0, 0, 0.5f * size), Vector3.one * size, new Vector3(90, 0, 0), false);
            var glow = Mat.Part(go.transform, Mat.Quad, Mat.FxShared(new Color(c.r, c.g, c.b, 0.9f), Mat.Glow, true, 2.5f), new Vector3(0, 0, 0.4f * size), Vector3.one * 0.9f * size, default, false);
            glow.AddComponent<Billboard>();
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.15f; tr.widthMultiplier = 0.25f * size;
            tr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(c, 1) }, new[] { new GradientAlphaKey(0.8f, 0), new GradientAlphaKey(0, 1) });
            tr.colorGradient = g;
            tr.sharedMaterial = Mat.FxShared(Color.white, Mat.White, true, 2f);
            var dir = new Vector3(Mathf.Sin(face), 0, Mathf.Cos(face));
            float t = 0;
            Fx.Run(dt =>
            {
                t += dt;
                var pos = go.transform.position;
                // ボスの方へ少しだけ曲がる
                if (B.Alive)
                {
                    var to = (B.Pos + Vector3.up * (B.Y + 2.3f)) - pos;
                    if (to.magnitude < 20) dir = Vector3.Slerp(dir, to.normalized, dt * 5);
                    if (to.magnitude < 1.4f + Boss.Radius * 0.5f)
                    {
                        onHit(pos);
                        Fx.Sparks(pos, c, 8, 0.8f);
                        return false;
                    }
                }
                go.transform.position = pos + dir * speed * dt;
                go.transform.rotation = Quaternion.LookRotation(dir);
                return t < 1.2f;
            }, go);
        }

        // やましょう「二度寝アロー」：空に矢を放つと、しばらくして矢の雨が降る
        public static void NidoneArrow(Player p)
        {
            var ec = p.Def.ElemColor;
            Sfx.Play("arrow", 1f, 0.8f);
            G.Hud.WorldText(p.Pos + Vector3.up * 2.6f, "あと5分……", new Color(0.8f, 1f, 0.9f), 0.8f);
            Fx.Sparks(p.HandPos, ec, 10, 1f);
            var center = B.Alive && Player.Flat(B.Pos - p.Pos).magnitude < 20 ? Player.Flat(B.Pos) : Player.Flat(p.Pos + p.Forward * 8);
            Fx.Ring(center, 4.5f, ec, 0.6f);
            bool first = true;
            for (int i = 0; i < 14; i++)
            {
                int k = i;
                Fx.Later(0.6f + k * 0.07f, () =>
                {
                    var c2 = B.Alive ? Player.Flat(B.Pos) : center;
                    var land = c2 + new Vector3(Random.Range(-3.5f, 3.5f), 0, Random.Range(-3.5f, 3.5f));
                    Fx.SlashLine(land + Vector3.up * 4, ec, 7);
                    Fx.Debris(land + Vector3.up * 0.2f, new Color(0.8f, 1f, 0.9f, 0.7f), 3);
                    Sfx.Play("multihit", 0.4f, 1.3f);
                    if (B.Alive && Player.Flat(B.Pos - land).magnitude < Boss.Radius + 1.8f)
                    {
                        G.DamageBoss(42, Game.HitKind.Skill, B.Pos + Vector3.up * 2.5f + Random.insideUnitSphere, first ? Elem.Wind : Elem.None, true);
                        if (first) { first = false; G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 2); }
                    }
                });
            }
        }

        static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);

        // ================= 奥義 =================

        // ともき「一夜漬け・雷光乱舞」：夜が来て、目にも止まらぬ連続斬り → 〆切斬り
        public static void TomokiBurst(Player p)
        {
            var ec = p.Def.ElemColor;
            if (B.Alive)
            {
                var d = Player.Flat(p.Pos - B.Pos);
                if (d.sqrMagnitude < 0.01f) d = Vector3.back;
                p.Pos = B.Pos + d.normalized * (Boss.Radius + 1.3f);
                p.Face = Mathf.Atan2(-d.x, -d.z);
            }
            PostFX.I?.SetTint(new Color(0.65f, 0.7f, 1f));
            Sky.SetStorm(1);
            for (int i = 0; i < 14; i++)
            {
                int k = i;
                Fx.Later(0.05f + k * 0.075f, () =>
                {
                    if (!B.Alive || G.State != Game.Mode.Battle) return;
                    var c = B.Pos + Vector3.up * (B.Y + 2.4f);
                    Fx.SlashLine(c, k % 2 == 0 ? Color.white : ec, 7f);
                    Sfx.Play(k % 2 == 0 ? "slash0" : "slash1", 0.7f, 1.2f, 0.15f);
                    Sfx.Play("multihit", 0.6f);
                    G.DamageBoss(55, Game.HitKind.Burst, c + Random.insideUnitSphere, k == 0 || k == 7 ? Elem.Electro : Elem.None, true);
                    G.Cam.Shake(0.15f);
                });
            }
            Fx.Later(1.25f, () =>
            {
                PostFX.I?.SetTint(Color.white);
                if (B.Phase == 1) Sky.SetStorm(0);
                if (!B.Alive || G.State != Game.Mode.Battle) return;
                for (int k = 0; k < 4; k++) Fx.Bolt(B.Pos + Random.insideUnitSphere * 1.5f, k % 2 == 0 ? Color.white : ec, 0.8f, 34);
                Fx.SlashLine(B.Pos + Vector3.up * 2.5f, Color.white, 14f);
                Fx.Ring(B.Pos, 11, ec, 0.6f, 3f);
                G.Hud.WorldText(B.Pos + Vector3.up * 6f, "〆切斬り！", Mat.Gold, 1.4f);
                Finale(420, Elem.Electro);
            });
        }

        // 杉山くん「ラグビー部タックル」：炎をまとって何度もボスに突っこみ、最後に全力タックル
        public static void SugiyamaBurst(Player p)
        {
            var ec = p.Def.ElemColor;
            p.LockT = 2.2f;
            p.Inv = Mathf.Max(p.Inv, 2.6f);
            int passes = 3; float passDur = 0.32f;
            float t = 0; int pass = -1; Vector3 from = p.Pos, to = p.Pos; bool hitThis = false;
            Fx.Run(dt =>
            {
                if (!B.Alive || G.State != Game.Mode.Battle) { p.LockT = 0; return false; }
                t += dt;
                // 最後の突進（pass == passes）より先には進まないようにする
                int cur = Mathf.Min(Mathf.FloorToInt(t / passDur), passes);
                if (cur != pass)
                {
                    pass = cur;
                    hitThis = false;
                    from = p.Pos;
                    if (pass < passes)
                    {
                        // ボスを通り抜けて反対側へ
                        float a = Random.value * Mathf.PI * 2;
                        var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        from = B.Pos - d * 6f; to = B.Pos + d * 6f;
                        p.Pos = new Vector3(from.x, 0, from.z);
                        Sfx.Play("whoosh", 0.9f, 0.7f);
                    }
                    else
                    {
                        var d = Player.Flat(B.Pos - p.Pos).normalized;
                        to = B.Pos - d * (Boss.Radius + 0.8f);
                    }
                }
                float u = Mathf.Clamp01((t - pass * passDur) / passDur);
                var pos = Vector3.Lerp(from, to, u);
                p.Pos = new Vector3(pos.x, 0, pos.z);
                var dir = Player.Flat(to - from);
                if (dir.sqrMagnitude > 0.01f) p.Face = Mathf.Atan2(dir.x, dir.z);
                Fx.Embers(p.Pos + Vector3.up * Random.Range(0.3f, 1.6f), ec, 6);
                Fx.Sparks(p.Pos + Vector3.up, ec, 2, 0.6f);
                if (Random.value < 0.25f) Fx.Scorch(p.Pos, 1.2f);
                if (Random.value < 0.5f) Fx.Debris(p.Pos, new Color(0.8f, 0.75f, 0.64f), 2);
                if (pass < passes && !hitThis && u > 0.5f)
                {
                    hitThis = true;
                    Fx.Explosion(B.Pos + Vector3.up * 2, ec, 1.8f);
                    Fx.Pillar(Player.Flat(B.Pos), ec, 2.5f, 9f, 0.5f);
                    Fx.Ring(B.Pos, 6f, ec, 0.3f, 3f);
                    Sfx.Play("stomp", 1f, 1.1f);
                    Sfx.Play("explode", 0.6f, 1.2f);
                    G.Cam.Shake(0.5f);
                    PostFX.I?.Flash(new Color(1f, 0.6f, 0.3f), 0.25f);
                    Fx.HitStop(0.06f);
                    G.Hud.WorldText(B.Pos + Vector3.up * 5f, pass == 0 ? "ぶつかる！" : pass == 1 ? "もう一丁！" : "まだまだぁ！", ec, 1f);
                    G.DamageBoss(150, Game.HitKind.Burst, B.Pos + Vector3.up * 2.2f, pass == 0 ? Elem.Pyro : Elem.None);
                }
                // 念のため：何かあっても2.5秒で必ず終わらせて、動けるようにする
                if (t > 2.5f) { p.LockT = 0; return false; }
                if (pass >= passes && u >= 1)
                {
                    p.LockT = 0;
                    // 最後の全力タックル：火柱の輪・大爆発・スロー
                    Fx.Pillar(Player.Flat(B.Pos), ec, 6f, 22f, 1.4f);
                    Fx.Explosion(B.Pos + Vector3.up * 2, ec, 4f);
                    Fx.Explosion(B.Pos + Vector3.up * 3, Color.white, 1.5f);
                    for (int i = 0; i < 10; i++)
                    {
                        int k = i;
                        Fx.Later(0.05f * k, () =>
                        {
                            float a = k / 10f * Mathf.PI * 2;
                            var pp = Player.Flat(B.Pos) + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 7f;
                            Fx.Pillar(pp, ec, 1.8f, 10f, 1f);
                            Fx.Explosion(pp + Vector3.up, ec, 0.8f);
                        });
                    }
                    Fx.Ring(B.Pos, 16, ec, 0.8f, 3f);
                    Fx.Ring(B.Pos, 10, Color.white, 0.5f, 3f);
                    Fx.Debris(B.Pos + Vector3.up, new Color(0.35f, 0.3f, 0.3f), 60);
                    Fx.Slow(0.2f, 0.9f);
                    Sky.Flash(1f);
                    G.Hud.WorldText(B.Pos + Vector3.up * 6.5f, "ラグビー部なめんな！！", ec, 1.5f);
                    Sfx.Play("explode", 1f, 0.7f);
                    Sfx.Play("boom", 1f, 0.6f);
                    Finale(480, Elem.Pyro);
                    return false;
                }
                return true;
            });
        }

        // やましょうの能力「留年」：生き返ったときに、極太の矢を2本同時に撃ち返す
        public static void RyunenShot(Player p)
        {
            var ec = p.Def.ElemColor;
            G.Hud.WorldText(p.Pos + Vector3.up * 3f, "留年！！", Mat.Gold, 1.3f);
            Sfx.Play("arrow", 1f, 0.6f);
            Sfx.Play("whoosh", 1f, 0.5f);
            G.Cam.FovPunch(-6);
            for (int k = 0; k < 2; k++)
            {
                float off = k == 0 ? -0.08f : 0.08f;
                var start = p.HandPos + Vector3.up * 0.2f + new Vector3(Mathf.Cos(p.Face), 0, -Mathf.Sin(p.Face)) * (k == 0 ? -0.5f : 0.5f);
                Shoot(start, p.Face + off, ec, 3.2f, 30f, hp =>
                {
                    Fx.Explosion(hp, ec, 1.8f);
                    Fx.Ring(B.Pos, 9, ec, 0.5f, 3f);
                    Fx.Sparks(hp, Color.white, 30, 1.8f);
                    Sfx.Play("burstHit", 0.8f, 1.1f);
                    G.Cam.Shake(0.6f);
                    G.DamageBoss(330, Game.HitKind.Burst, hp, Elem.Wind);
                });
            }
            // 矢の後に吹く突風（小さい追加ダメージ）
            for (int i = 0; i < 8; i++)
            {
                int k = i;
                Fx.Later(0.5f + k * 0.08f, () =>
                {
                    if (!B.Alive || G.State != Game.Mode.Battle) return;
                    Fx.SlashLine(B.Pos + Vector3.up * 2.4f, ec, 6f);
                    G.DamageBoss(35, Game.HitKind.Burst, B.Pos + Vector3.up * 2.4f + Random.insideUnitSphere, Elem.None, true);
                });
            }
            Fx.Later(1.3f, () =>
            {
                if (!B.Alive || G.State != Game.Mode.Battle) return;
                G.Hud.WorldText(B.Pos + Vector3.up * 6f, "もう1年！！", Mat.Gold, 1.2f);
                Finale(180, Elem.Wind);
            });
        }

        // やましょう「寝ぼけ乱れ撃ち」：寝ぼけたまま矢をあちこちに連射する（だいたい当たる）
        public static void YamashouBurst(Player p)
        {
            var ec = p.Def.ElemColor;
            G.Hud.WorldText(p.Pos + Vector3.up * 3f, "zzz……", new Color(0.8f, 1f, 0.9f), 1.1f);
            p.LockT = 1.9f;
            for (int i = 0; i < 22; i++)
            {
                int k = i;
                Fx.Later(0.05f + k * 0.075f, () =>
                {
                    if (G.State != Game.Mode.Battle) return;
                    if (B.Alive) p.Face = Mathf.Atan2(B.Pos.x - p.Pos.x, B.Pos.z - p.Pos.z);
                    Sfx.Play("arrow", 0.5f, 1.1f + Random.Range(-0.1f, 0.1f));
                    if (k % 5 == 0) G.Hud.WorldText(p.Pos + Vector3.up * 2.4f, "ねみー", new Color(0.8f, 1f, 0.9f), 0.6f);
                    Shoot(p.HandPos + Vector3.up * 0.1f, p.Face + Random.Range(-0.35f, 0.35f), ec, 1.2f, 40f, hp =>
                    {
                        G.DamageBoss(38, Game.HitKind.Burst, hp, k == 0 || k == 11 ? Elem.Wind : Elem.None, true);
                    });
                });
            }
            Fx.Later(1.8f, () =>
            {
                p.LockT = 0;
                if (!B.Alive || G.State != Game.Mode.Battle) return;
                G.Hud.WorldText(B.Pos + Vector3.up * 6f, "ねみぃぃぃ！！", ec, 1.3f);
                Fx.Ring(B.Pos, 10, ec, 0.6f, 3f);
                Finale(300, Elem.Wind);
            });
        }

        static void Finale(float dmg, Elem e)
        {
            Sfx.Play("burstHit", 1f, 0.85f);
            PostFX.I?.Flash(Color.white, 0.6f);
            PostFX.I?.Radial(0.8f);
            G.Cam.Shake(0.8f);
            G.Cam.FovPunch(8);
            Fx.HitStop(0.12f);
            G.DamageBoss(dmg, Game.HitKind.Burst, B.Pos + Vector3.up * (B.Y + 3.6f), e);
        }
    }
}
