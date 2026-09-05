using System;
using System.Collections.Generic;
using CoffeeGame.Bootstrap;
using CoffeeGame.Domain;
using CoffeeGame.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CoffeeGame.Editor
{
    [InitializeOnLoad]
    public static class CoffeeGameProjectSetup
    {
        public const string ScenePath = "Assets/CoffeeGame/Scenes/CombatSandbox.unity";
        private const string TuningPath = "Assets/CoffeeGame/Resources/Data/FirstCombatTuning.asset";
        private const string PipelinePath = "Assets/CoffeeGame/Settings/CoffeeGameURP.asset";
        private const string RuntimeLitMaterialPath = "Assets/CoffeeGame/Resources/Materials/RuntimeLit.mat";
        private const string RuntimeUnlitMaterialPath = "Assets/CoffeeGame/Resources/Materials/RuntimeUnlit.mat";
        private const string HeroModelPath = "Assets/CoffeeGame/Resources/Models/Hero/heroine-v4.fbx";
        private const string SlimeModelPath = "Assets/CoffeeGame/Resources/Models/Slime/slime-v2.fbx";
        private const string HeroControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/HeroRuntime.controller";
        private const string SlimeControllerPath = "Assets/CoffeeGame/Resources/Animations/Slime/SlimeRuntime.controller";
        private const string TrialAnimeGirlModelPath = "Assets/CoffeeGame/Resources/Models/Hero/trial-anime-girl.fbx";
        private const string TrialAnimeGirlControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/TrialAnimeGirlRuntime.controller";
        private const string TrialAnimeGirlAttackModelPath = "Assets/CoffeeGame/Resources/Models/Hero/trial-anime-girl-attack.fbx";
        private const string TrialAnimeGirlAttackControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/TrialAnimeGirlAttackRuntime.controller";
        private const string SnowKimonoModelPath = "Assets/CoffeeGame/Resources/Models/Hero/snow-kimono.fbx";
        private const string SnowKimonoControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/SnowKimonoRuntime.controller";
        private const string MeshySnowKimonoModelPath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono.fbx";
        private const string MeshySnowKimonoControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/MeshySnowKimonoRuntime.controller";
        private const string MeshySnowKimonoBasePath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono-texture-0.png";
        private const string MeshySnowKimonoOrmPath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono-texture-1.png";
        private const string MeshySnowKimonoNormalPath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono-texture-2.png";
        private const string MeshySnowKimonoPackedPath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono-metallic-smoothness.png";
        private const string MeshySnowKimonoMaterialPath = "Assets/CoffeeGame/Resources/Materials/MeshySnowKimonoLit.mat";
        private const string Hd2dArtPath = "Assets/CoffeeGame/Resources/Art/HD2D";
        private const string SessionKey = "CoffeeGame.FirstSetupAttempted";
        private static bool setupRunning;
        private static bool waitingForEditorReady;

        static CoffeeGameProjectSetup()
        {
            ScheduleAutomaticSetup();
        }

        [MenuItem("CoffeeGAME/Trial/Setup anime-girl 3D", priority = 50)]
        public static void SetupTrialAnimeGirl()
        {
            ConfigureModel(TrialAnimeGirlModelPath);
            RefreshImportedClipList(TrialAnimeGirlModelPath);
            EnsureModelAnimatorController(TrialAnimeGirlModelPath, TrialAnimeGirlControllerPath, false);
            AnimatorController trialController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(TrialAnimeGirlControllerPath);
            if (trialController != null && trialController.name != "TrialAnimeGirlRuntime")
            {
                trialController.name = "TrialAnimeGirlRuntime";
                EditorUtility.SetDirty(trialController);
            }

            ConfigureModel(TrialAnimeGirlAttackModelPath);
            RefreshImportedClipList(TrialAnimeGirlAttackModelPath);
            EnsureModelAnimatorController(TrialAnimeGirlAttackModelPath, TrialAnimeGirlAttackControllerPath, false);
            AnimatorController attackController =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(TrialAnimeGirlAttackControllerPath);
            if (attackController != null && attackController.name != "TrialAnimeGirlAttackRuntime")
            {
                attackController.name = "TrialAnimeGirlAttackRuntime";
                EditorUtility.SetDirty(attackController);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CoffeeGAME trial anime-girl 3D controller is ready. Enable it from CoffeeGAME > Trial > Use anime-girl 3D.");
        }

        [MenuItem("CoffeeGAME/Trial/Use anime-girl 3D", priority = 51)]
        public static void EnableTrialAnimeGirl()
        {
            SetupTrialAnimeGirl();
            PlayerPrefs.SetInt(CombatSliceBootstrap.SnowKimonoPrefKey, 0);
            PlayerPrefs.SetInt(CombatSliceBootstrap.TrialAnimeGirlPrefKey, 1);
            CombatSliceBootstrap.SetCharacterSelectionOverride(CharacterSelection.TrialAnimeGirl3D);
            Debug.Log("CoffeeGAME trial anime-girl 3D is enabled for the next Play. HD-2D remains the default when this is off.");
        }

        [MenuItem("CoffeeGAME/Trial/Use anime-girl 3D", true)]
        public static bool EnableTrialAnimeGirlValidate()
        {
            Menu.SetChecked("CoffeeGAME/Trial/Use anime-girl 3D", PlayerPrefs.GetInt(CombatSliceBootstrap.TrialAnimeGirlPrefKey, 0) == 1);
            return true;
        }

        [MenuItem("CoffeeGAME/Trial/Use HD-2D heroine", priority = 52)]
        public static void DisableTrialAnimeGirl()
        {
            PlayerPrefs.SetInt(CombatSliceBootstrap.TrialAnimeGirlPrefKey, 0);
            PlayerPrefs.SetInt(CombatSliceBootstrap.SnowKimonoPrefKey, 0);
            CombatSliceBootstrap.SetCharacterSelectionOverride(CharacterSelection.Hd2d);
            Debug.Log("CoffeeGAME trial anime-girl 3D is off. Play uses the HD-2D heroine.");
        }

        [MenuItem("CoffeeGAME/Trial/Setup snow-kimono 3D", priority = 53)]
        public static void SetupSnowKimono()
        {
            ConfigureModel(SnowKimonoModelPath);
            RefreshImportedClipList(SnowKimonoModelPath);
            EnsureModelAnimatorController(SnowKimonoModelPath, SnowKimonoControllerPath, false);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(SnowKimonoControllerPath);
            if (controller != null && controller.name != "SnowKimonoRuntime")
            {
                controller.name = "SnowKimonoRuntime";
                EditorUtility.SetDirty(controller);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CoffeeGAME snow-kimono 3D controller is ready. Enable it from CoffeeGAME > Trial > Use snow-kimono 3D.");
        }

        [MenuItem("CoffeeGAME/Trial/Use snow-kimono 3D", priority = 54)]
        public static void EnableSnowKimono()
        {
            SetupSnowKimono();
            PlayerPrefs.SetInt(CombatSliceBootstrap.TrialAnimeGirlPrefKey, 0);
            PlayerPrefs.SetInt(CombatSliceBootstrap.SnowKimonoPrefKey, 1);
            CombatSliceBootstrap.SetCharacterSelectionOverride(CharacterSelection.SnowKimono3D);
            Debug.Log("CoffeeGAME snow-kimono 3D is enabled for the next Play. HD-2D remains the default when this is off.");
        }

        [MenuItem("CoffeeGAME/Trial/Use snow-kimono 3D", true)]
        public static bool EnableSnowKimonoValidate()
        {
            Menu.SetChecked("CoffeeGAME/Trial/Use snow-kimono 3D", PlayerPrefs.GetInt(CombatSliceBootstrap.SnowKimonoPrefKey, 0) == 1);
            return true;
        }

        [MenuItem("CoffeeGAME/Trial/Setup Meshy snow-kimono 3D", priority = 55)]
        public static void SetupMeshySnowKimono()
        {
            ConfigureModel(MeshySnowKimonoModelPath);
            RefreshImportedClipList(MeshySnowKimonoModelPath);
            ConfigureMeshyTexture(MeshySnowKimonoBasePath, TextureImporterType.Default, true);
            ConfigureMeshyTexture(MeshySnowKimonoOrmPath, TextureImporterType.Default, false);
            ConfigureMeshyTexture(MeshySnowKimonoNormalPath, TextureImporterType.NormalMap, false);
            ConfigureMeshyTexture(MeshySnowKimonoPackedPath, TextureImporterType.Default, false);
            EnsureMeshySnowKimonoMaterial();
            EnsureModelAnimatorController(MeshySnowKimonoModelPath, MeshySnowKimonoControllerPath, false);
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(MeshySnowKimonoControllerPath);
            if (controller != null && controller.name != "MeshySnowKimonoRuntime")
            {
                controller.name = "MeshySnowKimonoRuntime";
                EditorUtility.SetDirty(controller);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureMeshyTexture(string path, TextureImporterType type, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }
            if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 4096)
            {
                return;
            }
            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureMeshySnowKimonoMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader is unavailable for Meshy Snow Kimono.");
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MeshySnowKimonoMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "MeshySnowKimonoLit" };
                AssetDatabase.CreateAsset(material, MeshySnowKimonoMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshySnowKimonoBasePath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshySnowKimonoNormalPath);
            Texture2D packedMap = AssetDatabase.LoadAssetAtPath<Texture2D>(MeshySnowKimonoPackedPath);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_BumpMap", normalMap);
            material.SetTexture("_MetallicGlossMap", packedMap);
            material.SetTexture("_EmissionMap", baseMap);
            material.SetColor("_EmissionColor", new Color(0.42f, 0.42f, 0.42f, 1f));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Cull", 0f);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        [MenuItem("CoffeeGAME/Setup first combat slice", priority = 1)]
        public static void SetupFirstCombatSlice()
        {
            try
            {
                SetupFirstCombatSliceOrThrow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static void SetupFirstCombatSliceOrThrow()
        {
            if (setupRunning)
            {
                throw new InvalidOperationException("CoffeeGAME setup is already running.");
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                throw new InvalidOperationException("CoffeeGAME setup must wait until Unity finishes compiling and importing assets.");
            }

            setupRunning = true;
            try
            {
                EnsureFolders();
                ConfigureImportedAssets();
                EnsureModelAnimatorControllers();
                EnsureTuningAsset();
                EnsureUrpPipeline();
                EnsureRuntimeMaterials();
                bool inputRestartRecommended = ConfigurePlayerSettings();
                EnsureCombatScene();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                {
                    throw new InvalidOperationException($"Combat scene was not created at {ScenePath}.");
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                SessionState.SetBool(SessionKey, true);
                if (inputRestartRecommended && !Application.isBatchMode)
                {
                    Debug.LogWarning(
                        $"CoffeeGAME assets and {ScenePath} are ready. Unity enabled Both input backends for Input System gamepads and IMGUI mouse fallback; restart the Editor once before pressing Play.");
                    return;
                }
                Debug.Log($"CoffeeGAME setup complete. Open {ScenePath} and press Play.");
            }
            finally
            {
                setupRunning = false;
            }
        }

        private static void TryAutomaticSetup()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            StopWaitingForEditorReady();
            if (IsSetupComplete())
            {
                SessionState.SetBool(SessionKey, true);
                return;
            }

            try
            {
                SetupFirstCombatSliceOrThrow();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ScheduleAutomaticSetup()
        {
            if (waitingForEditorReady)
            {
                return;
            }
            waitingForEditorReady = true;
            EditorApplication.update += TryAutomaticSetup;
        }

        internal static void NotifyHd2dAssetsImported()
        {
            SessionState.SetBool(SessionKey, false);
            ScheduleAutomaticSetup();
        }

        private static void StopWaitingForEditorReady()
        {
            if (!waitingForEditorReady)
            {
                return;
            }
            waitingForEditorReady = false;
            EditorApplication.update -= TryAutomaticSetup;
        }

        private static bool IsSetupComplete()
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath) == null ||
                pipeline == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null ||
                AssetDatabase.LoadAssetAtPath<Material>(RuntimeLitMaterialPath) == null ||
                AssetDatabase.LoadAssetAtPath<Material>(RuntimeUnlitMaterialPath) == null ||
                GraphicsSettings.defaultRenderPipeline != pipeline ||
                !AreImportedModelControllersReady() ||
                !AreHd2dSpriteImportersReady() ||
                !IsCompatibleInputHandlingConfigured())
            {
                return false;
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "CoffeeGame");
            EnsureFolder("Assets/CoffeeGame", "Scenes");
            EnsureFolder("Assets/CoffeeGame", "Settings");
            EnsureFolder("Assets/CoffeeGame/Resources", "Data");
            EnsureFolder("Assets/CoffeeGame/Resources", "Materials");
            EnsureFolder("Assets/CoffeeGame/Resources", "Models");
            EnsureFolder("Assets/CoffeeGame/Resources/Models", "Hero");
            EnsureFolder("Assets/CoffeeGame/Resources/Models", "Slime");
            EnsureFolder("Assets/CoffeeGame/Resources", "Animations");
            EnsureFolder("Assets/CoffeeGame/Resources/Animations", "Hero");
            EnsureFolder("Assets/CoffeeGame/Resources/Animations", "Slime");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string fullPath = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureImportedAssets()
        {
            ConfigureSprite("Assets/CoffeeGame/Resources/Art/Hero/hero-sprite.png", 360f);
            ConfigureSprite("Assets/CoffeeGame/Resources/Art/Slime/slime-sprite.png", 220f);

            string[] sourceSheets = AssetDatabase.FindAssets("t:Texture2D", new[]
            {
                "Assets/CoffeeGame/Resources/Art"
            });
            foreach (string guid in sourceSheets)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("hero-sprite.png", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("slime-sprite.png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                ConfigureSprite(path, GetSpritePixelsPerUnit(path));
            }

            ConfigureAudio("Assets/CoffeeGame/Resources/Audio/Rituals_of_the_Jade_Valley.mp3", true);
            ConfigureAudio("Assets/CoffeeGame/Resources/Audio/katana-slash1.mp3", false);
            ConfigureAudio("Assets/CoffeeGame/Resources/Audio/magic-wind2.mp3", false);

            ConfigureModel(HeroModelPath);
            ConfigureModel(SlimeModelPath);
        }

        private static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var desiredPivot = new Vector2(0.5f, 0f);
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            int maxTextureSize = GetHd2dMaxTextureSize(path);
            bool changed = !IsSpriteImporterReady(importer, pixelsPerUnit, android, maxTextureSize);

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
            importer.SetTextureSettings(textureSettings);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = desiredPivot;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxTextureSize;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            android.overridden = true;
            android.maxTextureSize = maxTextureSize;
            // Every HD-2D frame uses alpha. ASTC keeps that alpha on Android
            // while avoiding the much larger uncompressed RGBA footprint.
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static bool AreHd2dSpriteImportersReady()
        {
            string[] spriteGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { Hd2dArtPath });
            int pngCount = 0;
            for (int i = 0; i < spriteGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(spriteGuids[i]);
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                pngCount++;
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null ||
                    !IsSpriteImporterReady(
                        importer,
                        GetSpritePixelsPerUnit(path),
                        importer.GetPlatformTextureSettings("Android"),
                        GetHd2dMaxTextureSize(path)))
                {
                    return false;
                }
            }

            // An empty folder is not a complete HD-2D setup; this also makes a
            // missing art import visible rather than silently selecting 3D.
            return pngCount > 0;
        }

        private static bool IsSpriteImporterReady(
            TextureImporter importer,
            float pixelsPerUnit,
            TextureImporterPlatformSettings android,
            int maxTextureSize)
        {
            if (importer == null || android == null)
            {
                return false;
            }

            var desiredPivot = new Vector2(0.5f, 0f);
            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            return importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                textureSettings.spriteAlignment == (int)SpriteAlignment.Custom &&
                Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) &&
                (importer.spritePivot - desiredPivot).sqrMagnitude <= 0.000001f &&
                !importer.mipmapEnabled &&
                importer.alphaIsTransparency &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.wrapMode == TextureWrapMode.Clamp &&
                importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == maxTextureSize &&
                importer.sRGBTexture &&
                importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                importer.compressionQuality == 100 &&
                android.overridden &&
                android.maxTextureSize == maxTextureSize &&
                android.format == TextureImporterFormat.ASTC_6x6 &&
                android.compressionQuality == 100 &&
                !android.crunchedCompression;
        }

        private static float GetSpritePixelsPerUnit(string path)
        {
            if (path.Contains("/HD2D/Slime/", StringComparison.OrdinalIgnoreCase))
            {
                return 500f;
            }
            if (path.Contains("/HD2D/Hero/", StringComparison.OrdinalIgnoreCase))
            {
                return 540f;
            }
            return path.Contains("/Slime/", StringComparison.OrdinalIgnoreCase) ? 220f : 360f;
        }

        private static int GetHd2dMaxTextureSize(string path)
        {
            return path.Contains("/HD2D/Hero/Atlases/", StringComparison.OrdinalIgnoreCase)
                ? 4096
                : 2048;
        }

        private static void ConfigureAudio(string path, bool streaming)
        {
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = streaming ? 0.65f : 0.82f;
            settings.preloadAudioData = !streaming;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.SaveAndReimport();
        }

        private static void RefreshImportedClipList(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
            if (defaults == null || defaults.Length == 0)
            {
                return;
            }

            for (int i = 0; i < defaults.Length; i++)
            {
                ModelImporterClipAnimation clip = defaults[i];
                bool shouldLoop = IsLoopingClip(clip.name);
                clip.loopTime = shouldLoop;
                clip.loopPose = shouldLoop;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = defaults;
            importer.SaveAndReimport();
        }

        private static void ConfigureModel(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                !importer.importAnimation ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
                importer.importCameras ||
                importer.importLights ||
                !importer.importBlendShapes ||
                importer.isReadable ||
                importer.animationCompression != ModelImporterAnimationCompression.Optimal;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.isReadable = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            bool clipsChanged = false;
            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    ModelImporterClipAnimation clip = clips[i];
                    bool shouldLoop = IsLoopingClip(clip.name);
                    if (clip.loopTime != shouldLoop ||
                        clip.loopPose != shouldLoop ||
                        !clip.lockRootRotation ||
                        !clip.lockRootHeightY ||
                        !clip.lockRootPositionXZ ||
                        !clip.keepOriginalOrientation ||
                        !clip.keepOriginalPositionY ||
                        !clip.keepOriginalPositionXZ)
                    {
                        clip.loopTime = shouldLoop;
                        clip.loopPose = shouldLoop;
                        clip.lockRootRotation = true;
                        clip.lockRootHeightY = true;
                        clip.lockRootPositionXZ = true;
                        clip.keepOriginalOrientation = true;
                        clip.keepOriginalPositionY = true;
                        clip.keepOriginalPositionXZ = true;
                        clipsChanged = true;
                    }
                }
            }

            if (clipsChanged)
            {
                importer.clipAnimations = clips;
            }
            if (changed || clipsChanged)
            {
                importer.SaveAndReimport();
            }
        }

        private static bool IsLoopingClip(string clipName)
        {
            string normalized = NormalizeAnimationName(clipName);
            return normalized.EndsWith("idle", StringComparison.Ordinal) ||
                normalized.EndsWith("walk", StringComparison.Ordinal) ||
                normalized.EndsWith("run", StringComparison.Ordinal) ||
                normalized.EndsWith("move", StringComparison.Ordinal);
        }

        private static bool AreImportedModelControllersReady()
        {
            return IsImportedModelControllerReady(HeroModelPath, HeroControllerPath) &&
                IsImportedModelControllerReady(SlimeModelPath, SlimeControllerPath);
        }

        private static bool IsImportedModelControllerReady(string modelPath, string controllerPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
            {
                return true;
            }
            return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null;
        }

        private static void EnsureModelAnimatorControllers()
        {
            EnsureModelAnimatorController(HeroModelPath, HeroControllerPath, false);
            EnsureModelAnimatorController(SlimeModelPath, SlimeControllerPath, true);
        }

        private static void EnsureModelAnimatorController(string modelPath, string controllerPath, bool slimeModel)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) == null)
            {
                return;
            }

            List<AnimationClip> clips = LoadModelAnimationClips(modelPath);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"The model at {modelPath} has no imported animation clips; no runtime controller was generated.");
                return;
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }
            RebuildAnimatorController(controller, clips, slimeModel, modelPath);
        }

        private static List<AnimationClip> LoadModelAnimationClips(string modelPath)
        {
            var clips = new List<AnimationClip>();
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                clips.Add(clip);
            }
            return clips;
        }

        private static void RebuildAnimatorController(
            AnimatorController controller,
            List<AnimationClip> clips,
            bool slimeModel,
            string modelPath)
        {
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            ChildAnimatorState[] oldStates = stateMachine.states;
            for (int i = 0; i < oldStates.Length; i++)
            {
                stateMachine.RemoveState(oldStates[i].state);
            }
            ChildAnimatorStateMachine[] oldStateMachines = stateMachine.stateMachines;
            for (int i = 0; i < oldStateMachines.Length; i++)
            {
                stateMachine.RemoveStateMachine(oldStateMachines[i].stateMachine);
            }

            AnimationClip idleClip = FindBestClip(clips, new[] { "Idle" });
            AnimationClip fallbackClip = idleClip != null ? idleClip : clips[0];
            var usedStateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missingRequiredActions = new List<string>();

            Array actions = Enum.GetValues(typeof(CharacterAction));
            foreach (CharacterAction action in actions)
            {
                AnimationClip clip = FindBestClip(clips, GetClipAliases(action, slimeModel));
                if (clip == null)
                {
                    clip = fallbackClip;
                    if (IsRequiredRuntimeAction(action, slimeModel))
                    {
                        missingRequiredActions.Add(action.ToString());
                    }
                }

                string stateName = ModelCharacterVisual.GetDefaultStateName(action);
                AnimatorState state = stateMachine.AddState(stateName);
                state.motion = clip;
                state.speed = 1f;
                state.writeDefaultValues = true;
                usedStateNames.Add(stateName);
                if (action == CharacterAction.Idle)
                {
                    stateMachine.defaultState = state;
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                string clipStateName = GetAnimationLeafName(clips[i].name);
                if (string.IsNullOrWhiteSpace(clipStateName) || usedStateNames.Contains(clipStateName))
                {
                    continue;
                }

                AnimatorState state = stateMachine.AddState(clipStateName);
                state.motion = clips[i];
                state.speed = 1f;
                state.writeDefaultValues = true;
                usedStateNames.Add(clipStateName);
            }

            controller.name = slimeModel ? "SlimeRuntime" : "HeroRuntime";
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);

            if (missingRequiredActions.Count > 0)
            {
                Debug.LogWarning(
                    $"{modelPath} is missing clips for runtime actions: {string.Join(", ", missingRequiredActions)}. " +
                    $"Those states temporarily use {fallbackClip.name}.");
            }
        }

        private static bool IsRequiredRuntimeAction(CharacterAction action, bool slimeModel)
        {
            if (!slimeModel)
            {
                return action != CharacterAction.AttackWindup &&
                    action != CharacterAction.Attack &&
                    action != CharacterAction.Dodge;
            }

            return action == CharacterAction.Idle ||
                action == CharacterAction.Walk ||
                action == CharacterAction.Run ||
                action == CharacterAction.AttackWindup ||
                action == CharacterAction.Attack ||
                action == CharacterAction.Hurt ||
                action == CharacterAction.Defeated;
        }

        private static string[] GetClipAliases(CharacterAction action, bool slimeModel)
        {
            if (slimeModel)
            {
                switch (action)
                {
                    case CharacterAction.Idle:
                        return new[] { "Idle" };
                    case CharacterAction.Walk:
                    case CharacterAction.Run:
                        return new[] { "Move", "Walk", "Run" };
                    case CharacterAction.AttackWindup:
                        return new[] { "Windup" };
                    case CharacterAction.Attack:
                        return new[] { "Attack" };
                    case CharacterAction.Hurt:
                        return new[] { "Hurt" };
                    case CharacterAction.Defeated:
                        return new[] { "Defeated", "Death" };
                    default:
                        return new[] { action.ToString() };
                }
            }

            switch (action)
            {
                case CharacterAction.Idle:
                    return new[] { "Idle", "Stand" };
                case CharacterAction.Walk:
                    return new[] { "Walk", "Move" };
                case CharacterAction.Run:
                    return new[] { "Run", "Sprint" };
                case CharacterAction.Jump:
                    return new[] { "Jump", "Leap" };
                case CharacterAction.Dodge:
                    return new[] { "Dodge", "360_Power_Spin_Jump", "Power_Spin_Jump", "Jump", "Leap" };
                case CharacterAction.Sword:
                    return new[] { "Sword", "SwordAttack", "Attack", "Slash" };
                case CharacterAction.AirSlash:
                    return new[] { "AirSlash", "AerialSlash", "AirAttack" };
                case CharacterAction.Plunge:
                    return new[] { "Plunge", "Dive", "Slam" };
                case CharacterAction.SpinCharge:
                    return new[] { "SpinCharge", "SpecialCharge", "SpinWindup" };
                case CharacterAction.SpinRelease:
                    return new[] { "SpinRelease", "SpinAttack", "Whirlwind" };
                case CharacterAction.MagicCharge:
                    return new[] { "MagicCharge", "SpellCharge", "Cast" };
                case CharacterAction.Hurt:
                    return new[] { "Hurt", "HitReact", "Damage" };
                case CharacterAction.Defeated:
                    return new[] { "Defeated", "Death", "Die" };
                default:
                    return new[] { action.ToString() };
            }
        }

        private static AnimationClip FindBestClip(List<AnimationClip> clips, string[] aliases)
        {
            AnimationClip bestClip = null;
            int bestScore = -1;
            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                string normalizedClipName = NormalizeAnimationName(clips[clipIndex].name);
                string normalizedLeafName = NormalizeAnimationName(GetAnimationLeafName(clips[clipIndex].name));
                for (int aliasIndex = 0; aliasIndex < aliases.Length; aliasIndex++)
                {
                    string normalizedAlias = NormalizeAnimationName(aliases[aliasIndex]);
                    int score = -1;
                    if (string.Equals(normalizedLeafName, normalizedAlias, StringComparison.Ordinal))
                    {
                        score = 1200 + normalizedAlias.Length;
                    }
                    else if (string.Equals(normalizedClipName, normalizedAlias, StringComparison.Ordinal))
                    {
                        score = 1000 + normalizedAlias.Length;
                    }
                    else if (normalizedClipName.EndsWith(normalizedAlias, StringComparison.Ordinal))
                    {
                        score = 800 + normalizedAlias.Length;
                    }
                    else if (normalizedClipName.Contains(normalizedAlias))
                    {
                        score = 500 + normalizedAlias.Length;
                    }

                    if (score >= 0)
                    {
                        score += (aliases.Length - aliasIndex) * 20;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestClip = clips[clipIndex];
                    }
                }
            }
            return bestClip;
        }

        private static string NormalizeAnimationName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var characters = new char[value.Length];
            int length = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character))
                {
                    characters[length++] = char.ToLowerInvariant(character);
                }
            }
            return new string(characters, 0, length);
        }

        private static string GetAnimationLeafName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int separator = Math.Max(value.LastIndexOf('|'), Math.Max(value.LastIndexOf('@'), value.LastIndexOf('/')));
            return separator >= 0 && separator < value.Length - 1
                ? value.Substring(separator + 1).Trim()
                : value.Trim();
        }

        private static void EnsureTuningAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<CombatTuning>(TuningPath) != null)
            {
                return;
            }

            CombatTuning tuning = CombatTuning.CreateDefault();
            AssetDatabase.CreateAsset(tuning, TuningPath);
        }

        private static void EnsureUrpPipeline()
        {
            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                pipeline.name = "CoffeeGame URP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                pipeline.LoadBuiltinRendererData(RendererType.UniversalRenderer);
                pipeline.renderScale = 1f;
                pipeline.shadowDistance = 25f;
                EditorUtility.SetDirty(pipeline);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void EnsureRuntimeMaterials()
        {
            EnsureMaterial(RuntimeLitMaterialPath, "CoffeeGAME Runtime Lit", "Universal Render Pipeline/Lit");
            EnsureMaterial(RuntimeUnlitMaterialPath, "CoffeeGAME Runtime Unlit", "Universal Render Pipeline/Unlit");
        }

        private static void EnsureMaterial(string path, string materialName, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
            {
                return;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException($"Required shader is unavailable: {shaderName}");
            }

            var material = new Material(shader) { name = materialName };
            AssetDatabase.CreateAsset(material, path);
        }

        private static bool ConfigurePlayerSettings()
        {
            PlayerSettings.companyName = "Coffee Tools";
            PlayerSettings.productName = "CoffeeGAME";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "jp.coffeetools.coffeegame");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "jp.coffeetools.coffeegame");

            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length == 0)
            {
                return false;
            }

            var serializedSettings = new SerializedObject(settingsAssets[0]);
            SerializedProperty activeInputHandler = serializedSettings.FindProperty("activeInputHandler");
            // Runtime gamepad input is owned by the Input System. The temporary
            // IMGUI diagnostics/settings panel still needs legacy mouse events,
            // so keep Both enabled until this HUD is replaced by uGUI/UI Toolkit.
            if (activeInputHandler != null && activeInputHandler.intValue != 2)
            {
                activeInputHandler.intValue = 2;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                return true;
            }
            return false;
        }

        private static bool IsCompatibleInputHandlingConfigured()
        {
            UnityEngine.Object[] settingsAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length == 0)
            {
                return false;
            }

            var serializedSettings = new SerializedObject(settingsAssets[0]);
            SerializedProperty activeInputHandler = serializedSettings.FindProperty("activeInputHandler");
            return activeInputHandler != null && activeInputHandler.intValue == 2;
        }

        private static void EnsureCombatScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Scene previous = SceneManager.GetActiveScene();
                bool canRestorePrevious = previous.IsValid() && previous.isLoaded && !string.IsNullOrEmpty(previous.path);
                if (!canRestorePrevious && previous.IsValid() && previous.isDirty)
                {
                    throw new InvalidOperationException(
                        "Combat scene creation cannot replace the current untitled scene because it has unsaved changes. Save it, then run CoffeeGAME > Setup first combat slice again.");
                }

                Scene scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    canRestorePrevious ? NewSceneMode.Additive : NewSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("CoffeeGAME Combat Slice");
                root.AddComponent<CombatSliceBootstrap>();
                EditorSceneManager.SaveScene(scene, ScenePath);

                if (canRestorePrevious)
                {
                    SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existingIndex = scenes.FindIndex(scene => string.Equals(scene.path, ScenePath, StringComparison.OrdinalIgnoreCase));
            var combatScene = new EditorBuildSettingsScene(ScenePath, true);
            if (existingIndex >= 0)
            {
                scenes[existingIndex] = combatScene;
            }
            else
            {
                scenes.Add(combatScene);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    internal sealed class CoffeeGameHd2dAssetPostprocessor : AssetPostprocessor
    {
        private const string Hd2dPrefix = "Assets/CoffeeGame/Resources/Art/HD2D/";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsHd2dPng(importedAssets) || ContainsHd2dPng(movedAssets))
            {
                CoffeeGameProjectSetup.NotifyHd2dAssetsImported();
            }
        }

        private static bool ContainsHd2dPng(string[] assetPaths)
        {
            if (assetPaths == null)
            {
                return false;
            }

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                if (path.StartsWith(Hd2dPrefix, StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
