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
            CoffeeGameProjectSetup.SetupMeshySnowKimono();
            EnsureCombatSceneExists();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Could not switch to the Windows x64 build target.");
            }

            string output = GetOutputPath("Windows", "CoffeeGAME.exe");
            Build(output, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        }

        public static void BuildSnowKimonoTrialWindows()
        {
            CoffeeGameProjectSetup.SetupSnowKimono();
            EnsureCombatSceneExists();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Could not switch to the Windows x64 build target.");
            }

            string output = GetOutputPath("Windows-SnowKimono", "CoffeeGAME-SnowKimono.exe");
            Build(output, BuildTarget.StandaloneWindows64, BuildOptions.Development);
        }

        // Evidence-only build: compile the current scene/resources exactly as
        // they are, without running setup or regenerating shared controllers.
        public static void BuildAzureMotionDiagnosticNoSetup()
        {
            BuildDiagnosticNoSetup("Windows-AzureMotionDiagnostic", "CoffeeGAME-AzureMotionDiagnostic.exe");
        }

        public static void BuildGoblinV8NoSetup()
        {
            GoblinAssetSetup.Validate();
            BuildDiagnosticNoSetup("Windows-GoblinV8", "CoffeeGAME-GoblinV8.exe");
        }

        public static void BuildPartyV9NoSetup()
        {
            PartyAudioSetup.Validate();
            if (Resources.Load<GameObject>("Models/Characters/SilverCat/silver-cat-girl") == null ||
                Resources.Load<RuntimeAnimatorController>("Animations/Characters/SilverCat/SilverCatRuntime") == null)
                throw new InvalidOperationException("The silver cat model/controller must be prepared before building the companion player.");
            foreach (string voice in new[] { "magic_02_freeze", "dodge_02_over_here", "sword_01_ya", "sword_02_ha" })
                if (Resources.Load<AudioClip>("Audio/Voices/Heroine/" + voice) == null)
                    throw new InvalidOperationException("Missing heroine voice: " + voice);
            BuildDiagnosticNoSetup("Windows-PartyV9", "CoffeeGAME-PartyV9.exe");
        }

        public static void BuildDefenseV10NoSetup()
        {
            PartyAudioSetup.Validate();
            BuildDiagnosticNoSetup("Windows-DefenseV10", "CoffeeGAME-DefenseV10.exe");
        }

        public static void BuildAcrobaticsV11NoSetup()
        {
            PartyAudioSetup.Validate();
            BuildDiagnosticNoSetup("Windows-AcrobaticsV11", "CoffeeGAME-AcrobaticsV11.exe");
        }

        public static void BuildTargetLockV12NoSetup()
        {
            PartyAudioSetup.Validate();
            BuildDiagnosticNoSetup("Windows-TargetLockV12", "CoffeeGAME-TargetLockV12.exe");
        }

        public static void BuildCombatPolishV13NoSetup()
        {
            PartyAudioSetup.Validate();
            if (Resources.Load<AudioClip>("Audio/Voices/Heroine/special_01") == null)
                throw new InvalidOperationException("Missing Owner heroine finisher voice.");
            BuildDiagnosticNoSetup("Windows-CombatPolishV13", "CoffeeGAME-CombatPolishV13.exe");
        }

        public static void BuildForestV7NoSetup()
        {
            BuildDiagnosticNoSetup("Windows-ForestV7", "CoffeeGAME-ForestV7.exe");
        }

        public static void BuildAzureCleanV3DiagnosticNoSetup()
        {
            BuildDiagnosticNoSetup("Windows-AzureCleanV3", "CoffeeGAME-AzureCleanV3.exe");
        }

        public static void BuildAzureActionV5NoSetup()
        {
            AzureMaidenUpgradedValidation.Validate();
            BuildDiagnosticNoSetup("Windows-AzureActionV5", "CoffeeGAME-AzureActionV5.exe");
        }

        public static void BuildAzureIdleV6NoSetup()
        {
            AzureMaidenUpgradedValidation.ValidateSheathedIdle();
            BuildDiagnosticNoSetup("Windows-AzureIdleV6", "CoffeeGAME-AzureIdleV6.exe");
        }

        private static void BuildDiagnosticNoSetup(string directory, string executable)
        {
            EnsureCombatSceneExists();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            {
                throw new InvalidOperationException("Could not switch to the Windows x64 build target.");
            }

            string output = GetOutputPath(directory, executable);
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
