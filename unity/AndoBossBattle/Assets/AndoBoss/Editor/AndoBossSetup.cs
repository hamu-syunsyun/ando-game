using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AndoBoss.EditorTools
{
    // プロジェクトを初めて開いたときに、ボス戦シーンを作ってビルド設定に登録する。
    // シーンの中身はスクリプト（Game）が実行時にすべて組み立てるので、置くのは空のオブジェクト1つだけ
    [InitializeOnLoad]
    public static class AndoBossSetup
    {
        const string ScenePath = "Assets/AndoBoss/Scenes/BossBattle.unity";

        static AndoBossSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (!File.Exists(ScenePath)) CreateScene(true);
                else EnsureBuildSettings();
            };
        }

        [MenuItem("安東ボス戦/シーンを作り直して開く")]
        static void Recreate() => CreateScene(true);

        [MenuItem("安東ボス戦/ベストスコアを消す")]
        static void ClearBest()
        {
            if (EditorUtility.DisplayDialog("ベストスコアを消す", "このPCに保存されているベストスコアを消します。よろしいですか？", "消す", "やめる"))
            {
                PlayerPrefs.DeleteKey("ando_boss_best");
                PlayerPrefs.Save();
            }
        }

        // シーンが無ければ作る（ビルド前に呼ぶ）
        public static void EnsureScene()
        {
            if (!File.Exists(ScenePath)) CreateScene(false);
            EnsureBuildSettings();
            ApplyPlayerSettings();
        }

        static void CreateScene(bool open)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("AndoBossGame");
            go.AddComponent<Game>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            EnsureBuildSettings();
            ApplyPlayerSettings();
            if (open) EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[安東ボス戦] シーンを作成しました: " + ScenePath + "　▶ を押すと遊べます");
        }

        static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var s in scenes) if (s.path == ScenePath) return;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            list.AddRange(scenes);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "AndoGame";
            PlayerSettings.productName = "安東先生から単位をもぎとれ ボス戦";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = false;
        }
    }
}
