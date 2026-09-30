using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AndoBoss.EditorTools
{
    // Windows 用の exe を作る。メニューの「安東ボス戦 → Windows用のexeを作る」か、
    // コマンドラインの -executeMethod AndoBoss.EditorTools.AndoBossBuild.BuildWindows で使う
    public static class AndoBossBuild
    {
        const string OutDir = "Builds/Windows";
        const string ExeName = "AndoBossBattle.exe";

        [MenuItem("安東ボス戦/Windows用のexeを作る")]
        static void BuildFromMenu()
        {
            var report = Build();
            if (report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(Path.Combine(OutDir, ExeName));
                EditorUtility.DisplayDialog("安東ボス戦", $"exe ができました。\n{Path.GetFullPath(OutDir)}\n\nフォルダごとコピーして使ってください。", "OK");
            }
            else EditorUtility.DisplayDialog("安東ボス戦", "ビルドに失敗しました。Console のエラーを見てください。", "OK");
        }

        // 動作確認用の Linux 版（スクリーンショットを撮るのに使う）
        public static void BuildLinux()
        {
            var report = Build(BuildTarget.StandaloneLinux64, "Builds/Linux/AndoBossBattle.x86_64");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void BuildWindows()
        {
            var report = Build();
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build() => Build(BuildTarget.StandaloneWindows64, Path.Combine(OutDir, ExeName));

        static BuildReport Build(BuildTarget target, string path)
        {
            AndoBossSetup.EnsureScene();
            // 使っていないライブラリのコードを削って、配りやすい大きさにする
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
            PlayerSettings.usePlayerLog = true;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/AndoBoss/Scenes/BossBattle.unity" },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[安東ボス戦] ビルド結果: {report.summary.result}  サイズ: {report.summary.totalSize / 1024 / 1024} MB  エラー: {report.summary.totalErrors}");
            return report;
        }
    }
}
