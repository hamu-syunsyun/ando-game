using UnityEngine;

namespace AndoBoss
{
    // 3人の元素スキル（E）と元素爆発（Q）
    public static class Skills
    {
        static Game G => Game.I;
        static Boss B => Game.I.Boss;

        // ================= 元素スキル =================

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

        // 杉山くん「はんだ付け」：ボスの足元にはんだを流し込み、火柱を立てる
        public static void Solder(Player p)
        {
            var ec = p.Def.ElemColor;
            var target = B.Alive && Player.Flat(B.Pos - p.Pos).magnitude < 14 ? Player.Flat(B.Pos) : Player.Flat(p.Pos + p.Forward * 7);
            Sfx.Play("whoosh", 0.8f, 0.7f);
            Fx.Ring(target, 3.8f, ec, 0.3f);
            Fx.Sparks(p.HandPos, ec, 10, 0.8f);
            Fx.Later(0.3f, () =>
            {
                Fx.Pillar(target, ec, 3.2f, 8f, 1.3f);
                bool first = true;
                for (int i = 0; i < 3; i++)
                {
                    Fx.Later(i * 0.4f, () =>
                    {
                        Sfx.Play("fire", 0.9f, 1 + Random.Range(-0.1f, 0.1f));
                        Fx.Explosion(target + Vector3.up * 0.5f, ec, 0.8f);
                        G.Cam.Shake(0.15f);
                        if (B.Alive && Player.Flat(B.Pos - target).magnitude < 3.8f + Boss.Radius && B.Y < 4)
                        {
                            G.DamageBoss(95, Game.HitKind.Skill, B.Pos + Vector3.up * 2.2f, Elem.Pyro);
                            if (first) { first = false; G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 3); }
                        }
                    });
                }
            });
        }

        // やましょう「液体窒素」：周りを一瞬で凍らせる
        public static void Nitrogen(Player p)
        {
            var ec = p.Def.ElemColor;
            Sfx.Play("ice", 1f);
            Sfx.Play("whoosh", 0.6f, 0.6f);
            Fx.Ring(p.Pos, 6f, ec, 0.35f);
            Fx.Ring(p.Pos, 4f, Color.white, 0.25f);
            Fx.Debris(p.Pos + Vector3.up * 0.5f, new Color(0.9f, 0.97f, 1f, 0.8f), 30);
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2 + Random.value * 0.3f;
                Fx.IceSpike(p.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Random.Range(2f, 5f), Random.Range(1f, 2.2f));
            }
            G.Cam.Shake(0.2f);
            if (B.Alive && Player.Flat(B.Pos - p.Pos).magnitude < 5.8f + Boss.Radius && B.Y < 3)
            {
                G.DamageBoss(230, Game.HitKind.Skill, B.Pos + Vector3.up * 2.4f, Elem.Cryo);
                G.SpawnOrbs(B.Pos + Vector3.up * 2.5f, 3);
                for (int i = 0; i < 4; i++) Fx.IceSpike(B.Pos + Random.insideUnitSphere.Flat() * 2, 2.5f);
            }
        }

        static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0, v.z);

        // ================= 元素爆発 =================

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

        // 杉山くん「ショート回路」：ボスの周りの回路が次々ショートして大爆発
        public static void SugiyamaBurst(Player p)
        {
            var ec = p.Def.ElemColor;
            for (int i = 0; i < 7; i++)
            {
                int k = i;
                Fx.Later(0.05f + k * 0.13f, () =>
                {
                    if (!B.Alive || G.State != Game.Mode.Battle) return;
                    float a = k / 7f * Mathf.PI * 2;
                    var pos = B.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3f;
                    Fx.Explosion(pos + Vector3.up, ec, 1.2f);
                    Sfx.Play("explode", 0.6f, 1.1f + k * 0.03f);
                    G.DamageBoss(130, Game.HitKind.Burst, B.Pos + Vector3.up * 2.5f + Random.insideUnitSphere, k == 0 ? Elem.Pyro : Elem.None);
                    G.Cam.Shake(0.25f);
                });
            }
            Fx.Later(1.2f, () =>
            {
                if (!B.Alive || G.State != Game.Mode.Battle) return;
                Fx.Pillar(Player.Flat(B.Pos), ec, 5f, 16f, 1.2f);
                Fx.Explosion(B.Pos + Vector3.up * 2, ec, 2.5f);
                Fx.Ring(B.Pos, 12, ec, 0.6f, 3f);
                G.Hud.WorldText(B.Pos + Vector3.up * 6f, "ショート！！", ec, 1.4f);
                Sfx.Play("explode", 1f, 0.8f);
                Finale(480, Elem.Pyro);
            });
        }

        // やましょう「絶対零度」：ボスを氷づけにして、砕く
        public static void YamashouBurst(Player p)
        {
            var ec = p.Def.ElemColor;
            if (B.Alive) B.Freeze(2.4f);
            Sfx.Play("freeze", 1f);
            for (int i = 0; i < 16; i++)
            {
                int k = i;
                Fx.Later(0.1f + k * 0.1f, () =>
                {
                    if (!B.Alive || G.State != Game.Mode.Battle) return;
                    float a = Random.value * Mathf.PI * 2;
                    Fx.IceSpike(B.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * Random.Range(1.5f, 3.5f), Random.Range(2f, 4f));
                    Sfx.Play("ice", 0.5f, 1.1f);
                    G.DamageBoss(55, Game.HitKind.Burst, B.Pos + Vector3.up * 2.5f + Random.insideUnitSphere, k == 0 ? Elem.Cryo : Elem.None, true);
                });
            }
            Fx.Later(2.0f, () =>
            {
                if (!B.Alive || G.State != Game.Mode.Battle) return;
                B.Freeze(0);
                Fx.Debris(B.Pos + Vector3.up * 2, new Color(0.85f, 0.95f, 1f), 60);
                Fx.Sparks(B.Pos + Vector3.up * 2, ec, 50, 2f);
                Fx.Ring(B.Pos, 12, ec, 0.6f, 3f);
                G.Hud.WorldText(B.Pos + Vector3.up * 6f, "粉砕！！", ec, 1.4f);
                Sfx.Play("break", 1f);
                Finale(560, Elem.Cryo);
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
