using System;
using System.Collections;
using System.IO;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Integration;
using CoffeeGame.Persistence;
using CoffeeGame.Presentation;
using CoffeeGame.Run;
using CoffeeGame.UI;
using CoffeeGame.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Bootstrap
{
    public enum CharacterVisualBackend
    {
        Hd2d = 0,
        Model3D = 1,
        StaticSprite = 2
    }

    public enum CharacterSelection
    {
        Hd2d = 0,
        TrialAnimeGirl3D = 1,
        SnowKimono3D = 2,
        MeshySnowKimono3D = 3,
        AzureMaidenUpgraded3D = 4
    }

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class CombatSliceBootstrap : MonoBehaviour
    {
        private const string HeroModelResource = "Models/Hero/heroine-v4";
        private const string HeroControllerResource = "Animations/Hero/HeroRuntime";
        private const string HeroHd2dManifestResource = "Art/HD2D/hero-hd2d";
        private const string TrialHeroModelResource = "Models/Hero/trial-anime-girl";
        private const string TrialHeroControllerResource = "Animations/Hero/TrialAnimeGirlRuntime";
        private const string TrialHeroAttackModelResource = "Models/Hero/trial-anime-girl-attack";
        private const string TrialHeroAttackControllerResource = "Animations/Hero/TrialAnimeGirlAttackRuntime";
        public const string TrialAnimeGirlPrefKey = "CoffeeGAME.TrialAnimeGirl3D";
        private const string SnowKimonoModelResource = "Models/Hero/snow-kimono";
        private const string SnowKimonoControllerResource = "Animations/Hero/SnowKimonoRuntime";
        private const string MeshySnowKimonoModelResource = "Models/Hero/MeshySnowKimono/meshy-snow-kimono";
        private const string MeshySnowKimonoControllerResource = "Animations/Hero/MeshySnowKimonoRuntime";
        private const string AzureMaidenUpgradedModelResource = "Models/Hero/AzureMaidenUpgraded/azure-maiden-upgraded";
        private const string AzureMaidenUpgradedControllerResource = "Animations/Hero/AzureMaidenUpgradedRuntime";
        public const string SnowKimonoPrefKey = "CoffeeGAME.SnowKimono3D";
        public const string PreviousCharacterSelectionPrefKey = "CoffeeGAME.PreviousCharacterSelection.v1";
        public const string CharacterSelectionDefaultAppliedPrefKey = "CoffeeGAME.CharacterSelectionDefaultApplied.v1";
        public const string CharacterSelectionOverridePrefKey = "CoffeeGAME.CharacterSelectionOverride.v1";
        private const string RestorePreviousCharacterArg = "-restorePreviousCharacter";
        private const string SetSnowKimonoDefaultArg = "-useSnowKimonoDefault";
        private const string SetMeshySnowKimonoDefaultArg = "-useMeshySnowKimonoDefault";
        private const string SetAzureMaidenUpgradedDefaultArg = "-useAzureMaidenUpgradedDefault";
        private const string CaptureSceneArg = "-captureScene";
        private const string CaptureMeshyMotionArg = "-captureMeshyMotion";
        private const string SlimeModelResource = "Models/Slime/slime-v2";
        private const string SlimeControllerResource = "Animations/Slime/SlimeRuntime";
        private const string SlimeHd2dManifestResource = "Art/HD2D/slime-hd2d";

        private static readonly Vector3[] EnemySpawnPoints =
        {
            new Vector3(1.7f, 0.05f, 0.1f),
            new Vector3(1.25f, 0.05f, 1.45f),
            new Vector3(0.35f, 0.05f, -1.55f),
            new Vector3(2.25f, 0.05f, -1.15f),
            new Vector3(0.95f, 0.05f, 0.75f)
        };
        private static readonly ILearningBridge UnavailableLearningBridge = new NullLearningBridge();

        [SerializeField] private CharacterVisualBackend visualBackend = CharacterVisualBackend.Hd2d;

        private CombatTuning tuning;
        private Transform runtimeRoot;
        private Camera sceneCamera;
        private ForestArenaVisuals forestVisuals;
        private Health playerHealth;
        private int enemySpawnIndex;
        private PlayerProgression sessionProgression;
        private PlayerProfileStore profileStore;
        private CombatRunController runController;
        private CoffeeLearningConnectionPresenter coffeeLearningConnection;

        public ILearningBridge LearningBridge => coffeeLearningConnection?.LearningBridge ?? UnavailableLearningBridge;

        public static bool ShouldUseTrialAnimeGirl()
        {
            return GetCharacterSelection() == CharacterSelection.TrialAnimeGirl3D;
        }

        public static bool ShouldUseSnowKimono()
        {
            return GetCharacterSelection() == CharacterSelection.SnowKimono3D;
        }

        public static bool ShouldUseMeshySnowKimono()
        {
            return GetCharacterSelection() == CharacterSelection.MeshySnowKimono3D;
        }

        public static bool ShouldUseAzureMaidenUpgraded()
        {
            return GetCharacterSelection() == CharacterSelection.AzureMaidenUpgraded3D;
        }

        public static CharacterSelection GetCharacterSelection()
        {
            if (HasCommandLineFlag(RestorePreviousCharacterArg))
            {
                CharacterSelection previous = GetRememberedCharacterSelection();
                SetCharacterSelectionOverride(previous);
                return previous;
            }

            if (HasCommandLineFlag(SetSnowKimonoDefaultArg))
            {
                SetCharacterSelectionOverride(CharacterSelection.SnowKimono3D);
                return CharacterSelection.SnowKimono3D;
            }

            if (HasCommandLineFlag(SetMeshySnowKimonoDefaultArg))
            {
                SetCharacterSelectionOverride(CharacterSelection.MeshySnowKimono3D);
                return CharacterSelection.MeshySnowKimono3D;
            }

            if (HasCommandLineFlag(SetAzureMaidenUpgradedDefaultArg))
            {
                SetCharacterSelectionOverride(CharacterSelection.AzureMaidenUpgraded3D);
                return CharacterSelection.AzureMaidenUpgraded3D;
            }

            if (HasCommandLineFlag("-azureMaidenUpgraded3D"))
            {
                return CharacterSelection.AzureMaidenUpgraded3D;
            }

            if (HasCommandLineFlag("-meshySnowKimono3D"))
            {
                return CharacterSelection.MeshySnowKimono3D;
            }

            if (HasCommandLineFlag("-snowKimono3D"))
            {
                return CharacterSelection.SnowKimono3D;
            }

            if (HasCommandLineFlag("-trialAnimeGirl3D"))
            {
                return CharacterSelection.TrialAnimeGirl3D;
            }

            if (PlayerPrefs.GetInt(CharacterSelectionDefaultAppliedPrefKey, 0) == 1)
            {
                if (PlayerPrefs.HasKey(CharacterSelectionOverridePrefKey))
                {
                    int stored = PlayerPrefs.GetInt(
                        CharacterSelectionOverridePrefKey,
                        (int)CharacterSelection.MeshySnowKimono3D);
                    if (Enum.IsDefined(typeof(CharacterSelection), stored))
                    {
                        return (CharacterSelection)stored;
                    }
                }

                return CharacterSelection.MeshySnowKimono3D;
            }

            // Keep the accepted Meshy Snow Kimono as the ordinary default
            // while the upgraded Azure body remains an explicit trial.
            RememberPreviousCharacterSelection(GetLegacyCharacterSelection());
            SetCharacterSelectionOverride(CharacterSelection.MeshySnowKimono3D);
            return CharacterSelection.MeshySnowKimono3D;
        }

        public static void SetCharacterSelectionOverride(CharacterSelection selection)
        {
            if (!Enum.IsDefined(typeof(CharacterSelection), selection))
            {
                throw new ArgumentOutOfRangeException(nameof(selection), selection, "Unknown CoffeeGAME character selection.");
            }

            PlayerPrefs.SetInt(CharacterSelectionOverridePrefKey, (int)selection);
            PlayerPrefs.SetInt(CharacterSelectionDefaultAppliedPrefKey, 1);
            PlayerPrefs.Save();
            Debug.Log($"CoffeeGAME heroine selection override saved: {selection}.");
        }

        private static CharacterSelection GetLegacyCharacterSelection()
        {
            if (PlayerPrefs.GetInt(SnowKimonoPrefKey, 0) == 1)
            {
                return CharacterSelection.SnowKimono3D;
            }

            if (PlayerPrefs.GetInt(TrialAnimeGirlPrefKey, 0) == 1)
            {
                return CharacterSelection.TrialAnimeGirl3D;
            }

            return CharacterSelection.Hd2d;
        }

        private static CharacterSelection GetRememberedCharacterSelection()
        {
            if (PlayerPrefs.HasKey(PreviousCharacterSelectionPrefKey))
            {
                int stored = PlayerPrefs.GetInt(PreviousCharacterSelectionPrefKey, (int)CharacterSelection.Hd2d);
                if (Enum.IsDefined(typeof(CharacterSelection), stored))
                {
                    return (CharacterSelection)stored;
                }
            }

            // A build that has not yet recorded the temporary default still
            // honors the old selector preferences during an explicit restore.
            return GetLegacyCharacterSelection();
        }

        private static void RememberPreviousCharacterSelection(CharacterSelection previous)
        {
            if (PlayerPrefs.HasKey(PreviousCharacterSelectionPrefKey))
            {
                return;
            }

            // Before this temporary default, an unset selector resolved to the
            // HD-2D heroine. Existing explicit editor preferences are captured
            // separately so anime-girl trials are restored correctly too.
            PlayerPrefs.SetInt(PreviousCharacterSelectionPrefKey, (int)previous);
            PlayerPrefs.Save();
            Debug.Log($"CoffeeGAME previous heroine selection recorded: {previous}.");
        }

        private static bool HasCommandLineFlag(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            if (FindObjectsByType<CombatSliceBootstrap>(FindObjectsInactive.Exclude).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            BuildCombatSlice();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (TryGetCommandLineValue("-captureAcrobatics", out string acrobaticsCapturePath))
            {
                AcrobaticsEvidenceCapture.Begin(gameObject, runController, acrobaticsCapturePath);
            }
            else if (TryGetCommandLineValue("-captureDefense", out string defenseCapturePath))
            {
                DefenseEvidenceCapture.Begin(gameObject, runController, defenseCapturePath);
            }
            else if (TryGetCommandLineValue("-captureParty", out string partyCapturePath))
            {
                PartyEvidenceCapture.Begin(gameObject, runController, partyCapturePath);
            }
            else if (TryGetCommandLineValue("-captureGoblin", out string goblinCapturePath))
            {
                GoblinEvidenceCapture.Begin(gameObject, sceneCamera, runtimeRoot.Find("Player"), runController, goblinCapturePath);
            }
            else if (TryGetCommandLineValue("-captureForest", out string forestCapturePath))
            {
                ForestEvidenceCapture.Begin(gameObject, sceneCamera, runtimeRoot.Find("Player"), forestCapturePath);
            }
            else if (TryGetCommandLineValue(CaptureMeshyMotionArg, out string motionCapturePath))
            {
                Transform playerRoot = runtimeRoot.Find("Player");
                ModelCharacterVisual modelVisual = playerRoot != null
                    ? playerRoot.GetComponentInChildren<ModelCharacterVisual>(true)
                    : null;
                if (modelVisual == null)
                {
                    Debug.LogError("CoffeeGAME Meshy motion capture requires ModelCharacterVisual.");
                    Application.Quit(2);
                }
                else
                {
                    PlayerMotor3D motor = playerRoot.GetComponent<PlayerMotor3D>();
                    PlayerCombatController combat = playerRoot.GetComponent<PlayerCombatController>();
                    if (motor != null) motor.enabled = false;
                    if (combat != null) combat.enabled = false;
                    MeshyMotionEvidenceCapture.Begin(gameObject, sceneCamera, modelVisual, motionCapturePath);
                }
            }
            else
            {
                string captureKey = TryGetCommandLineValue(CaptureSceneArg, out string capturePath)
                    ? CaptureSceneArg
                    : "-captureSnowKimono";
                if (captureKey == "-captureSnowKimono" && !TryGetCommandLineValue(captureKey, out capturePath))
                {
                    capturePath = string.Empty;
                }
                if (!string.IsNullOrWhiteSpace(capturePath))
                {
                    Application.runInBackground = true;
                    StartCoroutine(CaptureRuntimeScene(capturePath));
                }
            }
#endif
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                if (runController == null || runController.Mode == CombatRunMode.Ready || runController.Mode == CombatRunMode.GameOver) TryApplyCloudProfile();
            }
        }

        private void OnApplicationQuit()
        {
            coffeeLearningConnection?.CancelPendingOrActiveAction();
            runController?.Party?.PrepareForExit();
            SavePlayerProfile();
        }

        private void OnDestroy()
        {
            if (sessionProgression != null)
            {
                sessionProgression.Changed -= SavePlayerProfile;
            }

            coffeeLearningConnection?.Dispose();
            coffeeLearningConnection = null;
        }

        private void BuildCombatSlice()
        {
            tuning = Resources.Load<CombatTuning>("Data/FirstCombatTuning");
            if (tuning == null || !tuning.IsValid)
            {
                tuning = CombatTuning.CreateDefault();
            }
            tuning.ValidateOrThrow();

            runtimeRoot = new GameObject("Runtime World").transform;
            runtimeRoot.SetParent(transform, false);

            sceneCamera = CreateCamera();
            CreateLighting();
            CreateGrasslandArena();
            Hd2dScenePresentation.Create(runtimeRoot, sceneCamera);

            GameInputReader input = gameObject.AddComponent<GameInputReader>();
            gameObject.AddComponent<CoffeeGameDeepLinkListener>();
            coffeeLearningConnection = CoffeeLearningConnectionComposition.CreateProduction();
            AudioDirector audioDirector = gameObject.AddComponent<AudioDirector>();
            audioDirector.Initialize();

            PlayerParts player = CreatePlayer(input, audioDirector);
            forestVisuals?.SetFocus(player.Root.transform);
            playerHealth = player.Health;
            EnsurePlayerProfileLoaded();

            runController = gameObject.AddComponent<CombatRunController>();
            runController.Initialize(
                tuning,
                sessionProgression,
                input,
                audioDirector,
                player.Health,
                player.Resources,
                player.Motor,
                player.Combat,
                claimId => CreateEnemy(claimId, player.Root.transform, player.Health),
                () => enemySpawnIndex = 0);

            CombatSliceHud hud = gameObject.AddComponent<CombatSliceHud>();
            hud.Initialize(
                runController,
                input,
                SavePlayerProfileManually,
                coffeeLearningConnection,
                ExportPlayerProfile,
                ImportPlayerProfile,
                UseGoogleDriveSave,
                UseFolderSave,
                UseLocalSave);
            if (!HasCommandLineFlag("-captureParty") && !HasCommandLineFlag("-captureDefense") && !HasCommandLineFlag("-captureAcrobatics")) _ = coffeeLearningConnection.RefreshAccountIdentityAsync();

            FixedCameraRig cameraRig = sceneCamera.gameObject.AddComponent<FixedCameraRig>();
            cameraRig.Initialize(player.Root.transform);
            cameraRig.SetBounds(
                StageLayout.CameraMinX,
                StageLayout.CameraMaxX,
                StageLayout.CameraMinZ,
                StageLayout.CameraMaxZ);
            CameraOrbitInputDriver orbitInput = sceneCamera.gameObject.AddComponent<CameraOrbitInputDriver>();
            orbitInput.Initialize(cameraRig, input);

            gameObject.AddComponent<TimeStopController>().InitializeWorldVisual(sceneCamera);
            PartyActor heroActor = player.Root.AddComponent<PartyActor>();
            heroActor.Initialize(PartyMemberIds.Hero);
            var party = gameObject.AddComponent<PartyRuntime>();
            party.Initialize(runController, tuning, input, heroActor, () => CreateCat(input, audioDirector),
                SavePlayerProfile, actor => { cameraRig.Follow(actor); forestVisuals?.SetFocus(actor); });
            if (runController.Mode == CombatRunMode.Playing) party.TryStartRun();
        }

        private void EnsurePlayerProfileLoaded()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (HasCommandLineFlag("-captureParty") || HasCommandLineFlag("-captureDefense") || HasCommandLineFlag("-captureAcrobatics"))
            {
                sessionProgression = new PlayerProgression(1, 0, 0, 0,
                    previouslyRecruitedRivalIds: new[] { RivalCharacterIds.WeaknessChallenger });
                Debug.Log("CoffeeGAME party evidence uses in-memory progression; profile/cloud writes disabled.");
                return;
            }
            if (HasCommandLineFlag("-captureGoblin"))
            {
                sessionProgression = new PlayerProgression();
                Debug.Log("CoffeeGAME goblin evidence uses in-memory progression; profile/cloud writes disabled.");
                return;
            }
#endif
            if (sessionProgression != null)
            {
                return;
            }

            profileStore = new PlayerProfileStore(CloudSaveSettings.ResolveProfilePath());
            sessionProgression = profileStore.LoadOrCreate(out string message);
            sessionProgression.Changed += SavePlayerProfile;
            TryApplyCloudProfile();
            Debug.Log($"CoffeeGAME profile: {message}");
        }

        private void SavePlayerProfile()
        {
            if (!TrySavePlayerProfile(out string message))
            {
                Debug.LogWarning($"CoffeeGAME profile: {message}");
            }
        }

        private string SavePlayerProfileManually()
        {
            bool saved = TrySavePlayerProfile(out string message);
            if (saved)
            {
                Debug.Log($"CoffeeGAME profile: {message}");
            }
            else
            {
                Debug.LogWarning($"CoffeeGAME profile: {message}");
            }
            return message;
        }

        private bool TrySavePlayerProfile(out string message)
        {
            runController?.Party?.Snapshot();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (HasCommandLineFlag("-captureGoblin") || HasCommandLineFlag("-captureParty") || HasCommandLineFlag("-captureDefense") || HasCommandLineFlag("-captureAcrobatics"))
            {
                message = "Goblin evidence: in-memory progression only.";
                return true;
            }
#endif
            if (profileStore == null || sessionProgression == null)
            {
                message = "プロフィール保存がまだ初期化されていません。";
                return false;
            }
            if (!profileStore.TrySave(sessionProgression, out message))
            {
                return false;
            }

            if (CloudSaveSettings.TryPublishLocalFile(profileStore.ProfilePath, out string cloudMessage)
                && !string.IsNullOrEmpty(cloudMessage))
            {
                message = message + "\n" + cloudMessage;
            }
            else if (!string.IsNullOrEmpty(cloudMessage))
            {
                message = message + "\n" + cloudMessage;
            }

            return true;
        }

        private string ExportPlayerProfile()
        {
            if (profileStore == null || sessionProgression == null)
            {
                return "プロフィール保存がまだ初期化されていません。";
            }

            PlayerProfilePortability.TryExport(profileStore, sessionProgression, out string message);
            return message;
        }

        private string ImportPlayerProfile()
        {
            if (profileStore == null)
            {
                return "プロフィール保存がまだ初期化されていません。";
            }

            if (TryApplyCloudProfile(out string cloudMessage))
            {
                return cloudMessage + "\n" + profileStore.DescribeSavedFile();
            }

            if (!PlayerProfilePortability.TryImport(profileStore, out PlayerProgression imported, out string message))
            {
                return string.IsNullOrEmpty(cloudMessage) ? message : cloudMessage + "\n" + message;
            }

            ApplyImportedProgression(imported);
            return message + "\nLv." + sessionProgression.Level;
        }

        private string UseGoogleDriveSave()
        {
            if (Application.isMobilePlatform)
            {
                CloudSaveSettings.TryPickAndroidFolder(out string message);
                return message + "\n" + CloudSaveSettings.StatusLabel;
            }

            bool selected = CloudSaveSettings.TryPickWindowsFolder(
                "Google Driveのフォルダを選んでください（このPCでは I: が起点です）",
                out string desktopMessage);
            if (selected)
            {
                RebindProfileStore();
            }

            return desktopMessage + "\n" + CloudSaveSettings.StatusLabel;
        }

        private string UseFolderSave()
        {
            bool selected = Application.isMobilePlatform
                ? CloudSaveSettings.TryPickAndroidFolder(out string message)
                : CloudSaveSettings.TryPickWindowsFolder("セーブ先フォルダを選択", out message);
            if (selected)
            {
                RebindProfileStore();
            }

            return message + " " + CloudSaveSettings.StatusLabel;
        }

        private string UseLocalSave()
        {
            CloudSaveSettings.UseLocal();
            RebindProfileStore();
            return "端末ローカルへ戻しました。 " + CloudSaveSettings.StatusLabel;
        }

        private bool TryApplyCloudProfile()
        {
            return TryApplyCloudProfile(out _);
        }

        private bool TryApplyCloudProfile(out string message)
        {
            message = string.Empty;
            if (profileStore == null || sessionProgression == null || !AndroidCloudFolder.HasFolder)
            {
                return false;
            }

            if (!CloudSaveSettings.TryImportFromAndroidFolder(profileStore, out message))
            {
                return false;
            }

            var importedStore = new PlayerProfileStore(profileStore.ProfilePath);
            PlayerProgression imported = importedStore.LoadOrCreate(out string loadMessage);
            if (importedStore.HasUnsupportedVersion || loadMessage.IndexOf("初期化", StringComparison.Ordinal) >= 0)
            {
                message = loadMessage;
                return false;
            }

            ApplyImportedProgression(imported);
            message = message + "\n反映: Lv." + sessionProgression.Level;
            return true;
        }

        private void ApplyImportedProgression(PlayerProgression imported)
        {
            if (imported == null || sessionProgression == null)
            {
                return;
            }

            runController?.Party?.BeginProfileReplacement();
            try { sessionProgression.ReplaceFrom(imported); }
            finally { runController?.Party?.EndProfileReplacement(); }
            runController?.ApplyLoadedProgression();
        }

        private void RebindProfileStore()
        {
            var candidate = new PlayerProfileStore(CloudSaveSettings.ResolveProfilePath());
            if (sessionProgression == null)
            {
                return;
            }

            if (System.IO.File.Exists(candidate.ProfilePath))
            {
                PlayerProgression loaded = candidate.LoadOrCreate(out string loadMessage);
                if (candidate.HasUnsupportedVersion) { Debug.LogWarning(loadMessage); return; }
                if (loadMessage.IndexOf("初期化", StringComparison.Ordinal) < 0)
                {
                    profileStore = candidate;
                    ApplyImportedProgression(loaded);
                    return;
                }
            }

            profileStore = candidate;
            if (!TryApplyCloudProfile()) TrySavePlayerProfile(out _);
        }

        private Camera CreateCamera()
        {
            Camera existing = Camera.main;
            GameObject cameraObject = existing != null ? existing.gameObject : new GameObject("Main Camera");
            cameraObject.transform.SetParent(runtimeRoot, true);
            Camera camera = existing != null ? existing : cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            // 2.55 versus the previous 3.1 makes actors about 21.5% larger at
            // 1280x720 while retaining roughly nine metres of arena width.
            camera.orthographicSize = 2.55f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ForestArenaVisuals.UseForest
                ? new Color(0.51f, 0.62f, 0.54f) : new Color(0.42f, 0.75f, 0.94f);
            // A lower three-quarter view keeps the face and clothing silhouette
            // readable while preserving enough floor for the 3D combat plane.
            cameraObject.transform.position = new Vector3(0f, 5.75f, -8.85f);
            cameraObject.transform.LookAt(new Vector3(0f, 0.78f, 0f));

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }
            return camera;
        }

        private void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.64f, 0.58f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = ForestArenaVisuals.UseForest;
            if (ForestArenaVisuals.UseForest)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.61f, 0.69f, 0.67f);
                RenderSettings.ambientEquatorColor = new Color(0.49f, 0.56f, 0.43f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.29f, 0.22f);
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.51f, 0.62f, 0.54f);
                RenderSettings.fogStartDistance = 18f;
                RenderSettings.fogEndDistance = 53f;
            }

            CreateDirectionalLight(
                "Warm key light",
                Quaternion.Euler(44f, -32f, 0f),
                new Color(1f, 0.95f, 0.82f),
                1.06f,
                LightShadows.Soft);
            CreateDirectionalLight(
                "Cool fill light",
                Quaternion.Euler(32f, 148f, 0f),
                new Color(0.66f, 0.82f, 1f),
                0.28f,
                LightShadows.None);
        }

        private void CreateDirectionalLight(
            string objectName,
            Quaternion rotation,
            Color color,
            float intensity,
            LightShadows shadows)
        {
            var lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(runtimeRoot, false);
            lightObject.transform.rotation = rotation;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
        }

        private void CreateGrasslandArena()
        {
            if (ForestArenaVisuals.UseForest) forestVisuals = ForestArenaVisuals.Create(runtimeRoot);
            Material floorMaterial = forestVisuals != null
                ? forestVisuals.GroundMaterial : GrasslandArenaVisuals.CreateGroundMaterial();
            CreateCube(
                "Grassland ground",
                new Vector3(0f, -0.12f, 0f),
                new Vector3(StageLayout.Width, 0.24f, StageLayout.Depth),
                floorMaterial);
            if (forestVisuals == null)
            {
                GrasslandArenaVisuals.CreateBackdrop(runtimeRoot);
                GrasslandArenaVisuals.CreateDepthAccents(runtimeRoot);
            }

            CreateInvisibleBoundary(
                "North jump boundary",
                new Vector3(0f, 1.5f, StageLayout.MaxZ),
                new Vector3(StageLayout.Width, 3f, 0.22f));
            CreateInvisibleBoundary(
                "South jump boundary",
                new Vector3(0f, 1.5f, StageLayout.MinZ),
                new Vector3(StageLayout.Width, 3f, 0.22f));
            CreateInvisibleBoundary(
                "East jump boundary",
                new Vector3(StageLayout.MaxX, 1.5f, 0f),
                new Vector3(0.22f, 3f, StageLayout.Depth));
            CreateInvisibleBoundary(
                "West jump boundary",
                new Vector3(StageLayout.MinX, 1.5f, 0f),
                new Vector3(0.22f, 3f, StageLayout.Depth));
        }

        private PartyActor CreateCat(GameInputReader input, AudioDirector audioDirector)
        {
            var root = new GameObject("Silver Cat Companion");
            root.transform.SetParent(runtimeRoot, false);
            root.transform.position = new Vector3(-0.4f, 0.05f, 0f);
            var controller = root.AddComponent<CharacterController>();
            controller.radius = 0.25f; controller.height = 1.24f;
            controller.center = new Vector3(0f, 0.62f, 0f);
            controller.stepOffset = 0.14f; controller.skinWidth = 0.035f;
            var health = root.AddComponent<Health>();
            health.Initialize(Mathf.RoundToInt(tuning.PlayerMaxHealth * 0.8f), 0.68f);
            var resources = root.AddComponent<PlayerResources>();
            resources.Initialize(tuning.MaxStamina, tuning.PlayerMaxMp * 1.5f, tuning.MagicMpRegenPerSecond);
            var prefab = Resources.Load<GameObject>("Models/Characters/SilverCat/silver-cat-girl");
            if (prefab == null) throw new InvalidOperationException("Silver cat companion model is missing.");
            var slot = new GameObject("Silver Cat Visual");
            slot.transform.SetParent(root.transform, false);
            var model = Instantiate(prefab, slot.transform);
            var visual = slot.AddComponent<ModelCharacterVisual>();
            visual.Initialize(model.transform, Resources.Load<RuntimeAnimatorController>("Animations/Characters/SilverCat/SilverCatRuntime"),
                CharacterModelStyle.SilverCat, sceneCamera, 1f, 0f);
            var motor = root.AddComponent<PlayerMotor3D>();
            motor.Initialize(input, tuning, sceneCamera, visual);
            var combat = root.AddComponent<PlayerCombatController>();
            combat.IsCatMage = true;
            motor.CanPlunge = false;
            combat.Initialize(input, tuning, motor, resources, health, visual, audioDirector);
            var actor = root.AddComponent<PartyActor>();
            actor.Initialize(PartyMemberIds.CatMage);
            health.Damaged += (_, hit) => { if (!hit.IsGuarded) visual.PlayAction(CharacterAction.Hurt, 0.18f); };
            health.Died += (_, hit) => visual.PlayAction(CharacterAction.Defeated, 0.6f);
            return actor;
        }

        private PlayerParts CreatePlayer(GameInputReader input, AudioDirector audioDirector)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(runtimeRoot, false);
            player.transform.position = new Vector3(-1.6f, 0.05f, 0f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.radius = 0.27f;
            controller.height = 1.24f;
            controller.center = new Vector3(0f, 0.62f, 0f);
            controller.stepOffset = 0.14f;
            controller.skinWidth = 0.035f;

            Health health = player.AddComponent<Health>();
            health.Initialize(tuning.PlayerMaxHealth, 0.68f);
            health.SetTeam(DamageTeam.Party);
            PlayerResources resources = player.AddComponent<PlayerResources>();
            resources.Initialize(tuning.MaxStamina, tuning.PlayerMaxMp, tuning.MagicMpRegenPerSecond);

            CharacterSelection selection = GetCharacterSelection();
            bool snowKimono = selection == CharacterSelection.SnowKimono3D;
            bool meshySnowKimono = selection == CharacterSelection.MeshySnowKimono3D;
            bool azureMaidenUpgraded = selection == CharacterSelection.AzureMaidenUpgraded3D;
            bool trialAnimeGirl = selection == CharacterSelection.TrialAnimeGirl3D;
            Debug.Log($"CoffeeGAME heroine selection: {selection}.");
            ICharacterVisual visual = CreatePreferredVisual(
                player.transform,
                "Hero VisualSlot",
                trialAnimeGirl || snowKimono || meshySnowKimono || azureMaidenUpgraded ? null : HeroHd2dManifestResource,
                azureMaidenUpgraded ? AzureMaidenUpgradedModelResource : meshySnowKimono ? MeshySnowKimonoModelResource : snowKimono ? SnowKimonoModelResource : trialAnimeGirl ? TrialHeroModelResource : HeroModelResource,
                azureMaidenUpgraded ? AzureMaidenUpgradedControllerResource : meshySnowKimono ? MeshySnowKimonoControllerResource : snowKimono ? SnowKimonoControllerResource : trialAnimeGirl ? TrialHeroControllerResource : HeroControllerResource,
                azureMaidenUpgraded ? CharacterModelStyle.AzureMaidenUpgraded : meshySnowKimono ? CharacterModelStyle.MeshySnowKimono : snowKimono ? CharacterModelStyle.SnowKimono : trialAnimeGirl ? CharacterModelStyle.Imported : CharacterModelStyle.Heroine,
                180f,
                1f,
                Resources.Load<Sprite>("Art/Hero/hero-sprite"),
                1f,
                sceneCamera,
                new Color(0.35f, 0.76f, 1f),
                2);
            if (snowKimono)
            {
                Debug.Log($"CoffeeGAME heroine visual: snow-kimono 3D selected ({visual.GetType().Name}).");
            }
            else if (meshySnowKimono)
            {
                Debug.Log($"CoffeeGAME heroine visual: Meshy snow-kimono 3D selected ({visual.GetType().Name}).");
            }
            else if (azureMaidenUpgraded)
            {
                Debug.Log($"CoffeeGAME heroine visual: upgraded Azure Maiden 3D selected ({visual.GetType().Name}).");
            }

            PlayerMotor3D motor = player.AddComponent<PlayerMotor3D>();
            motor.Initialize(input, tuning, sceneCamera, visual);
            player.AddComponent<HeroineCombatVoice>();
            PlayerCombatController combat = player.AddComponent<PlayerCombatController>();
            combat.Initialize(input, tuning, motor, resources, health, visual, audioDirector);
            if (trialAnimeGirl)
            {
                AttachTrialHeldSwordSet(visual);
            }

            health.Damaged += (_, damage) =>
            {
                if (damage.IsGuarded) return;
                visual.SetTint(new Color(1f, 0.48f, 0.48f));
                visual.PlayAction(CharacterAction.Hurt, 0.18f);
                audioDirector.Play(CombatSound.Impact, 0.62f);
            };
            health.Died += (_, damage) => visual.PlayAction(CharacterAction.Defeated, 0.6f);

            return new PlayerParts(player, health, resources, motor, combat);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static bool TryGetCommandLineValue(string key, out string value)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                {
                    value = args[i + 1];
                    return !string.IsNullOrWhiteSpace(value);
                }
            }
            value = string.Empty;
            return false;
        }

        private IEnumerator CaptureRuntimeScene(string capturePath)
        {
            string fullPath = Path.GetFullPath(capturePath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            yield return new WaitForSecondsRealtime(2f);
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(sceneCamera, request);
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(fullPath, image.EncodeToPNG());
                Debug.Log("CoffeeGAME runtime scene captured: " + fullPath);
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
                target.Release();
                Destroy(target);
            }
            yield return null;
            Application.Quit(0);
        }
#endif

        private CombatEnemy CreateEnemy(string claimId, Transform target, Health targetHealth)
        {
            return EnemyEncounterRoster.At(enemySpawnIndex) == EnemyKind.Goblin
                ? CreateGoblin(claimId, target, targetHealth)
                : CreateSlime(claimId, target, targetHealth);
        }

        private CombatEnemy CreateGoblin(string claimId, Transform target, Health targetHealth)
        {
            var root = new GameObject("Forest Goblin");
            root.transform.SetParent(runtimeRoot, false);
            int index = enemySpawnIndex++;
            root.transform.position = EnemySpawnPoints[index % EnemySpawnPoints.Length];
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = 0.3f;
            collider.height = 1.15f;
            collider.center = new Vector3(0f, 0.575f, 0f);
            var health = root.AddComponent<Health>();
            health.Initialize(GoblinController.MaximumHealth);
            health.SetTeam(DamageTeam.Enemy);
            var slot = new GameObject("Goblin VisualSlot");
            slot.transform.SetParent(root.transform, false);
            var visual = slot.AddComponent<GoblinCharacterVisual>();
            visual.Initialize();
            root.AddComponent<GoblinController>().Initialize(tuning, target, targetHealth,
                health, collider, visual, index);
            var enemy = root.AddComponent<CombatEnemy>();
            enemy.Initialize(claimId, EnemyKind.Goblin, health, GoblinController.Reward);
            Debug.Log("CoffeeGAME goblin-v8 spawned: Meshy forest goblin, club, animated combat.");
            return enemy;
        }

        private CombatEnemy CreateSlime(string claimId, Transform target, Health targetHealth)
        {
            var slime = new GameObject("Slime");
            slime.transform.SetParent(runtimeRoot, false);
            slime.transform.position = EnemySpawnPoints[enemySpawnIndex % EnemySpawnPoints.Length];
            enemySpawnIndex++;

            CapsuleCollider collider = slime.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = 0.38f;
            collider.height = 0.76f;
            collider.center = new Vector3(0f, 0.38f, 0f);

            Health health = slime.AddComponent<Health>();
            health.Initialize(tuning.SlimeMaxHealth);
            health.SetTeam(DamageTeam.Enemy);
            ICharacterVisual visual = CreatePreferredVisual(
                slime.transform,
                "Slime VisualSlot",
                SlimeHd2dManifestResource,
                SlimeModelResource,
                SlimeControllerResource,
                CharacterModelStyle.Slime,
                42f,
                1f,
                Resources.Load<Sprite>("Art/Slime/slime-sprite"),
                0.86f,
                sceneCamera,
                new Color(0.42f, 0.9f, 0.76f),
                0);

            SlimeController controller = slime.AddComponent<SlimeController>();
            controller.Initialize(claimId, tuning, target, targetHealth, health, collider, visual);
            var enemy = slime.AddComponent<CombatEnemy>();
            enemy.Initialize(claimId, EnemyKind.Slime, health, tuning.SlimeReward);
            return enemy;
        }

        private static void AttachTrialHeldSwordSet(ICharacterVisual visual)
        {
            var modelVisual = visual as ModelCharacterVisual;
            if (modelVisual == null)
            {
                return;
            }

            GameObject attackPrefab = Resources.Load<GameObject>(TrialHeroAttackModelResource);
            RuntimeAnimatorController attackController =
                Resources.Load<RuntimeAnimatorController>(TrialHeroAttackControllerResource);
            if (attackPrefab == null || attackController == null)
            {
                Debug.LogWarning(
                    "CoffeeGAME trial held-sword attack set is missing. Sword stays on the sheathed mesh.");
                return;
            }

            modelVisual.AttachHeldSwordSet(
                attackPrefab,
                attackController,
                ModelCharacterVisual.TrialHeldSwordAlbedoResource,
                ModelCharacterVisual.TrialHeldSwordNormalResource);
        }

        private ICharacterVisual CreatePreferredVisual(
            Transform parent,
            string objectName,
            string hd2dManifestResourcePath,
            string modelResourcePath,
            string controllerResourcePath,
            CharacterModelStyle modelStyle,
            float maxAngleFromCamera,
            float modelScale,
            Sprite fallbackSprite,
            float fallbackSpriteScale,
            Camera camera,
            Color fallbackColor,
            int depthTieBreaker)
        {
            if (visualBackend == CharacterVisualBackend.Hd2d)
            {
                ICharacterVisual hd2dVisual = TryCreateHd2dVisual(
                    parent,
                    objectName,
                    hd2dManifestResourcePath,
                    fallbackSprite,
                    modelScale,
                    camera,
                    depthTieBreaker);
                if (hd2dVisual != null)
                {
                    return hd2dVisual;
                }
            }

            GameObject modelPrefab = visualBackend != CharacterVisualBackend.StaticSprite
                ? Resources.Load<GameObject>(modelResourcePath)
                : null;
            if (modelPrefab != null)
            {
                GameObject visualObject = null;
                try
                {
                    visualObject = new GameObject(objectName);
                    visualObject.transform.SetParent(parent, false);
                    visualObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                    GameObject modelInstance = Instantiate(modelPrefab, visualObject.transform, false);
                    modelInstance.name = $"{modelPrefab.name} Model";
                    Collider[] importedColliders = modelInstance.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < importedColliders.Length; i++)
                    {
                        importedColliders[i].enabled = false;
                    }

                    RuntimeAnimatorController runtimeController =
                        Resources.Load<RuntimeAnimatorController>(controllerResourcePath);
                    if (runtimeController == null)
                    {
                        throw new InvalidOperationException(
                            $"Animator controller for {modelResourcePath} is missing. " +
                            "Run CoffeeGAME > Setup first combat slice after importing the FBX.");
                    }

                    ModelCharacterVisual visual = visualObject.AddComponent<ModelCharacterVisual>();
                    visual.Initialize(
                        modelInstance.transform,
                        runtimeController,
                        modelStyle,
                        camera,
                        modelScale,
                        0f,
                        maxAngleFromCamera);
                    return visual;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"Could not create the 3D visual at Resources/{modelResourcePath}; using the sprite fallback. " +
                        exception.Message,
                        this);

                    if (visualObject != null)
                    {
                        Destroy(visualObject);
                    }
                }
            }

            return CreateSpriteVisual(
                parent,
                objectName,
                fallbackSprite,
                fallbackSpriteScale,
                camera,
                fallbackColor);
        }

        private ICharacterVisual TryCreateHd2dVisual(
            Transform parent,
            string objectName,
            string manifestResourcePath,
            Sprite fallbackSprite,
            float scale,
            Camera camera,
            int depthTieBreaker)
        {
            if (string.IsNullOrWhiteSpace(manifestResourcePath))
            {
                return null;
            }

            GameObject visualObject = null;
            try
            {
                visualObject = new GameObject(objectName);
                visualObject.transform.SetParent(parent, false);
                visualObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                DirectionalSpriteCharacterVisual visual =
                    visualObject.AddComponent<DirectionalSpriteCharacterVisual>();
                if (visual.TryInitialize(
                    manifestResourcePath,
                    fallbackSprite,
                    scale,
                    camera,
                    depthTieBreaker))
                {
                    return visual;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Could not create the HD-2D visual from Resources/{manifestResourcePath}; " +
                    $"using the 3D fallback. {exception.Message}",
                    this);
            }

            if (visualObject != null)
            {
                Destroy(visualObject);
            }
            return null;
        }

        private SpriteCharacterVisual CreateSpriteVisual(
            Transform parent,
            string objectName,
            Sprite sprite,
            float scale,
            Camera camera,
            Color fallbackColor)
        {
            var visualObject = new GameObject(objectName);
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = objectName.StartsWith("Hero", StringComparison.Ordinal) ? 10 : 5;
            SpriteCharacterVisual visual = visualObject.AddComponent<SpriteCharacterVisual>();
            visual.Initialize(sprite, scale, camera);

            if (sprite == null)
            {
                Debug.LogWarning($"Sprite for {objectName} is not imported yet. Using a temporary 3D marker.", this);
                GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                fallback.name = "Temporary visual marker";
                fallback.transform.SetParent(visualObject.transform, false);
                fallback.transform.localPosition = new Vector3(0f, 0.55f, 0f);
                fallback.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
                Collider fallbackCollider = fallback.GetComponent<Collider>();
                if (fallbackCollider != null)
                {
                    Destroy(fallbackCollider);
                }
                Renderer fallbackRenderer = fallback.GetComponent<Renderer>();
                fallbackRenderer.material = CreateLitMaterial("Temporary actor material", fallbackColor);
            }
            return visual;
        }

        private GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = objectName;
            cube.transform.SetParent(runtimeRoot, false);
            cube.transform.position = position;
            cube.transform.localScale = scale;
            Renderer renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
            return cube;
        }

        private void CreateInvisibleBoundary(string objectName, Vector3 position, Vector3 scale)
        {
            GameObject boundary = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boundary.name = objectName;
            boundary.transform.SetParent(runtimeRoot, false);
            boundary.transform.position = position;
            boundary.transform.localScale = scale;
            Renderer renderer = boundary.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

        private static Material CreateLitMaterial(string materialName, Color color)
        {
            Material material = RuntimeMaterialFactory.CreateLit(materialName, color);
            if (material != null && material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }
            return material;
        }

        private readonly struct PlayerParts
        {
            public PlayerParts(GameObject root, Health health, PlayerResources resources, PlayerMotor3D motor, PlayerCombatController combat)
            {
                Root = root;
                Health = health;
                Resources = resources;
                Motor = motor;
                Combat = combat;
            }

            public GameObject Root { get; }
            public Health Health { get; }
            public PlayerResources Resources { get; }
            public PlayerMotor3D Motor { get; }
            public PlayerCombatController Combat { get; }
        }
    }
}
