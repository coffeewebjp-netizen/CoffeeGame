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

        private static string GetOutputPath(string platform, string fileName)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string directory = Path.Combine(projectRoot, "Builds", platform);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }
    }
}
