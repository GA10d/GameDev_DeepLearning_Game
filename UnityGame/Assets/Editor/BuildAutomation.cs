using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LearningFoundry.Editor
{
    public static class BuildAutomation
    {
        const string ScenePath = "Assets/Scenes/Workshop.unity";
        [MenuItem("Learning Foundry/Prepare Project")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "DeepLearningWorkshop";
            PlayerSettings.productName = "Learning Foundry";
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory("Assets/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Workshop Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f,.045f,.05f);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0,0,-10);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("LEARNING_FOUNDRY_PREPARED");
        }
        [MenuItem("Learning Foundry/Build Windows")]
        public static void BuildWindows()
        {
            Prepare();
            var output = Argument("-buildOutput") ?? Path.GetFullPath("Builds/Windows/LearningFoundry.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + report.summary.result);
            var licenses = Path.Combine(Path.GetDirectoryName(output), "Licenses");
            Directory.CreateDirectory(licenses);
            foreach (var source in Directory.GetFiles(Path.Combine(Application.dataPath, "ThirdPartyLicenses"), "*.txt"))
                File.Copy(source, Path.Combine(licenses, Path.GetFileName(source)), true);
            File.WriteAllText(Path.Combine(licenses, "FONT-CREDITS.txt"),
                "Source Han Sans CN — Adobe — SIL OFL 1.1\nhttps://github.com/adobe-fonts/source-han-sans\n\nIBM Plex Mono — IBM — SIL OFL 1.1\nhttps://github.com/google/fonts/tree/main/ofl/ibmplexmono\n\nOriginal font files are bundled without modification. Full licenses are included in this folder.\n");
            Debug.Log("LEARNING_FOUNDRY_BUILD_SUCCEEDED " + report.summary.totalSize);
        }
        static string Argument(string key)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (int i=0;i<arguments.Length-1;i++) if (arguments[i] == key) return arguments[i+1];
            return null;
        }
    }
}
