using System;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Presentation;
using CoffeeGame.Run;
using CoffeeGame.UI;
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

        [SerializeField] private CharacterVisualBackend visualBackend = CharacterVisualBackend.Hd2d;

        private CombatTuning tuning;
        private Transform runtimeRoot;
        private Camera sceneCamera;
        private Health playerHealth;
        private int slimeSpawnIndex;
        private PlayerProgression sessionProgression;

        private void Awake()
        {
            if (FindObjectsByType<CombatSliceBootstrap>(FindObjectsInactive.Exclude).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            BuildCombatSlice();
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
            CreateRoom();

            GameInputReader input = gameObject.AddComponent<GameInputReader>();
            AudioDirector audioDirector = gameObject.AddComponent<AudioDirector>();
            audioDirector.Initialize();

            PlayerParts player = CreatePlayer(input, audioDirector);
            playerHealth = player.Health;
            sessionProgression ??= new PlayerProgression();

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
            hud.Initialize(runController, input);

            FixedCameraRig cameraRig = sceneCamera.gameObject.AddComponent<FixedCameraRig>();
            cameraRig.Initialize(player.Root.transform);
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
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.105f);
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
            RenderSettings.ambientLight = new Color(0.34f, 0.38f, 0.44f);
            RenderSettings.ambientIntensity = 1f;

            CreateDirectionalLight(
                "Warm key light",
                Quaternion.Euler(44f, -32f, 0f),
                new Color(1f, 0.92f, 0.86f),
                1f,
                LightShadows.Soft);
            CreateDirectionalLight(
                "Cool fill light",
                Quaternion.Euler(32f, 148f, 0f),
                new Color(0.58f, 0.76f, 1f),
                0.34f,
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

        private void CreateRoom()
        {
            Material floorMaterial = CreateLitMaterial("Jade floor", new Color(0.11f, 0.26f, 0.24f));
            Material wallMaterial = CreateLitMaterial("Dark jade wall", new Color(0.07f, 0.14f, 0.17f));

            CreateCube("Arena floor", new Vector3(0f, -0.12f, 0f), new Vector3(9.6f, 0.24f, 5.4f), floorMaterial);
            CreateCube("North wall", new Vector3(0f, 0.3f, 2.78f), new Vector3(9.8f, 0.65f, 0.18f), wallMaterial);
            CreateCube("South wall", new Vector3(0f, 0.3f, -2.78f), new Vector3(9.8f, 0.65f, 0.18f), wallMaterial);
            CreateCube("East wall", new Vector3(4.88f, 0.3f, 0f), new Vector3(0.18f, 0.65f, 5.4f), wallMaterial);
            CreateCube("West wall", new Vector3(-4.88f, 0.3f, 0f), new Vector3(0.18f, 0.65f, 5.4f), wallMaterial);

            CreateInvisibleBoundary("North jump boundary", new Vector3(0f, 1.5f, 2.78f), new Vector3(9.8f, 3f, 0.22f));
            CreateInvisibleBoundary("South jump boundary", new Vector3(0f, 1.5f, -2.78f), new Vector3(9.8f, 3f, 0.22f));
            CreateInvisibleBoundary("East jump boundary", new Vector3(4.88f, 1.5f, 0f), new Vector3(0.22f, 3f, 5.4f));
            CreateInvisibleBoundary("West jump boundary", new Vector3(-4.88f, 1.5f, 0f), new Vector3(0.22f, 3f, 5.4f));

            for (int i = -4; i <= 4; i++)
            {
                GameObject marker = CreateCube($"Floor line {i}", new Vector3(i, 0.012f, 0f), new Vector3(0.018f, 0.012f, 5.2f), wallMaterial);
                Collider markerCollider = marker.GetComponent<Collider>();
                if (markerCollider != null)
                {
                    Destroy(markerCollider);
                }
            }
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
