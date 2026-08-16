using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public static class BuildCoffeeGame
    {
        [MenuItem("CoffeeGAME/Build/Windows development build", priority = 20)]
        public static void BuildWindows()
        {
            CoffeeGameProjectSetup.SetupFirstCombatSliceOrThrow();
            EnsureCombatSceneExists();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Could not switch to the Windows x64 build target.");
            }

            string output = GetOutputPath("Windows", "CoffeeGAME.exe");
            Build(output, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        }

        [MenuItem("CoffeeGAME/Build/Android development APK", priority = 21)]
        public static void BuildAndroid()
        {
            ApplyLocalAndroidToolchain();
            CoffeeGameProjectSetup.SetupFirstCombatSliceOrThrow();
            EnsureCombatSceneExists();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                throw new InvalidOperationException("Android Build Support is not installed or the target switch failed.");
            }

            EditorUserBuildSettings.buildAppBundle = false;
            string output = GetOutputPath("Android", "CoffeeGAME-development.apk");
            Build(output, BuildTarget.Android, BuildOptions.Development);
        }

        private static void Build(string outputPath, BuildTarget target, BuildOptions options)
        {
            var buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { CoffeeGameProjectSetup.ScenePath },
                locationPathName = outputPath,
                target = target,
                options = options
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"CoffeeGAME {target} build failed with {report.summary.totalErrors} errors. See the Editor log.");
            }

            Debug.Log($"CoffeeGAME build complete: {outputPath} ({report.summary.totalSize:N0} bytes)");
        }

        private static void EnsureCombatSceneExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CoffeeGameProjectSetup.ScenePath) == null)
            {
                throw new FileNotFoundException(
                    "CoffeeGAME combat scene is missing after project setup.",
                    CoffeeGameProjectSetup.ScenePath);
            }
        }

        private static void ApplyLocalAndroidToolchain()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string tools = Path.Combine(repoRoot, ".tools", "android");
            string sdk = Path.Combine(tools, "SDK");
            string ndk = Path.Combine(tools, "NDK");
            string jdk = Path.Combine(tools, "OpenJDK");

            if (Directory.Exists(sdk))
            {
                EditorPrefs.SetString("AndroidSdkRoot", sdk);
                EditorPrefs.SetBool("SdkUseEmbedded", false);
                UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdk;
            }

            if (Directory.Exists(ndk))
            {
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                EditorPrefs.SetBool("NdkUseEmbedded", false);
                UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = ndk;
            }

            if (File.Exists(Path.Combine(jdk, "bin", "java.exe")))
            {
                EditorPrefs.SetString("JdkPath", jdk);
                EditorPrefs.SetBool("JdkUseEmbedded", false);
                UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = jdk;
            }
        }

        private static string GetOutputPath(string platform, string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(projectRoot, "Builds", platform);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }
    }
}
