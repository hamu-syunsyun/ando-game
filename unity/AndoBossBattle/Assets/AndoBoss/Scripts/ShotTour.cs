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
        int boss;

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
            G.GoSelect();
            if (boss != 0) G.SelectBoss(boss);
            yield return Wait(1.2f);
            yield return ShotCo("02_bossselect");
            G.GoCharSelect();
            yield return Wait(1.2f);
            yield return ShotCo("02_select");
            G.GoIntro();
            yield return Wait(1.6f);
            yield return ShotCo("03_intro");
            G.GoBattle();
            yield return Wait(1.8f);
            yield return ShotCo("04_battle");
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
            G.Player.Energy = 100;
            G.StartBurst(G.Player);
            yield return Wait(0.5f);
            yield return ShotCo("06_cutin");
            yield return Wait(3f);
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
            var path = Path.Combine(dir, $"b{boss}_{name}_{Screen.width}x{Screen.height}.png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[ShotTour] " + path);
            // 撮影はフレームの最後に行われるので、次の操作は2フレーム待ってから
            yield return null;
            yield return null;
        }
    }
}
