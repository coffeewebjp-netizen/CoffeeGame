using System;
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

    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class CombatSliceBootstrap : MonoBehaviour
    {
        private const string HeroModelResource = "Models/Hero/heroine-v4";
        private const string HeroControllerResource = "Animations/Hero/HeroRuntime";
        private const string HeroHd2dManifestResource = "Art/HD2D/hero-hd2d";
        private const string SlimeModelResource = "Models/Slime/slime-v2";
        private const string SlimeControllerResource = "Animations/Slime/SlimeRuntime";
        private const string SlimeHd2dManifestResource = "Art/HD2D/slime-hd2d";

        private static readonly Vector3[] SlimeSpawnPoints =
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
        private Health playerHealth;
        private int slimeSpawnIndex;
        private PlayerProgression sessionProgression;
        private PlayerProfileStore profileStore;
        private CoffeeLearningConnectionPresenter coffeeLearningConnection;

        public ILearningBridge LearningBridge => coffeeLearningConnection?.LearningBridge ?? UnavailableLearningBridge;

        private void Awake()
        {
            if (FindObjectsByType<CombatSliceBootstrap>(FindObjectsInactive.Exclude).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            BuildCombatSlice();
        }

        private void OnApplicationQuit()
        {
            coffeeLearningConnection?.CancelPendingOrActiveAction();
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
            playerHealth = player.Health;
            EnsurePlayerProfileLoaded();

            var runController = gameObject.AddComponent<CombatRunController>();
            runController.Initialize(
                tuning,
                sessionProgression,
                input,
                audioDirector,
                player.Health,
                player.Resources,
                player.Motor,
                player.Combat,
                claimId => CreateSlime(claimId, player.Root.transform, player.Health),
                () => slimeSpawnIndex = 0);

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
            _ = coffeeLearningConnection.RefreshAccountIdentityAsync();

            FixedCameraRig cameraRig = sceneCamera.gameObject.AddComponent<FixedCameraRig>();
            cameraRig.Initialize(player.Root.transform);
            cameraRig.SetBounds(
                StageLayout.CameraMinX,
                StageLayout.CameraMaxX,
                StageLayout.CameraMinZ,
                StageLayout.CameraMaxZ);
            CameraOrbitInputDriver orbitInput = sceneCamera.gameObject.AddComponent<CameraOrbitInputDriver>();
            orbitInput.Initialize(cameraRig, input);
        }

        private void EnsurePlayerProfileLoaded()
        {
            if (sessionProgression != null)
            {
                return;
            }

            profileStore = new PlayerProfileStore(CloudSaveSettings.ResolveProfilePath());
            sessionProgression = profileStore.LoadOrCreate(out string message);
            sessionProgression.Changed += SavePlayerProfile;
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

            if (AndroidCloudFolder.HasFolder
                && CloudSaveSettings.TryImportFromAndroidFolder(profileStore, out string cloudMessage))
            {
                return cloudMessage + "\n" + profileStore.DescribeSavedFile();
            }

            if (!PlayerProfilePortability.TryImport(profileStore, out _, out string message))
            {
                return message;
            }

            return message;
        }

        private string UseGoogleDriveSave()
        {
            if (Application.isMobilePlatform)
            {
                CloudSaveSettings.TryPickAndroidFolder(out string message);
                return message + "\n" + CloudSaveSettings.StatusLabel;
            }

            bool selected = CloudSaveSettings.TryUseGoogleDrive(out string desktopMessage);
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
                : CloudSaveSettings.TryUseClipboardFolder(out message);
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

        private void RebindProfileStore()
        {
            profileStore = new PlayerProfileStore(CloudSaveSettings.ResolveProfilePath());
            if (sessionProgression != null)
            {
                TrySavePlayerProfile(out _);
            }
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
            camera.backgroundColor = new Color(0.42f, 0.75f, 0.94f);
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
            Material floorMaterial = GrasslandArenaVisuals.CreateGroundMaterial();
            CreateCube(
                "Grassland ground",
                new Vector3(0f, -0.12f, 0f),
                new Vector3(StageLayout.Width, 0.24f, StageLayout.Depth),
                floorMaterial);
            GrasslandArenaVisuals.CreateBackdrop(runtimeRoot);
            GrasslandArenaVisuals.CreateDepthAccents(runtimeRoot);

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
            PlayerResources resources = player.AddComponent<PlayerResources>();
            resources.Initialize(tuning.MaxStamina, tuning.PlayerMaxMp, tuning.MagicMpRegenPerSecond);

            ICharacterVisual visual = CreatePreferredVisual(
                player.transform,
                "Hero VisualSlot",
                HeroHd2dManifestResource,
                HeroModelResource,
                HeroControllerResource,
                CharacterModelStyle.Heroine,
                180f,
                1f,
                Resources.Load<Sprite>("Art/Hero/hero-sprite"),
                1f,
                sceneCamera,
                new Color(0.35f, 0.76f, 1f),
                2);

            PlayerMotor3D motor = player.AddComponent<PlayerMotor3D>();
            motor.Initialize(input, tuning, sceneCamera, visual);
            PlayerCombatController combat = player.AddComponent<PlayerCombatController>();
            combat.Initialize(input, tuning, motor, resources, health, visual, audioDirector);

            health.Damaged += (_, damage) =>
            {
                visual.SetTint(new Color(1f, 0.48f, 0.48f));
                visual.PlayAction(CharacterAction.Hurt, 0.18f);
                audioDirector.Play(CombatSound.Impact, 0.62f);
            };
            health.Died += (_, damage) => visual.PlayAction(CharacterAction.Defeated, 0.6f);

            return new PlayerParts(player, health, resources, motor, combat);
        }

        private SlimeController CreateSlime(string claimId, Transform target, Health targetHealth)
        {
            var slime = new GameObject("Slime");
            slime.transform.SetParent(runtimeRoot, false);
            slime.transform.position = SlimeSpawnPoints[slimeSpawnIndex % SlimeSpawnPoints.Length];
            slimeSpawnIndex++;

            CapsuleCollider collider = slime.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.radius = 0.38f;
            collider.height = 0.76f;
            collider.center = new Vector3(0f, 0.38f, 0f);

            Health health = slime.AddComponent<Health>();
            health.Initialize(tuning.SlimeMaxHealth);
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
            return controller;
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
