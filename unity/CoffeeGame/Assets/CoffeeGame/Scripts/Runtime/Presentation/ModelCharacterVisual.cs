using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum CharacterModelStyle
    {
        Imported,
        Heroine,
        Slime
    }

    [DisallowMultipleComponent]
    public sealed class ModelCharacterVisual : MonoBehaviour, ICharacterVisual
    {
        private const string LocomotionSpeedParameter = "LocomotionSpeed";
        private const string TrialAlbedoResource = "Models/Hero/Meshy_AI_Azure_Blade_Maiden_biped_texture_0";
        private const string TrialNormalResource = "Models/Hero/Meshy_AI_Azure_Blade_Maiden_biped_texture_0_normal";
        public const string TrialHeldSwordAlbedoResource = "Models/Hero/Meshy_AI_Blue_Haired_Ronin_biped_texture_0";
        public const string TrialHeldSwordNormalResource = "Models/Hero/Meshy_AI_Blue_Haired_Ronin_biped_texture_0_normal";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int LocomotionSpeedId = Animator.StringToHash(LocomotionSpeedParameter);

        [Serializable]
        public struct CharacterStateName
        {
            public CharacterAction Action;
            public string StateName;
        }

        [SerializeField] private Transform modelRoot;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform heldSwordRoot;
        [SerializeField] private Animator heldSwordAnimator;
        [SerializeField] private float modelScale = 1f;
        [SerializeField] private float facingYawOffset;
        [SerializeField] private CharacterModelStyle modelStyle = CharacterModelStyle.Imported;
        [SerializeField, Range(0f, 180f)] private float maxAngleFromCamera = 180f;
        [SerializeField] private float locomotionCrossFadeSeconds = 0.08f;
        [SerializeField] private float actionCrossFadeSeconds = 0.035f;
        [SerializeField] private CharacterStateName[] stateNameOverrides = Array.Empty<CharacterStateName>();

        private readonly Dictionary<CharacterAction, string> stateNames = new Dictionary<CharacterAction, string>();
        private readonly List<TintTarget> tintTargets = new List<TintTarget>();
        private readonly List<Material> ownedDisplayMaterials = new List<Material>();
        private MaterialPropertyBlock propertyBlock;

        private Coroutine actionRoutine;
        private CharacterAction locomotion = CharacterAction.Idle;
        private CharacterAction currentState = CharacterAction.Idle;
        private Vector3 baseLocalScale = Vector3.one;
        private Quaternion baseLocalRotation = Quaternion.identity;
        private Camera facingCamera;
        private float locomotionPlaybackSpeed = 1f;
        private float groundWorldY;
        private float airHeight;
        private float previousAirHeight;
        private float airHeightTrend;
        private bool hasLocomotionSpeedParameter;
        private bool actionPlaying;
        private bool defeated;
        private Transform locomotionRoot;
        private Animator locomotionAnimator;
        private bool showingHeldSwordSet;

        public Animator Animator => animator;
        public Transform ModelRoot => modelRoot;

        public static string GetDefaultStateName(CharacterAction action)
        {
            return action.ToString();
        }

        public static bool UsesHeldSwordSet(CharacterAction action)
        {
            return action == CharacterAction.Sword ||
                action == CharacterAction.AirSlash ||
                action == CharacterAction.Plunge ||
                action == CharacterAction.SpinRelease;
        }

        public void Initialize(
            Transform instantiatedModel,
            RuntimeAnimatorController runtimeController,
            float scale = 1f,
            float yawOffset = 0f)
        {
            Initialize(
                instantiatedModel,
                runtimeController,
                CharacterModelStyle.Imported,
                null,
                scale,
                yawOffset,
                180f);
        }

        public void Initialize(
            Transform instantiatedModel,
            RuntimeAnimatorController runtimeController,
            CharacterModelStyle style,
            Camera camera,
            float scale = 1f,
            float yawOffset = 0f,
            float maximumAngleFromCamera = 180f)
        {
            modelRoot = instantiatedModel != null ? instantiatedModel : transform;
            propertyBlock = new MaterialPropertyBlock();
            modelScale = Mathf.Max(0.01f, scale);
            facingYawOffset = yawOffset;
            modelStyle = style;
            facingCamera = camera;
            maxAngleFromCamera = Mathf.Clamp(maximumAngleFromCamera, 0f, 180f);
            baseLocalRotation = transform.localRotation;
            baseLocalScale = transform.localScale * modelScale;
            transform.localScale = baseLocalScale;
            Transform actorRoot = transform.parent != null ? transform.parent : transform;
            groundWorldY = actorRoot.position.y;

            animator = modelRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                animator = modelRoot.gameObject.AddComponent<Animator>();
            }

            if (runtimeController != null)
            {
                animator.runtimeAnimatorController = runtimeController;
            }
            animator.applyRootMotion = false;
            locomotionRoot = modelRoot;
            locomotionAnimator = animator;
            showingHeldSwordSet = false;

            BuildStateNameLookup();
            ApplyReferenceMaterials();
            ApplyImportedTexturesIfMissing(
                modelRoot,
                TrialAlbedoResource,
                TrialNormalResource,
                "trial-anime-girl display");
            CacheTintTargets();
            CacheAnimatorParameters();
            ResetState(Vector3.back);
            GroundImportedModel(modelRoot);
        }

        public void AttachHeldSwordSet(
            GameObject attackPrefab,
            RuntimeAnimatorController attackController,
            string albedoResource,
            string normalResource)
        {
            if (attackPrefab == null || attackController == null)
            {
                return;
            }

            GameObject attackInstance = Instantiate(attackPrefab, transform, false);
            attackInstance.name = $"{attackPrefab.name} AttackSet";
            attackInstance.transform.localPosition = locomotionRoot != null
                ? locomotionRoot.localPosition
                : Vector3.zero;
            attackInstance.transform.localRotation = Quaternion.identity;
            attackInstance.transform.localScale = Vector3.one;

            Collider[] importedColliders = attackInstance.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < importedColliders.Length; i++)
            {
                importedColliders[i].enabled = false;
            }

            heldSwordRoot = attackInstance.transform;
            heldSwordAnimator = heldSwordRoot.GetComponentInChildren<Animator>(true);
            if (heldSwordAnimator == null)
            {
                heldSwordAnimator = attackInstance.AddComponent<Animator>();
            }

            heldSwordAnimator.runtimeAnimatorController = attackController;
            heldSwordAnimator.applyRootMotion = false;
            ApplyImportedTexturesIfMissing(
                heldSwordRoot,
                string.IsNullOrWhiteSpace(albedoResource) ? TrialHeldSwordAlbedoResource : albedoResource,
                string.IsNullOrWhiteSpace(normalResource) ? TrialHeldSwordNormalResource : normalResource,
                "trial-anime-girl-attack display");
            GroundImportedModel(heldSwordRoot);
            MatchHeldSwordHeight(locomotionRoot, heldSwordRoot);
            GroundImportedModel(heldSwordRoot);
            CacheTintTargets();
            SetHeldSwordVisible(false);
        }

        private static void MatchHeldSwordHeight(Transform locomotion, Transform attack)
        {
            if (locomotion == null || attack == null)
            {
                return;
            }

            Transform locoHead = FindNamedChild(locomotion, "Head");
            Transform attackHead = FindNamedChild(attack, "Head");
            if (locoHead == null || attackHead == null || attackHead.position.y < 0.2f)
            {
                return;
            }

            float scale = locoHead.position.y / attackHead.position.y;
            if (scale > 0.8f && scale < 1.25f)
            {
                attack.localScale *= scale;
            }
        }

        private static Transform FindNamedChild(Transform root, string boneName)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (string.Equals(children[i].name, boneName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return children[i];
                }
            }

            return null;
        }

        private void GroundImportedModel(Transform root)
        {
            if (modelStyle != CharacterModelStyle.Imported || root == null)
            {
                return;
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = true;
                }
            }

            Animator sourceAnimator = root.GetComponentInChildren<Animator>(true);
            if (sourceAnimator != null)
            {
                sourceAnimator.Update(0f);
            }

            float minFootY = float.PositiveInfinity;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string boneName = transforms[i].name;
                if (boneName.IndexOf("Foot", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    boneName.IndexOf("Toe", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                minFootY = Mathf.Min(minFootY, transforms[i].position.y);
            }

            if (float.IsPositiveInfinity(minFootY))
            {
                return;
            }

            float lift = -minFootY;
            if (Mathf.Abs(lift) > 0.001f && Mathf.Abs(lift) < 5f)
            {
                root.position += new Vector3(0f, lift, 0f);
            }

            Debug.Log(
                $"CoffeeGAME trial foot ground ({root.name}): minFootY={minFootY:0.000} lift={lift:0.000}",
                this);
        }

        public void ResetState(Vector3 worldDirection)
        {
            StopActionRoutine();
            defeated = false;
            actionPlaying = false;
            SetHeldSwordVisible(false);
            locomotion = CharacterAction.Idle;
            currentState = CharacterAction.Idle;
            locomotionPlaybackSpeed = 1f;
            Transform actorRoot = transform.parent != null ? transform.parent : transform;
            groundWorldY = actorRoot.position.y;
            airHeight = 0f;
            previousAirHeight = 0f;
            airHeightTrend = 0f;
            transform.localScale = baseLocalScale;
            transform.localRotation = baseLocalRotation;

            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.speed = 1f;
                if (animator.isActiveAndEnabled)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
                TryPlayState(CharacterAction.Idle, 0f);
            }

            SetTint(Color.white);
            SetFacing(worldDirection);
        }

        public void SetFacing(Vector3 worldDirection)
        {
            Vector3 planarDirection = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (planarDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (maxAngleFromCamera < 180f && facingCamera != null)
            {
                Vector3 towardCamera = Vector3.ProjectOnPlane(
                    facingCamera.transform.position - transform.position,
                    Vector3.up);
                if (towardCamera.sqrMagnitude > 0.0001f)
                {
                    planarDirection = Vector3.RotateTowards(
                        towardCamera.normalized,
                        planarDirection.normalized,
                        maxAngleFromCamera * Mathf.Deg2Rad,
                        0f);
                }
            }

            Quaternion worldFacing = Quaternion.LookRotation(planarDirection.normalized, Vector3.up);
            Quaternion localFacing = transform.parent == null
                ? worldFacing
                : Quaternion.Inverse(transform.parent.rotation) * worldFacing;
            transform.localRotation = localFacing * Quaternion.Euler(0f, facingYawOffset, 0f) * baseLocalRotation;
        }

        public void SetLocomotion(CharacterAction action, float normalizedSpeed)
        {
            if (action != CharacterAction.Idle && action != CharacterAction.Walk && action != CharacterAction.Run)
            {
                action = normalizedSpeed > 0.55f ? CharacterAction.Run :
                    normalizedSpeed > 0.01f ? CharacterAction.Walk : CharacterAction.Idle;
            }

            locomotion = action;
            locomotionPlaybackSpeed = action == CharacterAction.Idle
                ? 1f
                : Mathf.Lerp(0.78f, 1.22f, Mathf.Clamp01(normalizedSpeed));

            if (animator != null && hasLocomotionSpeedParameter)
            {
                animator.SetFloat(LocomotionSpeedId, Mathf.Clamp01(normalizedSpeed));
            }

            if (actionPlaying || defeated)
            {
                return;
            }

            if (animator != null)
            {
                animator.speed = locomotionPlaybackSpeed;
            }
            if (currentState != locomotion)
            {
                TryPlayState(locomotion, locomotionCrossFadeSeconds);
            }
        }

        public void PlayAction(CharacterAction action, float duration)
        {
            if (defeated && action != CharacterAction.Defeated)
            {
                return;
            }
            if (actionPlaying &&
                GetActionPriority(action) < GetActionPriority(currentState) &&
                !CharacterVisualTransitionPolicy.IsForcedPhysicsTransition(currentState, action))
            {
                return;
            }

            StopActionRoutine();
            actionPlaying = true;
            defeated = action == CharacterAction.Defeated;

            if (animator != null)
            {
                animator.speed = 1f;
            }
            float playCrossFade = action == CharacterAction.Dodge ? 0f : actionCrossFadeSeconds;
            TryPlayState(action, playCrossFade);
            MatchClipPlaybackToDuration(action, Mathf.Max(0.05f, duration));
            actionRoutine = StartCoroutine(FinishActionAfter(action, Mathf.Max(0.05f, duration)));
        }

        private void MatchClipPlaybackToDuration(CharacterAction action, float duration)
        {
            if (animator == null || !animator.isActiveAndEnabled || float.IsInfinity(duration))
            {
                return;
            }

            if (!showingHeldSwordSet &&
                action != CharacterAction.MagicCharge &&
                action != CharacterAction.MagicRelease &&
                action != CharacterAction.Dodge)
            {
                return;
            }

            animator.Update(0f);
            float length = 0f;
            AnimatorClipInfo[] clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            if (clipInfo != null && clipInfo.Length > 0 && clipInfo[0].clip != null)
            {
                length = clipInfo[0].clip.length;
            }

            if (length < 0.08f)
            {
                length = animator.GetCurrentAnimatorStateInfo(0).length;
            }

            if (length < 0.08f)
            {
                return;
            }

            animator.speed = length / duration;
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
                case CharacterAction.Dodge:
                case CharacterAction.Fall:
                case CharacterAction.Land:
                    return 20;
                default:
                    return 0;
            }
        }

        public void SetAirHeight(float height)
        {
            // The actor root still owns Y movement. These values only let a
            // finite combat/hurt animation return to the physics-driven air pose.
            previousAirHeight = airHeight;
            airHeight = Mathf.Max(0f, height - groundWorldY);
            airHeightTrend = airHeight - previousAirHeight;
        }

        public void SetTint(Color color)
        {
            bool clearTint = IsWhite(color);
            Color shaderTint = RuntimeMaterialFactory.ToShaderColor(color);
            for (int i = 0; i < tintTargets.Count; i++)
            {
                TintTarget target = tintTargets[i];
                if (target.Renderer == null)
                {
                    continue;
                }

                if (clearTint)
                {
                    // Returning to the material value avoids feeding a linear
                    // GetColor result through SetColor's sRGB conversion again.
                    // It also guarantees that an interrupted hurt/wind-up tint
                    // cannot remain on a pooled or reset actor.
                    target.Renderer.SetPropertyBlock(null, target.MaterialIndex);
                    continue;
                }

                propertyBlock.Clear();
                target.Renderer.GetPropertyBlock(propertyBlock, target.MaterialIndex);
                if (target.HasBaseColor)
                {
                    propertyBlock.SetVector(BaseColorId, Multiply(target.BaseColor, shaderTint));
                }
                if (target.HasColor)
                {
                    propertyBlock.SetVector(ColorId, Multiply(target.Color, shaderTint));
                }
                target.Renderer.SetPropertyBlock(propertyBlock, target.MaterialIndex);
            }
        }

        private IEnumerator FinishActionAfter(CharacterAction action, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            actionRoutine = null;
            if (action == CharacterAction.Defeated)
            {
                actionPlaying = false;
                yield break;
            }

            if (action == CharacterAction.Dodge)
            {
                yield break;
            }

            actionPlaying = false;
            SetTint(Color.white);
            if (animator != null)
            {
                animator.speed = locomotionPlaybackSpeed;
            }

            // Match the HD-2D backend: a finite AirSlash/Hurt (or another
            // combat override) must not flash to grounded locomotion in mid-air.
            if (airHeight > 0.06f &&
                action != CharacterAction.Jump &&
                action != CharacterAction.Fall &&
                action != CharacterAction.Plunge &&
                action != CharacterAction.Land)
            {
                actionPlaying = true;
                CharacterAction airState = airHeightTrend >= -0.0001f
                    ? CharacterAction.Jump
                    : CharacterAction.Fall;
                TryPlayState(airState, actionCrossFadeSeconds);
                yield break;
            }
            TryPlayState(locomotion, locomotionCrossFadeSeconds);
        }

        private void StopActionRoutine()
        {
            if (actionRoutine == null)
            {
                return;
            }

            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        private bool TryPlayState(CharacterAction action, float crossFadeSeconds)
        {
            bool swappedSets = EnsureSetForAction(action);
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
            {
                currentState = action;
                return false;
            }

            if (swappedSets)
            {
                crossFadeSeconds = 0f;
            }

            string stateName = ResolveStateName(action);
            string layerName = animator.layerCount > 0 ? animator.GetLayerName(0) : "Base Layer";
            int fullPathHash = Animator.StringToHash($"{layerName}.{stateName}");
            int shortNameHash = Animator.StringToHash(stateName);
            int stateHash = animator.HasState(0, fullPathHash)
                ? fullPathHash
                : animator.HasState(0, shortNameHash) ? shortNameHash : 0;
            if (stateHash == 0)
            {
                return false;
            }

            if (crossFadeSeconds <= 0f)
            {
                animator.Play(stateHash, 0, 0f);
            }
            else
            {
                animator.CrossFade(stateHash, crossFadeSeconds, 0, 0f);
            }
            currentState = action;
            return true;
        }

        private void BuildStateNameLookup()
        {
            stateNames.Clear();
            Array actions = Enum.GetValues(typeof(CharacterAction));
            foreach (CharacterAction action in actions)
            {
                stateNames[action] = GetDefaultStateName(action);
            }

            if (stateNameOverrides == null)
            {
                return;
            }

            for (int i = 0; i < stateNameOverrides.Length; i++)
            {
                CharacterStateName mapping = stateNameOverrides[i];
                if (!string.IsNullOrWhiteSpace(mapping.StateName))
                {
                    stateNames[mapping.Action] = mapping.StateName.Trim();
                }
            }
        }

        private string ResolveStateName(CharacterAction action)
        {
            return stateNames.TryGetValue(action, out string stateName)
                ? stateName
                : GetDefaultStateName(action);
        }

        private void CacheAnimatorParameters()
        {
            hasLocomotionSpeedParameter = false;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == LocomotionSpeedId && parameters[i].type == AnimatorControllerParameterType.Float)
                {
                    hasLocomotionSpeedParameter = true;
                    return;
                }
            }
        }

        private void CacheTintTargets()
        {
            tintTargets.Clear();
            Transform searchRoot = heldSwordRoot != null ? transform : modelRoot;
            if (searchRoot == null)
            {
                return;
            }

            Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    bool hasBaseColor = material.HasProperty(BaseColorId);
                    bool hasColor = material.HasProperty(ColorId);
                    if (!hasBaseColor && !hasColor)
                    {
                        continue;
                    }

                    tintTargets.Add(new TintTarget(
                        renderer,
                        materialIndex,
                        hasBaseColor,
                        hasBaseColor ? material.GetColor(BaseColorId) : Color.white,
                        hasColor,
                        hasColor ? material.GetColor(ColorId) : Color.white));
                }
            }
        }

        private bool EnsureSetForAction(CharacterAction action)
        {
            bool wantHeldSword = heldSwordAnimator != null && UsesHeldSwordSet(action);
            if (wantHeldSword == showingHeldSwordSet)
            {
                animator = wantHeldSword ? heldSwordAnimator : locomotionAnimator ?? animator;
                return false;
            }

            SetHeldSwordVisible(wantHeldSword);
            return true;
        }

        private void SetHeldSwordVisible(bool visible)
        {
            showingHeldSwordSet = visible && heldSwordRoot != null;
            if (locomotionRoot != null)
            {
                locomotionRoot.gameObject.SetActive(!showingHeldSwordSet);
            }

            if (heldSwordRoot != null)
            {
                heldSwordRoot.gameObject.SetActive(showingHeldSwordSet);
            }

            animator = showingHeldSwordSet ? heldSwordAnimator : locomotionAnimator;
            CacheAnimatorParameters();
        }

        private void ApplyImportedTexturesIfMissing(
            Transform root,
            string albedoResource,
            string normalResource,
            string materialName)
        {
            if (modelStyle != CharacterModelStyle.Imported || root == null)
            {
                return;
            }

            Texture2D albedo = Resources.Load<Texture2D>(albedoResource);
            if (albedo == null)
            {
                Debug.LogWarning(
                    $"CoffeeGAME trial texture: missing Resources/{albedoResource}.",
                    this);
                return;
            }

            Texture2D normal = Resources.Load<Texture2D>(normalResource);
            Material display = RuntimeMaterialFactory.CreateUnlit(materialName, Color.white);
            if (display == null)
            {
                display = RuntimeMaterialFactory.CreateLit(materialName, Color.white);
            }
            if (display == null)
            {
                Debug.LogWarning("CoffeeGAME trial texture: could not create a display material.", this);
                return;
            }

            if (display.HasProperty(BaseMapId))
            {
                display.SetTexture(BaseMapId, albedo);
            }
            if (display.HasProperty("_MainTex"))
            {
                display.SetTexture("_MainTex", albedo);
            }

            if (normal != null && display.HasProperty(BumpMapId))
            {
                display.SetTexture(BumpMapId, normal);
                display.EnableKeyword("_NORMALMAP");
            }

            if (display.HasProperty("_Smoothness"))
            {
                display.SetFloat("_Smoothness", 0.18f);
            }
            if (display.HasProperty("_Metallic"))
            {
                display.SetFloat("_Metallic", 0.02f);
            }

            ownedDisplayMaterials.Add(display);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            int assigned = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                if (materials == null || materials.Length == 0)
                {
                    renderers[i].sharedMaterial = display;
                    assigned++;
                    continue;
                }

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    materials[materialIndex] = display;
                }
                renderers[i].sharedMaterials = materials;
                assigned++;
            }

            Debug.Log(
                $"CoffeeGAME trial texture: albedo {albedo.width}x{albedo.height} assigned to {assigned} renderers on {root.name}.",
                this);
        }

        private void ApplyReferenceMaterials()
        {
            if (modelStyle == CharacterModelStyle.Imported || modelRoot == null)
            {
                return;
            }

            var replacements = new Dictionary<Material, Material>();
            Renderer[] renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material imported = materials[materialIndex];
                    if (imported == null)
                    {
                        continue;
                    }

                    if (!replacements.TryGetValue(imported, out Material replacement))
                    {
                        Color importedColor = imported.HasProperty(BaseColorId)
                            ? imported.GetColor(BaseColorId)
                            : imported.HasProperty(ColorId) ? imported.GetColor(ColorId) : Color.white;
                        Color displayColor = ResolveReferenceColor(imported.name, importedColor);
                        replacement = RuntimeMaterialFactory.CreateLit(
                            $"{imported.name} CoffeeGAME display",
                            displayColor);
                        if (replacement == null)
                        {
                            continue;
                        }

                        CopyDisplayTextures(imported, replacement);
                        ConfigureDisplayMaterial(replacement, imported.name, displayColor);
                        replacements.Add(imported, replacement);
                        ownedDisplayMaterials.Add(replacement);
                    }

                    materials[materialIndex] = replacement;
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void ConfigureDisplayMaterial(Material material, string sourceName, Color displayColor)
        {
            string name = (sourceName ?? string.Empty).Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            float smoothness = 0.24f;
            float metallic = 0f;
            bool disableSpecularHighlights = false;
            bool disableEnvironmentReflections = false;

            if (name.Contains("hair"))
            {
                smoothness = 0.2f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
            }
            if (name.Contains("skin"))
            {
                smoothness = 0.1f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
            }
            if (name.Contains("haori") || name.Contains("skirt") || name.Contains("textile") || name.Contains("top"))
            {
                smoothness = 0.08f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
            }
            if (name.Contains("obi") || name.Contains("glove") || name.Contains("boot") ||
                name.Contains("rubber") || name.Contains("wrap") || name.Contains("ink"))
            {
                smoothness = 0.1f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
            }
            if (name.Contains("eye") || name.Contains("iris"))
            {
                smoothness = 0.22f;
                disableEnvironmentReflections = true;
            }
            if (name.Contains("steel") || name.Contains("blade") || name.Contains("fitting"))
            {
                smoothness = 0.7f;
                metallic = 0.78f;
            }

            bool slimeBody = modelStyle == CharacterModelStyle.Slime &&
                (name.Contains("cyan") || name.Contains("gel") || name.Contains("slimebody") ||
                 (name.Contains("slime") && name.Contains("body")));
            bool slimeSpark = modelStyle == CharacterModelStyle.Slime &&
                (name.Contains("spark") || name.Contains("highlight"));
            bool slimeAmberEye = modelStyle == CharacterModelStyle.Slime &&
                !name.Contains("core") && !name.Contains("pupil") &&
                (name.Contains("amber") || name.Contains("eye"));

            if (slimeBody)
            {
                // Keep the Android-safe opaque surface, but remove the broad
                // white URP reflections that made the cyan body read as silver.
                smoothness = 0.3f;
                metallic = 0f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
                SetEmission(material, ScaleRgb(displayColor, 0.12f));
            }
            else if (slimeSpark)
            {
                smoothness = 0.12f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
                SetEmission(material, ScaleRgb(displayColor, 0.65f));
            }
            else if (slimeAmberEye)
            {
                smoothness = 0.18f;
                disableSpecularHighlights = true;
                disableEnvironmentReflections = true;
                SetEmission(material, ScaleRgb(displayColor, 0.1f));
            }
            else if (modelStyle == CharacterModelStyle.Heroine &&
                     (name.Contains("iris") || name.Contains("eyeamber") || name.Contains("goldhighlight")))
            {
                SetEmission(material, ScaleRgb(displayColor, 0.08f));
            }

            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            ConfigureToggleOff(
                material,
                "_SpecularHighlights",
                "_SPECULARHIGHLIGHTS_OFF",
                disableSpecularHighlights);
            ConfigureToggleOff(
                material,
                "_EnvironmentReflections",
                "_ENVIRONMENTREFLECTIONS_OFF",
                disableEnvironmentReflections);
        }

        private static void ConfigureToggleOff(
            Material material,
            string propertyName,
            string disabledKeyword,
            bool disabled)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, disabled ? 0f : 1f);
            }
            if (disabled)
            {
                material.EnableKeyword(disabledKeyword);
            }
            else
            {
                material.DisableKeyword(disabledKeyword);
            }
        }

        private static void SetEmission(Material material, Color srgbColor)
        {
            if (!material.HasProperty("_EmissionColor"))
            {
                return;
            }

            RuntimeMaterialFactory.SetSrgbColor(material, "_EmissionColor", srgbColor);
            material.EnableKeyword("_EMISSION");
        }

        private static Color ScaleRgb(Color color, float scale)
        {
            return new Color(color.r * scale, color.g * scale, color.b * scale, 1f);
        }

        private static void CopyDisplayTextures(Material source, Material destination)
        {
            if (!CopyTexture(source, destination, "_BaseMap", "_BaseMap"))
            {
                CopyTexture(source, destination, "_MainTex", "_BaseMap");
            }
            CopyTexture(source, destination, "_BumpMap", "_BumpMap");
            if (CopyTexture(source, destination, "_EmissionMap", "_EmissionMap"))
            {
                destination.EnableKeyword("_EMISSION");
            }
        }

        private static bool CopyTexture(
            Material source,
            Material destination,
            string sourceProperty,
            string destinationProperty)
        {
            if (!source.HasProperty(sourceProperty) || !destination.HasProperty(destinationProperty))
            {
                return false;
            }

            Texture texture = source.GetTexture(sourceProperty);
            if (texture == null)
            {
                return false;
            }

            destination.SetTexture(destinationProperty, texture);
            destination.SetTextureScale(destinationProperty, source.GetTextureScale(sourceProperty));
            destination.SetTextureOffset(destinationProperty, source.GetTextureOffset(sourceProperty));
            return true;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedDisplayMaterials.Count; i++)
            {
                Material material = ownedDisplayMaterials[i];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
            ownedDisplayMaterials.Clear();
        }

        private Color ResolveReferenceColor(string materialName, Color fallback)
        {
            if (modelStyle == CharacterModelStyle.Imported || string.IsNullOrWhiteSpace(materialName))
            {
                return fallback;
            }

            string name = materialName.Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .ToLowerInvariant();

            if (modelStyle == CharacterModelStyle.Heroine)
            {
                if (name.Contains("hair") && name.Contains("shadow")) return Rgb(50, 130, 189, fallback.a);
                if (name.Contains("hair") && name.Contains("highlight")) return Rgb(181, 238, 255, fallback.a);
                if (name.Contains("hair") || name.Contains("skyblue")) return Rgb(119, 216, 255, fallback.a);
                if (name.Contains("skin") && name.Contains("shadow")) return Rgb(217, 140, 130, fallback.a);
                if (name.Contains("skin") || name.Contains("porcelain")) return Rgb(255, 216, 203, fallback.a);
                if (name.Contains("eyewhite") || name.Contains("sclera")) return Rgb(255, 248, 237, fallback.a);
                if (name.Contains("amberiris") || name.Contains("eyeamber") || name.Contains("iris")) return Rgb(255, 139, 24, fallback.a);
                if (name.Contains("ink") || name.Contains("pupil") || name.Contains("lash")) return Rgb(30, 20, 32, fallback.a);
                if (name.Contains("wine") || name.Contains("redshadow") || name.Contains("crimsonshadow")) return Rgb(116, 29, 53, fallback.a);
                if (name.Contains("haori") && name.Contains("highlight")) return Rgb(233, 87, 114, fallback.a);
                if (name.Contains("haori") || name.Contains("crimson") || name.Contains("robe")) return Rgb(207, 54, 87, fallback.a);
                if (name.Contains("pleat") || name.Contains("orangeshadow")) return Rgb(184, 91, 54, fallback.a);
                if (name.Contains("skirt") && name.Contains("highlight")) return Rgb(255, 196, 125, fallback.a);
                if (name.Contains("skirt") || name.Contains("orange")) return Rgb(243, 161, 95, fallback.a);
                if (name.Contains("top") && (name.Contains("fold") || name.Contains("shadow"))) return Rgb(178, 190, 201, fallback.a);
                if (name.Contains("warmwhite") || name.Contains("topwhite") || name.Contains("shirt") || name.Contains("top")) return Rgb(255, 248, 237, fallback.a);
                if (name.Contains("lip")) return Rgb(183, 95, 104, fallback.a);
                if (name.Contains("goldhighlight")) return Rgb(255, 217, 90, fallback.a);
                if (name.Contains("bladeedge") || (name.Contains("katana") && name.Contains("edge"))) return Rgb(232, 250, 255, fallback.a);
                if (name.Contains("steel")) return Rgb(179, 216, 232, fallback.a);
                if (name.Contains("fitting")) return Rgb(38, 56, 73, fallback.a);
                if (name.Contains("wrap") || name.Contains("handleaccent")) return Rgb(158, 52, 86, fallback.a);
                if (name.Contains("rubber") || name.Contains("sole")) return Rgb(13, 17, 24, fallback.a);
                if (name.Contains("black") || name.Contains("boot") || name.Contains("belt")) return Rgb(23, 28, 37, fallback.a);
            }
            else if (modelStyle == CharacterModelStyle.Slime)
            {
                if (name.Contains("highlight") || name.Contains("spark")) return Rgb(255, 240, 181, fallback.a);
                if (name.Contains("core") || name.Contains("pupil")) return Rgb(28, 18, 35, fallback.a);
                if (name.Contains("amber") || name.Contains("eye")) return Rgb(255, 148, 28, fallback.a);
                if (name.Contains("cyan") || name.Contains("gel") || name.Contains("slimebody")) return Rgb(66, 207, 255, fallback.a);
            }

            return fallback;
        }

        private static Color Rgb(byte red, byte green, byte blue, float alpha)
        {
            return new Color32(red, green, blue, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }

        private static Color Multiply(Color left, Color right)
        {
            return new Color(
                left.r * right.r,
                left.g * right.g,
                left.b * right.b,
                left.a * right.a);
        }

        private static bool IsWhite(Color color)
        {
            return Mathf.Abs(color.r - 1f) < 0.0001f &&
                   Mathf.Abs(color.g - 1f) < 0.0001f &&
                   Mathf.Abs(color.b - 1f) < 0.0001f &&
                   Mathf.Abs(color.a - 1f) < 0.0001f;
        }

        private readonly struct TintTarget
        {
            public TintTarget(
                Renderer renderer,
                int materialIndex,
                bool hasBaseColor,
                Color baseColor,
                bool hasColor,
                Color color)
            {
                Renderer = renderer;
                MaterialIndex = materialIndex;
                HasBaseColor = hasBaseColor;
                BaseColor = baseColor;
                HasColor = hasColor;
                Color = color;
            }

            public Renderer Renderer { get; }
            public int MaterialIndex { get; }
            public bool HasBaseColor { get; }
            public Color BaseColor { get; }
            public bool HasColor { get; }
            public Color Color { get; }
        }
    }
}
