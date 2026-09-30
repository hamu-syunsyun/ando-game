using System.Collections;
using System.IO;
using UnityEngine;

namespace AndoBoss
{
    // 動作確認用：起動引数に -andoShots <フォルダ> をつけると、画面を順番に自動で進めてスクリーンショットを撮る。
    // ふつうに遊ぶときは何もしない
    public class ShotTour : MonoBehaviour
    {
        string dir;
        int boss, chara;

        public static void StartIfRequested(Game g)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-andoShots")
                {
                    var t = g.gameObject.AddComponent<ShotTour>();
                    Application.runInBackground = true; // 仮想画面ではウィンドウに焦点が当たらないので
                    QualitySettings.vSyncCount = 0;
                    // ソフトウェア描画でも動くように、影とアンチエイリアスを切る（見た目の確認には十分）
                    if (System.Array.IndexOf(args, "-andoLite") >= 0) { QualitySettings.shadows = ShadowQuality.Disable; QualitySettings.antiAliasing = 0; }
                    t.dir = args[i + 1];
                    int bi = System.Array.IndexOf(args, "-andoBoss");
                    if (bi >= 0 && bi + 1 < args.Length) int.TryParse(args[bi + 1], out t.boss);
                    int ci = System.Array.IndexOf(args, "-andoChar");
                    if (ci >= 0 && ci + 1 < args.Length) int.TryParse(args[ci + 1], out t.chara);
                    Directory.CreateDirectory(t.dir);
                    return;
                }
        }

        IEnumerator Start()
        {
            var G = Game.I;
            // 仮想画面では垂直同期で止まることがあるので切る
            QualitySettings.vSyncCount = 0;
            Debug.Log("[ShotTour] start");
            yield return Wait(3f);
            yield return ShotCo("01_title");
            G.GoSettings();
            yield return Wait(0.8f);
            yield return ShotCo("015_settings");
            G.GoTitle();
            yield return Wait(0.5f);
            G.GoSelect();
            if (boss != 0) G.SelectBoss(boss);
            yield return Wait(1.2f);
            yield return ShotCo("02_bossselect");
            G.StartChar = chara;
            G.GoCharSelect();
            yield return Wait(1.2f);
            yield return ShotCo("02_select");
            G.GoIntro();
            yield return Wait(1.6f);
            yield return ShotCo("03_intro");
            G.GoBattle();
            yield return Wait(1.8f);
            yield return ShotCo("04_battle");
            // 必殺技の見た目の確認
            {
                var B = G.Boss;
                var P = G.Player;
                B.DebugAttack(B.IsSuga ? "hakai" : "sansou");
                Fx.TimeMul = 0.2f; // 仮想画面はコマ落ちするので、ゆっくり進めて途中の様子を撮る
                yield return Wait(0.35f);
                yield return ShotCo("041_special");
                yield return Wait(0.9f);
                P.Inv = 5f;
                yield return ShotCo("042_special");
                yield return Wait(1.0f);
                yield return ShotCo("043_special");
                // 横から大きく見る
                G.Cam.Cinematic(B.Pos + new Vector3(18, 12, -14), B.Pos + new Vector3(0, 2, -10), 55, true);
                yield return Wait(0.3f);
                yield return ShotCo("044_special");
                G.Cam.EndCinematic();
                Fx.TimeMul = 1f;
                yield return Wait(3f);
                G.PartyHp = G.Player.MaxHp; G.Counts = 0;
            }
            for (int i = 0; i < 12; i++)
            {
                G.DamageBoss(60, Game.HitKind.Normal, G.Boss.Pos + Vector3.up * 2.5f);
                yield return Wait(0.06f);
            }
            if (Game.BossKind != 0)
            {
                G.AddCount(1);
                yield return Wait(0.2f);
                G.AddCount(1);
            }
            G.Say(G.Boss.Line("hit"));
            G.Hud.Toast("エナドリで回復！　元気100倍");
            G.PartyStam = 60;
            yield return Wait(0.3f);
            yield return ShotCo("05_hits");
            // 構えの確認：横から見る
            {
                var P = G.Player;
                var right = new Vector3(Mathf.Cos(P.Face), 0, -Mathf.Sin(P.Face));
                G.Cam.Cinematic(P.Pos + right * 3.2f + Vector3.up * 1.4f + P.Forward * 0.8f, P.Pos + Vector3.up * 1.1f, 40, true);
                if (P.Def.Weapon == Weapon.Bow) { P.ShootT = 1f; P.ShootUp = false; }
                if (P.Def.Weapon == Weapon.Spear) P.ThrustT = 1f;
                yield return Wait(0.25f);
                yield return ShotCo("055_pose");
                if (P.Def.Weapon == Weapon.Bow) P.ShootUp = true;
                yield return Wait(0.2f);
                yield return ShotCo("056_pose");
                G.Cam.EndCinematic();
            }
            G.Player.Energy = 100;
            G.StartBurst(G.Player);
            yield return Wait(0.5f);
            yield return ShotCo("06_cutin");
            yield return Wait(1.6f);
            yield return ShotCo("065_burst");
            yield return Wait(1.4f);
            G.PerfectDodge();
            yield return Wait(0.3f);
            yield return ShotCo("07_perfect");
            yield return Wait(1.5f);
            if (Game.IsDouble)
            {
                // ダブル：1人目を倒したところ
                G.Boss.Hp = 1;
                G.DamageBoss(100, Game.HitKind.Normal, G.Boss.Pos + Vector3.up * 2.5f);
                yield return Wait(1.2f);
                yield return ShotCo("075_down");
                yield return Wait(1.5f);
            }
            G.Boss.Hp = 1;
            G.DamageBoss(100, Game.HitKind.Normal, G.Boss.Pos + Vector3.up * 2.5f);
            yield return Wait(1.0f);
            yield return ShotCo("08_win");
            yield return Wait(10f);
            yield return ShotCo("09_result");
            yield return Wait(1f);
            Application.Quit();
        }

        static IEnumerator Wait(float s)
        {
            float t = 0;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
            Debug.Log($"[ShotTour] frame {Time.frameCount} t={Time.realtimeSinceStartup:0.0}");
        }

        void Update()
        {
            if (Time.frameCount % 20 == 0) Debug.Log($"[ShotTour] alive frame {Time.frameCount} t={Time.realtimeSinceStartup:0.0}");
        }

        IEnumerator ShotCo(string name)
        {
            var path = Path.Combine(dir, $"b{boss}c{chara}_{name}_{Screen.width}x{Screen.height}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[ShotTour] " + path);
            // 撮影はフレームの最後に行われるので、次の操作は2フレーム待ってから
            yield return null;
            yield return null;
        }
    }
}
