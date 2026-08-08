using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    /// <summary>
    /// Atlas-driven HD-2D presentation for an actor whose movement, collision and
    /// combat remain in the 3D world. Art direction is selected relative to the
    /// gameplay camera, while the renderer itself stays camera-facing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DirectionalSpriteCharacterVisual : MonoBehaviour, ICharacterVisual
    {
        private sealed class RuntimeClip
        {
            public RuntimeClip(Sprite[] frames, float framesPerSecond, bool loop, bool holdLastFrame)
            {
                Frames = frames;
                FramesPerSecond = Mathf.Max(0.01f, framesPerSecond);
                Loop = loop;
                HoldLastFrame = holdLastFrame;
            }

            public Sprite[] Frames { get; }
            public float FramesPerSecond { get; }
            public bool Loop { get; }
            public bool HoldLastFrame { get; }
        }

        private readonly Dictionary<int, RuntimeClip> clips = new Dictionary<int, RuntimeClip>();
        private readonly Dictionary<string, Sprite> leasedSprites =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform spriteTransform;

        private Hd2dSpriteManifest manifest;
        private Camera facingCamera;
        private Transform actorRoot;
        private Vector3 baseLocalScale = Vector3.one;
        private Vector3 baseSpritePosition = Vector3.zero;
        private Color baseColor = Color.white;
        private Color requestedTint = Color.white;
        private Hd2dFacingDirection facing = Hd2dFacingDirection.Down;
        private CharacterAction locomotion = CharacterAction.Idle;
        private CharacterAction activeState = CharacterAction.Idle;
        private RuntimeClip activeClip;
        private Sprite fallbackSprite;
        private int frameIndex;
        private float frameClock;
        private float locomotionSpeed = 1f;
        private float actionElapsed;
        private float actionDuration;
        private bool actionPlaying;
        private bool actionCompletionPending;
        private int completionHoldFrames;
        private bool defeated;
        private int sortingTieBreaker;

        private GameObject shadowObject;
        private Mesh shadowMesh;
        private MeshRenderer shadowRenderer;
        private Material shadowMaterial;
        private float groundWorldY;
        private float airHeight;
        private float previousAirHeight;
        private float airHeightTrend;
        private float lastShadowFactor = -1f;

        public SpriteRenderer Renderer => spriteRenderer;
        public CharacterAction CurrentAction => activeState;
        public string CharacterId => manifest != null ? manifest.characterId : string.Empty;

        /// <summary>
        /// Returns false without throwing when art is absent or malformed. The
        /// bootstrap can then retain the imported 3D model as a safe fallback.
        /// </summary>
        public bool TryInitialize(
            string manifestResourcePath,
            Sprite suppliedFallbackSprite,
            float scale,
            Camera cameraToFace,
            int depthTieBreaker = 0)
        {
            if (!Hd2dSpriteManifestLoader.TryLoad(
                manifestResourcePath,
                out Hd2dSpriteManifest loadedManifest,
                out string error))
            {
                Debug.LogWarning($"HD-2D manifest could not be loaded: {error}", this);
                return false;
            }

            manifest = loadedManifest;
            try
            {
                facingCamera = cameraToFace;
                actorRoot = transform.parent != null ? transform.parent : transform;
                groundWorldY = actorRoot.position.y;
                sortingTieBreaker = depthTieBreaker;

                if (!TryBuildClipLibrary(
                    out Dictionary<int, RuntimeClip> loadedClips,
                    out string libraryError))
                {
                    Debug.LogWarning(
                        $"HD-2D character '{manifest.characterId}' could not be initialized atomically: " +
                        $"{libraryError}. Retaining the 3D fallback.",
                        this);
                    clips.Clear();
                    ReleaseLeasedSprites();
                    fallbackSprite = null;
                    manifest = null;
                    return false;
                }

                clips.Clear();
                foreach (KeyValuePair<int, RuntimeClip> pair in loadedClips)
                {
                    clips.Add(pair.Key, pair.Value);
                }

                fallbackSprite = suppliedFallbackSprite != null
                    ? suppliedFallbackSprite
                    : LoadFallbackSprite(manifest.fallbackSpritePath);
                EnsureRenderer();

                baseLocalScale = Vector3.one * (Mathf.Max(0.01f, scale) * manifest.visualScale);
                transform.localScale = baseLocalScale;
                baseSpritePosition = spriteTransform.localPosition;
                baseColor = spriteRenderer.color;
                requestedTint = baseColor;

                CameraFacingBillboard billboard = GetComponent<CameraFacingBillboard>();
                if (billboard == null)
                {
                    billboard = gameObject.AddComponent<CameraFacingBillboard>();
                }
                billboard.SetCamera(cameraToFace);

                ConfigureSpriteRenderer();
                CreateBlobShadow();
                ResetState(Vector3.back);
                Debug.Log($"HD-2D visual initialized: {manifest.characterId}", this);
                return true;
            }
            catch
            {
                clips.Clear();
                ReleaseLeasedSprites();
                fallbackSprite = null;
                manifest = null;
                throw;
            }
        }

        public void ResetState(Vector3 worldDirection)
        {
            defeated = false;
            actionPlaying = false;
            actionCompletionPending = false;
            completionHoldFrames = 0;
            actionElapsed = 0f;
            actionDuration = 0f;
            locomotion = CharacterAction.Idle;
            activeState = CharacterAction.Idle;
            locomotionSpeed = 1f;
            frameClock = 0f;
            frameIndex = 0;
            transform.localScale = baseLocalScale;
            spriteTransform.localPosition = baseSpritePosition;
            spriteTransform.localRotation = Quaternion.identity;
            spriteTransform.localScale = Vector3.one;
            requestedTint = baseColor;
            SetFacing(worldDirection);
            SelectClip(CharacterAction.Idle, false);
            ApplyDisplayColor();
            UpdateShadow(true);
        }

        public void SetFacing(Vector3 worldDirection)
        {
            if (manifest == null || worldDirection.sqrMagnitude < 0.0025f)
            {
                return;
            }

            Hd2dFacingDirection nextFacing = Hd2dFacingDirection.Down;
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up).normalized;
            Vector3 cameraForward = facingCamera != null
                ? Vector3.ProjectOnPlane(facingCamera.transform.forward, Vector3.up)
                : Vector3.forward;
            Vector3 cameraRight = facingCamera != null
                ? Vector3.ProjectOnPlane(facingCamera.transform.right, Vector3.up)
                : Vector3.right;
            cameraForward = cameraForward.sqrMagnitude > 0.001f ? cameraForward.normalized : Vector3.forward;
            cameraRight = cameraRight.sqrMagnitude > 0.001f ? cameraRight.normalized : Vector3.right;
            float forwardAmount = Vector3.Dot(planarDirection, cameraForward);
            float sideAmount = Vector3.Dot(planarDirection, cameraRight);
            bool flip = Hd2dFacingPolicy.ResolveHorizontalFlip(
                sideAmount,
                spriteRenderer.flipX);
            if (manifest.directional)
            {
                nextFacing = Hd2dFacingPolicy.ResolveDirection(
                    forwardAmount,
                    sideAmount,
                    facing);
                if (nextFacing != Hd2dFacingDirection.Side)
                {
                    // Front/back art should not mirror as a side-facing pose.
                    flip = false;
                }
            }

            bool directionChanged = nextFacing != facing;
            facing = nextFacing;
            spriteRenderer.flipX = flip;
            if (directionChanged)
            {
                SelectClip(activeState, true);
            }
        }

        public void SetLocomotion(CharacterAction action, float normalizedSpeed)
        {
            if (action != CharacterAction.Idle && action != CharacterAction.Walk && action != CharacterAction.Run)
            {
                action = normalizedSpeed > 0.55f ? CharacterAction.Run :
                    normalizedSpeed > 0.01f ? CharacterAction.Walk : CharacterAction.Idle;
            }

            locomotion = action;
            locomotionSpeed = action == CharacterAction.Idle
                ? 1f
                : Mathf.Lerp(0.72f, 1.28f, Mathf.Clamp01(normalizedSpeed));

            if (actionPlaying || defeated || activeState == locomotion)
            {
                return;
            }

            SelectClip(locomotion, false);
        }

        public void PlayAction(CharacterAction action, float duration)
        {
            if (defeated && action != CharacterAction.Defeated)
            {
                return;
            }

            if (actionPlaying &&
                GetActionPriority(action) < GetActionPriority(activeState) &&
                !CharacterVisualTransitionPolicy.IsForcedPhysicsTransition(activeState, action))
            {
                return;
            }

            defeated = action == CharacterAction.Defeated;
            actionPlaying = true;
            actionCompletionPending = false;
            completionHoldFrames = 0;
            actionElapsed = 0f;
            actionDuration = float.IsPositiveInfinity(duration)
                ? float.PositiveInfinity
                : Mathf.Max(0.05f, duration);
            SelectClip(action, false);
        }

        public void SetAirHeight(float height)
        {
            previousAirHeight = airHeight;
            airHeight = Mathf.Max(0f, height - groundWorldY);
            airHeightTrend = airHeight - previousAirHeight;
            UpdateShadow(false);
        }

        public void SetTint(Color color)
        {
            requestedTint = color;
            ApplyDisplayColor();
        }

        private void Update()
        {
            if (spriteRenderer == null || activeClip == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (actionPlaying)
            {
                if (actionCompletionPending)
                {
                    if (completionHoldFrames > 0)
                    {
                        completionHoldFrames--;
                        ApplyActionPose();
                        ApplyDisplayColor();
                        return;
                    }

                    FinishAction();
                    ApplyDisplayColor();
                    return;
                }

                actionElapsed = float.IsPositiveInfinity(actionDuration)
                    ? actionElapsed + deltaTime
                    : Mathf.Min(actionDuration, actionElapsed + deltaTime);
                UpdateActionFrame(deltaTime);
                ApplyActionPose();

                if (!float.IsPositiveInfinity(actionDuration) &&
                    actionElapsed >= actionDuration && !defeated)
                {
                    // Do not swap back to locomotion in the same Update that
                    // selects the final action frame: it must reach the renderer.
                    SetFrame(activeClip.Frames.Length - 1);
                    actionCompletionPending = true;
                    completionHoldFrames = activeClip.HoldLastFrame ? 1 : 0;
                }
            }
            else
            {
                AdvanceLoopingFrame(deltaTime, locomotionSpeed);
                ApplyLocomotionPose();
            }

            ApplyDisplayColor();
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null || actorRoot == null || manifest == null)
            {
                return;
            }

            Vector3 planarCameraForward = facingCamera != null
                ? Vector3.ProjectOnPlane(facingCamera.transform.forward, Vector3.up)
                : Vector3.forward;
            planarCameraForward = planarCameraForward.sqrMagnitude > 0.001f
                ? planarCameraForward.normalized
                : Vector3.forward;
            Vector3 fromCamera = facingCamera != null
                ? Vector3.ProjectOnPlane(actorRoot.position - facingCamera.transform.position, Vector3.up)
                : actorRoot.position;
            float cameraDepth = Vector3.Dot(fromCamera, planarCameraForward);
            // Transparent items nearer the camera need the greater order.
            int depth = -Mathf.RoundToInt(cameraDepth * manifest.depthSortingStepsPerUnit);
            int order = manifest.sortingBase + sortingTieBreaker + depth;
            spriteRenderer.sortingOrder = order;
            if (shadowRenderer != null)
            {
                shadowRenderer.sortingOrder = order - 1;
                Vector3 actorPosition = actorRoot.position;
                shadowObject.transform.position = new Vector3(
                    actorPosition.x,
                    groundWorldY + manifest.shadowYOffset,
                    actorPosition.z);
            }
        }

        private void UpdateActionFrame(float deltaTime)
        {
            if (activeClip.Frames.Length <= 1)
            {
                return;
            }

            if (activeClip.Loop)
            {
                AdvanceLoopingFrame(deltaTime, 1f);
                return;
            }

            if (float.IsPositiveInfinity(actionDuration))
            {
                AdvanceLoopingFrame(deltaTime, 1f);
                return;
            }

            float normalized = actionDuration <= 0f ? 1f : Mathf.Clamp01(actionElapsed / actionDuration);
            int targetFrame = Mathf.Min(
                activeClip.Frames.Length - 1,
                Mathf.FloorToInt(normalized * activeClip.Frames.Length));
            SetFrame(targetFrame);
        }

        private void AdvanceLoopingFrame(float deltaTime, float speedMultiplier)
        {
            if (activeClip == null || activeClip.Frames.Length <= 1)
            {
                return;
            }

            frameClock += deltaTime * activeClip.FramesPerSecond * Mathf.Max(0.05f, speedMultiplier);
            int wholeFrames = Mathf.FloorToInt(frameClock);
            if (wholeFrames <= 0)
            {
                return;
            }

            frameClock -= wholeFrames;
            int nextFrame = frameIndex + wholeFrames;
            if (activeClip.Loop)
            {
                nextFrame %= activeClip.Frames.Length;
            }
            else
            {
                nextFrame = Mathf.Min(activeClip.Frames.Length - 1, nextFrame);
            }
            SetFrame(nextFrame);
        }

        private void FinishAction()
        {
            CharacterAction completedAction = activeState;
            actionPlaying = false;
            actionCompletionPending = false;
            completionHoldFrames = 0;
            actionElapsed = 0f;
            actionDuration = 0f;
            requestedTint = baseColor;
            ApplyNeutralPose();

            // A combat action can temporarily override Jump/Fall. Restore the
            // physics-driven air pose instead of flashing to ground locomotion.
            if (airHeight > 0.06f &&
                completedAction != CharacterAction.Jump &&
                completedAction != CharacterAction.Fall &&
                completedAction != CharacterAction.Plunge &&
                completedAction != CharacterAction.Land)
            {
                actionPlaying = true;
                actionDuration = float.PositiveInfinity;
                actionElapsed = 0f;
                SelectClip(
                    airHeightTrend >= -0.0001f ? CharacterAction.Jump : CharacterAction.Fall,
                    false);
                return;
            }
            SelectClip(locomotion, false);
        }

        private void SelectClip(CharacterAction state, bool preserveProgress)
        {
            float normalizedFrame = activeClip != null && activeClip.Frames.Length > 1
                ? (float)frameIndex / (activeClip.Frames.Length - 1)
                : 0f;

            activeState = state;
            activeClip = ResolveClip(state, facing);
            frameClock = 0f;
            if (activeClip == null || activeClip.Frames.Length == 0)
            {
                spriteRenderer.sprite = fallbackSprite;
                frameIndex = 0;
                return;
            }

            int nextFrame = preserveProgress
                ? Mathf.RoundToInt(normalizedFrame * (activeClip.Frames.Length - 1))
                : 0;
            SetFrame(nextFrame);
        }

        private RuntimeClip ResolveClip(CharacterAction action, Hd2dFacingDirection direction)
        {
            if (clips.TryGetValue(ClipKey(action, direction), out RuntimeClip exact))
            {
                return exact;
            }
            if (clips.TryGetValue(ClipKey(action, Hd2dFacingDirection.Down), out RuntimeClip actionDown))
            {
                return actionDown;
            }
            if (clips.TryGetValue(ClipKey(CharacterAction.Idle, direction), out RuntimeClip idleDirection))
            {
                return idleDirection;
            }
            if (clips.TryGetValue(ClipKey(CharacterAction.Idle, Hd2dFacingDirection.Down), out RuntimeClip idleDown))
            {
                return idleDown;
            }
            if (fallbackSprite != null)
            {
                return new RuntimeClip(new[] { fallbackSprite }, 1f, true, true);
            }
            return null;
        }

        private void SetFrame(int index)
        {
            if (activeClip == null || activeClip.Frames.Length == 0)
            {
                return;
            }

            frameIndex = Mathf.Clamp(index, 0, activeClip.Frames.Length - 1);
            spriteRenderer.sprite = activeClip.Frames[frameIndex];
        }

        private bool TryBuildClipLibrary(
            out Dictionary<int, RuntimeClip> loadedClips,
            out string error)
        {
            loadedClips = new Dictionary<int, RuntimeClip>();
            error = string.Empty;
            var loadedStrips = new Dictionary<Hd2dSpriteStripDefinition, Sprite[]>();

            for (int clipIndex = 0; clipIndex < manifest.clips.Length; clipIndex++)
            {
                Hd2dSpriteClipDefinition definition = manifest.clips[clipIndex];
                if (definition == null ||
                    !Enum.TryParse(definition.action, true, out CharacterAction action) ||
                    !Enum.IsDefined(typeof(CharacterAction), action))
                {
                    error = $"clip {clipIndex} contains unknown action '{definition?.action}'";
                    return false;
                }

                // Every authored strip is loaded before any clip library is
                // committed. This catches a broken override even when another
                // direction could otherwise mask it through fallback.
                if (!TryLoadDeclaredStrip(action, "all", definition.all, loadedStrips, out error) ||
                    !TryLoadDeclaredStrip(action, "down", definition.down, loadedStrips, out error) ||
                    !TryLoadDeclaredStrip(action, "side", definition.side, loadedStrips, out error) ||
                    !TryLoadDeclaredStrip(action, "up", definition.up, loadedStrips, out error))
                {
                    return false;
                }

                if (manifest.directional)
                {
                    if (!TryBuildDirectionalClip(
                            action,
                            Hd2dFacingDirection.Down,
                            definition.down ?? definition.all,
                            definition,
                            loadedStrips,
                            loadedClips,
                            out error) ||
                        !TryBuildDirectionalClip(
                            action,
                            Hd2dFacingDirection.Side,
                            definition.side ?? definition.all,
                            definition,
                            loadedStrips,
                            loadedClips,
                            out error) ||
                        !TryBuildDirectionalClip(
                            action,
                            Hd2dFacingDirection.Up,
                            definition.up ?? definition.all,
                            definition,
                            loadedStrips,
                            loadedClips,
                            out error))
                    {
                        return false;
                    }
                }
                else
                {
                    Hd2dSpriteStripDefinition effectiveStrip = definition.all ??
                        definition.down ?? definition.side ?? definition.up;
                    if (!TryBuildDirectionalClip(
                        action,
                        Hd2dFacingDirection.Down,
                        effectiveStrip,
                        definition,
                        loadedStrips,
                        loadedClips,
                        out error))
                    {
                        return false;
                    }
                }
            }

            if (!TryValidateLoadedCoverage(loadedClips, out error))
            {
                return false;
            }

            return true;
        }

        private bool TryLoadDeclaredStrip(
            CharacterAction action,
            string directionLabel,
            Hd2dSpriteStripDefinition strip,
            Dictionary<Hd2dSpriteStripDefinition, Sprite[]> loadedStrips,
            out string error)
        {
            error = string.Empty;
            if (strip == null || loadedStrips.ContainsKey(strip))
            {
                return true;
            }

            Sprite[] frames = SliceStrip(strip);
            if (!AreFramesComplete(frames, strip.ResolvedFrameCount))
            {
                error = $"action '{action}' {directionLabel} strip did not load every declared frame";
                return false;
            }

            loadedStrips.Add(strip, frames);
            return true;
        }

        private bool TryBuildDirectionalClip(
            CharacterAction action,
            Hd2dFacingDirection direction,
            Hd2dSpriteStripDefinition strip,
            Hd2dSpriteClipDefinition definition,
            Dictionary<Hd2dSpriteStripDefinition, Sprite[]> loadedStrips,
            Dictionary<int, RuntimeClip> loadedClips,
            out string error)
        {
            if (strip == null || !loadedStrips.TryGetValue(strip, out Sprite[] frames) ||
                !AreFramesComplete(frames, strip.ResolvedFrameCount))
            {
                error = $"action '{action}' has no loadable {direction} strip";
                return false;
            }

            loadedClips[ClipKey(action, direction)] = new RuntimeClip(
                frames,
                definition.framesPerSecond,
                definition.loop,
                definition.holdLastFrame);
            error = string.Empty;
            return true;
        }

        private bool TryValidateLoadedCoverage(
            Dictionary<int, RuntimeClip> loadedClips,
            out string error)
        {
            for (int requiredIndex = 0; requiredIndex < manifest.requiredActions.Length; requiredIndex++)
            {
                if (!Enum.TryParse(
                        manifest.requiredActions[requiredIndex],
                        true,
                        out CharacterAction required) ||
                    !Enum.IsDefined(typeof(CharacterAction), required))
                {
                    error = $"required action '{manifest.requiredActions[requiredIndex]}' is invalid";
                    return false;
                }

                if (!HasLoadedClip(loadedClips, required, Hd2dFacingDirection.Down) ||
                    (manifest.directional &&
                        (!HasLoadedClip(loadedClips, required, Hd2dFacingDirection.Side) ||
                         !HasLoadedClip(loadedClips, required, Hd2dFacingDirection.Up))))
                {
                    error = $"required action '{required}' is missing a runtime direction";
                    return false;
                }
            }

            error = string.Empty;
            return loadedClips.Count > 0;
        }

        private static bool HasLoadedClip(
            Dictionary<int, RuntimeClip> loadedClips,
            CharacterAction action,
            Hd2dFacingDirection direction)
        {
            return loadedClips.TryGetValue(ClipKey(action, direction), out RuntimeClip clip) &&
                clip != null && AreFramesComplete(clip.Frames, clip.Frames.Length);
        }

        private static bool AreFramesComplete(Sprite[] frames, int expectedCount)
        {
            if (frames == null || frames.Length == 0 || frames.Length != expectedCount)
            {
                return false;
            }

            for (int frame = 0; frame < frames.Length; frame++)
            {
                if (frames[frame] == null)
                {
                    return false;
                }
            }
            return true;
        }

        private Sprite[] SliceStrip(Hd2dSpriteStripDefinition strip)
        {
            if (strip == null)
            {
                return null;
            }

            if (strip.resourcePaths != null && strip.resourcePaths.Length > 0)
            {
                return SliceIndividualFrames(strip);
            }
            if (string.IsNullOrWhiteSpace(strip.resourcePath))
            {
                return null;
            }

            string resourcePath = NormalizeResourcePath(strip.resourcePath);
            Texture2D texture = Hd2dRuntimeSpriteCache.LoadTexture(resourcePath);

            if (texture == null)
            {
                Debug.LogWarning($"HD-2D sheet Resources/{resourcePath}.png was not found.", this);
                return null;
            }

            int columns = Mathf.Max(1, strip.columns);
            int rows = Mathf.Max(1, strip.rows);
            if (texture.width % columns != 0 || texture.height % rows != 0)
            {
                Debug.LogWarning(
                    $"HD-2D sheet '{resourcePath}' ({texture.width}x{texture.height}) is not divisible by " +
                    $"its {columns}x{rows} grid.",
                    this);
                return null;
            }

            int row = strip.rowFromTop;
            if (row < 0 || row >= rows)
            {
                Debug.LogWarning($"HD-2D sheet '{resourcePath}' has invalid row {row}.", this);
                return null;
            }

            int cellWidth = texture.width / columns;
            int cellHeight = texture.height / rows;
            float pixelsPerUnit = strip.pixelsPerUnit > 1f
                ? strip.pixelsPerUnit
                : manifest.pixelsPerUnit;
            ResolveStripPivot(strip, out float pivotX, out float pivotY);
            int frameCount = strip.ResolvedFrameCount;
            var frames = new Sprite[frameCount];
            for (int frame = 0; frame < frameCount; frame++)
            {
                int column = strip.GetColumn(frame);
                if (column < 0 || column >= columns)
                {
                    Debug.LogWarning(
                        $"HD-2D sheet '{resourcePath}' has invalid column {column} in row {row}.",
                        this);
                    return null;
                }

                string sliceKey = $"{resourcePath}|{columns}x{rows}|{row}:{column}|" +
                    $"{pixelsPerUnit:0.###}|{pivotX:0.###},{pivotY:0.###}";
                if (!leasedSprites.TryGetValue(sliceKey, out Sprite sprite))
                {
                    var rectangle = new Rect(
                        column * cellWidth,
                        texture.height - (row + 1) * cellHeight,
                        cellWidth,
                        cellHeight);
                    sprite = Hd2dRuntimeSpriteCache.Acquire(
                        sliceKey,
                        resourcePath,
                        () =>
                        {
                            Sprite created = Sprite.Create(
                                texture,
                                rectangle,
                                new Vector2(pivotX, pivotY),
                                pixelsPerUnit,
                                1,
                                SpriteMeshType.FullRect);
                            created.name = $"{manifest.characterId}-{resourcePath.Replace('/', '-')}-{row}-{column}";
                            return created;
                        });
                    if (sprite != null)
                    {
                        leasedSprites[sliceKey] = sprite;
                    }
                }
                frames[frame] = sprite;
            }

            return frames;
        }

        private Sprite[] SliceIndividualFrames(Hd2dSpriteStripDefinition strip)
        {
            float pixelsPerUnit = strip.pixelsPerUnit > 1f
                ? strip.pixelsPerUnit
                : manifest.pixelsPerUnit;
            ResolveStripPivot(strip, out float pivotX, out float pivotY);
            var frames = new Sprite[strip.resourcePaths.Length];

            for (int frame = 0; frame < strip.resourcePaths.Length; frame++)
            {
                string resourcePath = NormalizeResourcePath(strip.resourcePaths[frame]);
                Texture2D texture = Hd2dRuntimeSpriteCache.LoadTexture(resourcePath);
                if (texture == null)
                {
                    Debug.LogWarning($"HD-2D frame Resources/{resourcePath}.png was not found.", this);
                    return null;
                }

                string sliceKey = $"{resourcePath}|full|{pixelsPerUnit:0.###}|{pivotX:0.###},{pivotY:0.###}";
                if (!leasedSprites.TryGetValue(sliceKey, out Sprite sprite))
                {
                    sprite = Hd2dRuntimeSpriteCache.Acquire(
                        sliceKey,
                        resourcePath,
                        () =>
                        {
                            Sprite created = Sprite.Create(
                                texture,
                                new Rect(0f, 0f, texture.width, texture.height),
                                new Vector2(pivotX, pivotY),
                                pixelsPerUnit,
                                1,
                                SpriteMeshType.FullRect);
                            created.name = $"{manifest.characterId}-{resourcePath.Replace('/', '-')}";
                            return created;
                        });
                    if (sprite != null)
                    {
                        leasedSprites[sliceKey] = sprite;
                    }
                }
                frames[frame] = sprite;
            }

            return frames;
        }

        private void ResolveStripPivot(
            Hd2dSpriteStripDefinition strip,
            out float pivotX,
            out float pivotY)
        {
            bool hasAuthoredOverride = strip.usePivotOverride ||
                !Mathf.Approximately(strip.pivotX, 0f) ||
                !Mathf.Approximately(strip.pivotY, 0f);
            if (!hasAuthoredOverride)
            {
                pivotX = manifest.pivotX;
                pivotY = manifest.pivotY;
                return;
            }

            pivotX = strip.pivotX >= 0f ? strip.pivotX : manifest.pivotX;
            pivotY = strip.pivotY >= 0f ? strip.pivotY : manifest.pivotY;
        }

        private Sprite LoadFallbackSprite(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            string normalizedPath = NormalizeResourcePath(resourcePath);
            Sprite importedSprite = Resources.Load<Sprite>(normalizedPath);
            if (importedSprite != null)
            {
                return importedSprite;
            }

            Texture2D texture = Hd2dRuntimeSpriteCache.LoadTexture(normalizedPath);
            if (texture == null)
            {
                return null;
            }

            string sliceKey = $"{normalizedPath}|full|{manifest.pixelsPerUnit:0.###}|" +
                $"{manifest.pivotX:0.###},{manifest.pivotY:0.###}";
            Sprite sprite = Hd2dRuntimeSpriteCache.Acquire(
                sliceKey,
                normalizedPath,
                () =>
                {
                    Sprite created = Sprite.Create(
                        texture,
                        new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(manifest.pivotX, manifest.pivotY),
                        manifest.pixelsPerUnit,
                        1,
                        SpriteMeshType.FullRect);
                    created.name = $"{manifest.characterId}-fallback";
                    return created;
                });
            if (sprite != null)
            {
                leasedSprites[sliceKey] = sprite;
            }
            return sprite;
        }

        private void EnsureRenderer()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (spriteRenderer == null)
            {
                var spriteObject = new GameObject("HD-2D Sprite");
                spriteObject.transform.SetParent(transform, false);
                spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            }
            spriteTransform = spriteRenderer.transform;
        }

        private void ConfigureSpriteRenderer()
        {
            spriteRenderer.drawMode = SpriteDrawMode.Simple;
            spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
            spriteRenderer.receiveShadows = false;
            spriteRenderer.lightProbeUsage = LightProbeUsage.Off;
            spriteRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void CreateBlobShadow()
        {
            if (manifest.shadowWidth <= 0f || manifest.shadowDepth <= 0f || manifest.shadowOpacity <= 0f)
            {
                return;
            }

            shadowObject = new GameObject($"{manifest.characterId} HD-2D shadow");
            shadowObject.transform.SetParent(actorRoot.parent, true);
            shadowObject.transform.position = new Vector3(
                actorRoot.position.x,
                groundWorldY + manifest.shadowYOffset,
                actorRoot.position.z);
            shadowObject.transform.localScale = new Vector3(manifest.shadowWidth, 1f, manifest.shadowDepth);

            MeshFilter meshFilter = shadowObject.AddComponent<MeshFilter>();
            shadowMesh = CreateDiscMesh(28);
            meshFilter.sharedMesh = shadowMesh;
            shadowRenderer = shadowObject.AddComponent<MeshRenderer>();
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;
            shadowRenderer.lightProbeUsage = LightProbeUsage.Off;
            shadowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            shadowMaterial = RuntimeMaterialFactory.CreateUnlit(
                $"{manifest.characterId} HD-2D shadow material",
                new Color(0.015f, 0.025f, 0.04f, manifest.shadowOpacity));
            ConfigureTransparentMaterial(shadowMaterial);
            shadowRenderer.sharedMaterial = shadowMaterial;
            UpdateShadow(true);
        }

        private static Mesh CreateDiscMesh(int segments)
        {
            int resolvedSegments = Mathf.Max(8, segments);
            var vertices = new Vector3[resolvedSegments + 1];
            var triangles = new int[resolvedSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < resolvedSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / resolvedSegments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f);

                int triangle = i * 3;
                triangles[triangle] = 0;
                triangles[triangle + 1] = (i + 1) % resolvedSegments + 1;
                triangles[triangle + 2] = i + 1;
            }

            var mesh = new Mesh { name = "HD-2D blob shadow disc" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }
            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void UpdateShadow(bool force)
        {
            if (shadowObject == null || shadowMaterial == null || manifest == null)
            {
                return;
            }

            float heightFactor = Mathf.Clamp01(airHeight / 2.4f);
            float shadowFactor = Mathf.Lerp(1f, 0.64f, heightFactor);
            if (!force && Mathf.Abs(lastShadowFactor - shadowFactor) < 0.01f)
            {
                return;
            }

            lastShadowFactor = shadowFactor;
            shadowObject.transform.localScale = new Vector3(
                manifest.shadowWidth * shadowFactor,
                1f,
                manifest.shadowDepth * shadowFactor);
            float alpha = manifest.shadowOpacity * Mathf.Lerp(1f, 0.25f, heightFactor);
            if (defeated)
            {
                alpha *= 0.45f;
            }
            if (shadowMaterial.HasProperty("_BaseColor"))
            {
                RuntimeMaterialFactory.SetSrgbColor(
                    shadowMaterial,
                    "_BaseColor",
                    new Color(0.015f, 0.025f, 0.04f, alpha));
            }
            else if (shadowMaterial.HasProperty("_Color"))
            {
                RuntimeMaterialFactory.SetSrgbColor(
                    shadowMaterial,
                    "_Color",
                    new Color(0.015f, 0.025f, 0.04f, alpha));
            }
        }

        private void ApplyActionPose()
        {
            float normalized = float.IsPositiveInfinity(actionDuration)
                ? Mathf.Repeat(actionElapsed * 0.65f, 1f)
                : actionDuration <= 0f ? 1f : Mathf.Clamp01(actionElapsed / actionDuration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            spriteTransform.localPosition = baseSpritePosition;
            spriteTransform.localRotation = Quaternion.identity;
            spriteTransform.localScale = Vector3.one;

            switch (activeState)
            {
                case CharacterAction.Sword:
                case CharacterAction.AirSlash:
                    spriteTransform.localRotation = Quaternion.Euler(0f, 0f, -5f * pulse);
                    break;
                case CharacterAction.SpinCharge:
                case CharacterAction.MagicCharge:
                    spriteTransform.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(actionElapsed * 24f));
                    break;
                case CharacterAction.SpinRelease:
                    spriteTransform.localRotation = Quaternion.Euler(0f, 0f, normalized * -360f);
                    spriteTransform.localScale = Vector3.one * (1f + 0.055f * pulse);
                    break;
                case CharacterAction.Hurt:
                    spriteTransform.localPosition = baseSpritePosition +
                        Vector3.right * (Mathf.Sin(normalized * Mathf.PI * 6f) * 0.035f * (1f - normalized));
                    break;
                case CharacterAction.Land:
                    spriteTransform.localScale = new Vector3(1f + 0.06f * pulse, 1f - 0.08f * pulse, 1f);
                    break;
                case CharacterAction.AttackWindup:
                    spriteTransform.localScale = new Vector3(1f + 0.05f * pulse, 1f - 0.07f * pulse, 1f);
                    break;
                case CharacterAction.Defeated:
                    float direction = spriteRenderer.flipX ? 1f : -1f;
                    spriteTransform.localRotation = Quaternion.Euler(0f, 0f, direction * normalized * 72f);
                    spriteTransform.localPosition = baseSpritePosition + Vector3.down * (0.08f * normalized);
                    UpdateShadow(true);
                    break;
            }
        }

        private void ApplyNeutralPose()
        {
            spriteTransform.localPosition = baseSpritePosition;
            spriteTransform.localRotation = Quaternion.identity;
            spriteTransform.localScale = Vector3.one;
        }

        private void ApplyLocomotionPose()
        {
            ApplyNeutralPose();
            if (activeState == CharacterAction.Idle)
            {
                float breath = Mathf.Sin(Time.time * 2.4f) * 0.004f;
                spriteTransform.localScale = new Vector3(1f - breath, 1f + breath, 1f);
                return;
            }

            if (activeState != CharacterAction.Walk && activeState != CharacterAction.Run)
            {
                return;
            }

            float frequency = activeState == CharacterAction.Run ? 13f : 8f;
            float amplitude = activeState == CharacterAction.Run ? 0.025f : 0.014f;
            float stride = Mathf.Abs(Mathf.Sin(Time.time * frequency));
            spriteTransform.localPosition = baseSpritePosition + Vector3.up * (stride * amplitude);
            spriteTransform.localScale = new Vector3(
                1f + stride * amplitude * 0.4f,
                1f - stride * amplitude * 0.25f,
                1f);
        }

        private void ApplyDisplayColor()
        {
            Color displayColor = requestedTint;
            if (defeated)
            {
                float normalized = float.IsPositiveInfinity(actionDuration)
                    ? 0f
                    : actionDuration <= 0f ? 1f : Mathf.Clamp01(actionElapsed / actionDuration);
                displayColor.a *= Mathf.Lerp(1f, 0.48f, normalized);
            }
            spriteRenderer.color = displayColor;
        }

        private static int GetActionPriority(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Defeated:
                    return 100;
                case CharacterAction.Hurt:
                    return 90;
                case CharacterAction.Sword:
                case CharacterAction.AirSlash:
                case CharacterAction.Plunge:
                case CharacterAction.SpinCharge:
                case CharacterAction.SpinRelease:
                case CharacterAction.MagicCharge:
                case CharacterAction.MagicRelease:
                case CharacterAction.AttackWindup:
                case CharacterAction.Attack:
                    return 60;
                case CharacterAction.Jump:
                case CharacterAction.Fall:
                case CharacterAction.Land:
                    return 20;
                default:
                    return 0;
            }
        }

        private static int ClipKey(CharacterAction action, Hd2dFacingDirection direction)
        {
            return ((int)action * 4) + (int)direction;
        }

        private static string NormalizeResourcePath(string path)
        {
            string normalized = (path ?? string.Empty).Trim().Replace('\\', '/');
            int resourcesMarker = normalized.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
            if (resourcesMarker >= 0)
            {
                normalized = normalized.Substring(resourcesMarker + "/Resources/".Length);
            }
            if (normalized.StartsWith("Resources/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Resources/".Length);
            }
            int extension = normalized.LastIndexOf('.');
            if (extension > normalized.LastIndexOf('/'))
            {
                normalized = normalized.Substring(0, extension);
            }
            return normalized;
        }

        private void OnDestroy()
        {
            ReleaseLeasedSprites();

            if (shadowObject != null)
            {
                Destroy(shadowObject);
            }
            if (shadowMesh != null)
            {
                Destroy(shadowMesh);
            }
            if (shadowMaterial != null)
            {
                Destroy(shadowMaterial);
            }
        }

        private void ReleaseLeasedSprites()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = null;
            }

            foreach (string sliceKey in leasedSprites.Keys)
            {
                Hd2dRuntimeSpriteCache.Release(sliceKey);
            }
            leasedSprites.Clear();
            Hd2dRuntimeSpriteCache.ReleaseUnleasedTextures();
        }
    }
}
