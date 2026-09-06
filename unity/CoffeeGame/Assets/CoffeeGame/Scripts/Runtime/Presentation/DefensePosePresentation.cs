using CoffeeGame.Combat;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum DefensePoseStyle
    {
        HeroineBlade,
        CatBarrier
    }

    /// <summary>
    /// Adds a small late-frame humanoid or named-bone pose over the active animation without
    /// replacing its controller or changing gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefensePosePresentation : MonoBehaviour
    {
        private sealed class BonePose
        {
            public Transform Bone;
            public Vector3 GuardEuler;
            public Vector3 PlungeEuler;
            public Quaternion AuthoredRotation;
            public Quaternion AppliedRotation;
            public bool WasApplied;
        }

        private readonly BonePose[] poses = new BonePose[13];
        private Transform visualRoot;
        private Transform facingRoot;
        private GameObject clockOwner;
        private Animator animator;
        private Transform spine;
        private Transform chest;
        private Transform head;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform hips;
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;
        private Vector3 swordAxisInRightHand;
        private float swordLength;
        private bool hasSwordAxis;
        private Vector3 requestedFacing;
        private bool hasRequestedFacing;
        private DefensePoseStyle style;
        private bool guarding;
        private bool plunging;
        private float guardBlend;
        private float plungeBlend;
        private float plungeRecovery;
        private Vector3 hipsAuthoredPosition;
        private Vector3 hipsAppliedPosition;
        private bool hipsPositionWasApplied;
        private GameObject barrierRoot;
        private LineRenderer[] barrierLines;
        private Material barrierMaterial;
        private LineRenderer bladeGuardLine;
        private Material bladeMaterial;
        private LineRenderer[] plungeLines;
        private Material plungeMaterial;

        public bool IsGuarding => guarding;
        public bool IsPlunging => plunging;
        public float PlungeRecovery => plungeRecovery;
        public bool HasHumanoidRig => animator != null && animator.isHuman;
        public bool HasPoseRig => animator != null && rightUpperArm != null && rightLowerArm != null && rightHand != null;
        public bool HasSwordAxis => hasSwordAxis;
        public DefensePoseStyle Style => style;

        public void Initialize(
            Transform characterVisualRoot,
            DefensePoseStyle poseStyle,
            GameObject combatClockOwner = null)
        {
            RestorePose();
            DestroyPersistentVisuals();
            visualRoot = characterVisualRoot != null ? characterVisualRoot : transform;
            style = poseStyle;
            clockOwner = combatClockOwner != null ? combatClockOwner : gameObject;
            RefreshRig();
            CreatePersistentVisuals();
        }

        public void RefreshRig()
        {
            RestorePose();
            animator = FindPoseAnimator(visualRoot != null ? visualRoot : transform);
            ModelCharacterVisual modelVisual = visualRoot != null
                ? visualRoot.GetComponentInChildren<ModelCharacterVisual>(true)
                : null;
            facingRoot = modelVisual != null ? modelVisual.transform :
                visualRoot != null ? visualRoot : transform;
            spine = null;
            chest = null;
            head = null;
            leftUpperArm = null;
            leftLowerArm = null;
            leftHand = null;
            rightUpperArm = null;
            rightLowerArm = null;
            rightHand = null;
            hips = null;
            leftUpperLeg = null;
            leftLowerLeg = null;
            leftFoot = null;
            rightUpperLeg = null;
            rightLowerLeg = null;
            rightFoot = null;
            swordAxisInRightHand = Vector3.zero;
            hasSwordAxis = false;
            swordLength = 0f;
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = null;
            }

            if (animator == null)
            {
                return;
            }

            if (style == DefensePoseStyle.CatBarrier)
            {
                AddPose(0, HumanBodyBones.Spine, new Vector3(4f, 0f, 0f), Vector3.zero);
                AddPose(1, HumanBodyBones.Chest, new Vector3(-5f, 0f, 0f), Vector3.zero);
                leftUpperArm = AddPose(2, HumanBodyBones.LeftUpperArm, new Vector3(-8f, 4f, 10f), Vector3.zero);
                leftLowerArm = AddPose(3, HumanBodyBones.LeftLowerArm, new Vector3(0f, -8f, 2f), Vector3.zero);
                leftHand = AddPose(4, HumanBodyBones.LeftHand, new Vector3(-8f, -10f, 12f), Vector3.zero);
                rightUpperArm = AddPose(5, HumanBodyBones.RightUpperArm, new Vector3(-8f, -4f, -10f), Vector3.zero);
                rightLowerArm = AddPose(6, HumanBodyBones.RightLowerArm, new Vector3(0f, 8f, -2f), Vector3.zero);
                rightHand = AddPose(7, HumanBodyBones.RightHand, new Vector3(-8f, 10f, -12f), Vector3.zero);
                return;
            }

            spine = AddPose(0, HumanBodyBones.Spine, new Vector3(7f, -6f, 0f), new Vector3(14f, 0f, 0f));
            chest = AddPose(1, HumanBodyBones.Chest, new Vector3(-3f, -9f, 0f), new Vector3(10f, 0f, 0f));
            leftUpperArm = AddPose(2, HumanBodyBones.LeftUpperArm, new Vector3(-6f, 4f, 8f), new Vector3(3f, 0f, 5f));
            leftLowerArm = AddPose(3, HumanBodyBones.LeftLowerArm, new Vector3(0f, -8f, 3f), new Vector3(0f, -3f, 0f));
            rightUpperArm = AddPose(4, HumanBodyBones.RightUpperArm, new Vector3(-8f, -6f, -12f), new Vector3(8f, -3f, -6f));
            rightLowerArm = AddPose(5, HumanBodyBones.RightLowerArm, new Vector3(0f, 9f, -3f), new Vector3(2f, 3f, -2f));
            rightHand = AddPose(6, HumanBodyBones.RightHand, new Vector3(4f, 5f, -8f), Vector3.zero);
            head = AddPose(7, HumanBodyBones.Head, new Vector3(-2f, 5f, 0f), new Vector3(-10f, 0f, 0f));
            hips = AddPose(8, HumanBodyBones.Hips, Vector3.zero, Vector3.zero);
            leftUpperLeg = AddPose(9, HumanBodyBones.LeftUpperLeg, Vector3.zero, Vector3.zero);
            leftLowerLeg = AddPose(10, HumanBodyBones.LeftLowerLeg, Vector3.zero, Vector3.zero);
            rightUpperLeg = AddPose(11, HumanBodyBones.RightUpperLeg, Vector3.zero, Vector3.zero);
            rightLowerLeg = AddPose(12, HumanBodyBones.RightLowerLeg, Vector3.zero, Vector3.zero);
            leftFoot = ResolveBone(HumanBodyBones.LeftFoot);
            rightFoot = ResolveBone(HumanBodyBones.RightFoot);
            leftHand = ResolveBone(HumanBodyBones.LeftHand);
            CacheSwordAxis();
        }

        public void SetGuarding(bool value)
        {
            guarding = value;
            if (value)
            {
                plunging = false;
                plungeRecovery = 0f;
            }
        }

        public void SetPlunging(bool value)
        {
            plunging = style == DefensePoseStyle.HeroineBlade && value;
            if (plunging)
            {
                guarding = false;
                plungeRecovery = 0f;
            }
        }

        /// <summary>
        /// Drives the planted-sword landing recovery explicitly from the motor clock.
        /// One is the first grounded frame and zero is the fully recovered pose.
        /// </summary>
        public void SetPlungeRecovery(float normalizedRemaining)
        {
            plungeRecovery = style == DefensePoseStyle.HeroineBlade
                ? Mathf.Clamp01(normalizedRemaining)
                : 0f;
            if (plungeRecovery > 0f)
            {
                guarding = false;
            }
        }

        public void SetFacing(Vector3 worldFacing)
        {
            Vector3 planar = Vector3.ProjectOnPlane(worldFacing, Vector3.up);
            if (planar.sqrMagnitude < 0.0001f)
            {
                return;
            }
            requestedFacing = planar.normalized;
            hasRequestedFacing = true;
        }

        private void LateUpdate()
        {
            float deltaTime = CombatClock.DeltaTime(clockOwner != null ? clockOwner : gameObject);
            if (deltaTime > 0f)
            {
                guardBlend = Mathf.MoveTowards(guardBlend, guarding ? 1f : 0f, deltaTime * 10f);
                plungeBlend = Mathf.MoveTowards(plungeBlend, plunging ? 1f : 0f, deltaTime * 14f);
            }

            ApplyPose();
            ApplyDirectionalPose();
            UpdatePersistentVisuals();
        }

        private void ApplyPose()
        {
            float effectivePlungeBlend = EffectivePlungeBlend;
            bool wantsPose = guardBlend > 0.001f || effectivePlungeBlend > 0.001f;
            for (int i = 0; i < poses.Length; i++)
            {
                BonePose pose = poses[i];
                if (pose == null || pose.Bone == null)
                {
                    continue;
                }

                Quaternion current = pose.Bone.localRotation;
                Quaternion authored = pose.WasApplied && Quaternion.Angle(current, pose.AppliedRotation) < 0.05f
                    ? pose.AuthoredRotation
                    : current;
                if (!wantsPose)
                {
                    if (pose.WasApplied)
                    {
                        pose.Bone.localRotation = authored;
                    }
                    pose.WasApplied = false;
                    continue;
                }

                Quaternion guardOffset = Quaternion.SlerpUnclamped(
                    Quaternion.identity,
                    Quaternion.Euler(pose.GuardEuler),
                    guardBlend);
                Quaternion plungeOffset = Quaternion.SlerpUnclamped(
                    Quaternion.identity,
                    Quaternion.Euler(pose.PlungeEuler),
                    effectivePlungeBlend);
                pose.AuthoredRotation = authored;
                pose.AppliedRotation = authored * guardOffset * plungeOffset;
                pose.Bone.localRotation = pose.AppliedRotation;
                pose.WasApplied = true;
            }

            ApplyLandingCrouch();
        }

        private void ApplyDirectionalPose()
        {
            if (!HasPoseRig)
            {
                return;
            }

            Vector3 up = Vector3.up;
            Vector3 forward = GetVisualForward();
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (guardBlend > 0.001f)
            {
                if (style == DefensePoseStyle.CatBarrier)
                {
                    AlignBone(leftUpperArm, leftLowerArm,
                        forward * 0.72f + right * 0.22f - up * 0.12f, guardBlend);
                    AlignBone(leftLowerArm, leftHand,
                        forward * 0.84f + right * 0.12f + up * 0.18f, guardBlend);
                    AlignBone(rightUpperArm, rightLowerArm,
                        forward * 0.72f - right * 0.22f - up * 0.12f, guardBlend);
                    AlignBone(rightLowerArm, rightHand,
                        forward * 0.84f - right * 0.12f + up * 0.18f, guardBlend);
                }
                else
                {
                    AlignBone(leftUpperArm, leftLowerArm,
                        right * 0.52f + forward * 0.28f - up * 0.34f, guardBlend);
                    AlignBone(leftLowerArm, leftHand,
                        right * 0.48f + forward * 0.3f + up * 0.2f, guardBlend);
                    AlignBone(rightUpperArm, rightLowerArm,
                        -right * 0.7f + forward * 0.28f - up * 0.34f, guardBlend);
                    AlignBone(rightLowerArm, rightHand,
                        -right * 0.52f + forward * 0.34f + up * 0.22f, guardBlend);
                    AlignSwordAxis((-right + up * 0.38f).normalized, guardBlend);
                }
            }

            float effectivePlungeBlend = EffectivePlungeBlend;
            if (effectivePlungeBlend > 0.001f && style == DefensePoseStyle.HeroineBlade)
            {
                AlignBone(rightUpperArm, rightLowerArm,
                    -up * 0.84f + forward * 0.28f - right * 0.12f, effectivePlungeBlend);
                AlignBone(rightLowerArm, rightHand,
                    -up * 0.92f + forward * 0.2f, effectivePlungeBlend);
                AlignSwordAxis(Vector3.down, plunging ? 1f : effectivePlungeBlend);

                float crouch = RecoveryPoseWeight;
                if (crouch > 0.001f)
                {
                    AlignBone(spine, chest,
                        forward * 0.9f - up * 0.12f, crouch);
                    AlignBone(chest, head,
                        forward * 0.72f - up * 0.24f, crouch);
                    AlignBone(leftUpperLeg, leftLowerLeg,
                        forward * 0.9f - up * 0.22f + right * 0.08f, crouch);
                    AlignBone(leftLowerLeg, leftFoot,
                        -forward * 0.34f - up * 0.94f, crouch);
                    AlignBone(rightUpperLeg, rightLowerLeg,
                        forward * 0.9f - up * 0.22f - right * 0.08f, crouch);
                    AlignBone(rightLowerLeg, rightFoot,
                        -forward * 0.34f - up * 0.94f, crouch);
                    // Curling the parent spine moves the sword arm too. Replant
                    // the weapon after the torso and knees reach their final pose.
                    AlignBone(rightUpperArm, rightLowerArm,
                        -up * 0.84f + forward * 0.28f - right * 0.12f, effectivePlungeBlend);
                    AlignBone(rightLowerArm, rightHand,
                        -up * 0.92f + forward * 0.2f, effectivePlungeBlend);
                    AlignSwordAxis(Vector3.down, effectivePlungeBlend);
                }
            }
        }

        private float RecoveryPoseWeight => 1f - Mathf.Pow(1f - plungeRecovery, 2f);
        private float EffectivePlungeBlend => Mathf.Max(plungeBlend, RecoveryPoseWeight);

        private void ApplyLandingCrouch()
        {
            if (hips == null)
            {
                return;
            }

            Vector3 current = hips.localPosition;
            Vector3 authored = hipsPositionWasApplied &&
                (current - hipsAppliedPosition).sqrMagnitude < 0.0000001f
                    ? hipsAuthoredPosition
                    : current;
            float weight = RecoveryPoseWeight;
            if (weight <= 0.001f)
            {
                if (hipsPositionWasApplied)
                {
                    hips.localPosition = authored;
                }
                hipsPositionWasApplied = false;
                return;
            }

            Vector3 worldOffset = Vector3.down * (0.42f * weight);
            Vector3 localOffset = hips.parent != null
                ? hips.parent.InverseTransformVector(worldOffset)
                : worldOffset;
            hipsAuthoredPosition = authored;
            hipsAppliedPosition = authored + localOffset;
            hips.localPosition = hipsAppliedPosition;
            hipsPositionWasApplied = true;
        }

        private void AlignBone(Transform bone, Transform child, Vector3 desiredDirection, float weight)
        {
            if (bone == null || child == null || desiredDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector3 currentDirection = child.position - bone.position;
            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Quaternion correction = Quaternion.FromToRotation(currentDirection.normalized, desiredDirection.normalized);
            bone.rotation = Quaternion.SlerpUnclamped(
                bone.rotation,
                correction * bone.rotation,
                Mathf.Clamp01(weight));
            RecordAppliedRotation(bone);
        }

        private void AlignSwordAxis(Vector3 desiredDirection, float weight)
        {
            if (!hasSwordAxis || rightHand == null || desiredDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector3 currentAxis = rightHand.rotation * swordAxisInRightHand;
            Quaternion correction = Quaternion.FromToRotation(currentAxis, desiredDirection.normalized);
            rightHand.rotation = Quaternion.SlerpUnclamped(
                rightHand.rotation,
                correction * rightHand.rotation,
                Mathf.Clamp01(weight));
            RecordAppliedRotation(rightHand);
        }

        private void RecordAppliedRotation(Transform bone)
        {
            for (int i = 0; i < poses.Length; i++)
            {
                if (poses[i] != null && poses[i].Bone == bone)
                {
                    poses[i].AppliedRotation = bone.localRotation;
                    poses[i].WasApplied = true;
                    return;
                }
            }
        }

        private void CacheSwordAxis()
        {
            if (rightHand == null || animator == null)
            {
                return;
            }
            Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
            float bestDistance = 0f;
            for (int i = 0; i < renderers.Length; i++)
            {
                string name = NormalizeName(renderers[i].name);
                if (!(renderers[i] is SkinnedMeshRenderer) && !(renderers[i] is MeshRenderer)) continue;
                if (!name.Contains("katana") && !name.Contains("sword") && !name.Contains("blade"))
                {
                    continue;
                }
                Vector3 direction = MeasureBladeTipDirection(renderers[i]);
                if (direction.sqrMagnitude <= bestDistance * bestDistance || direction.sqrMagnitude < 0.0025f)
                {
                    continue;
                }
                bestDistance = direction.magnitude;
                swordLength = bestDistance;
                swordAxisInRightHand = Quaternion.Inverse(rightHand.rotation) * direction.normalized;
                hasSwordAxis = true;
            }
        }

        // Skinned renderer bounds span animation poses; their center is not the
        // current blade. Bake the small weapon once to calibrate its real tip.
        private Vector3 MeasureBladeTipDirection(Renderer renderer)
        {
            Mesh baked = null;
            Mesh mesh = null;
            try
            {
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    // Imported FBX renderers carry a 0.01 scale. Include that
                    // scale so TransformPoint reconstructs the rendered tip.
                    baked = new Mesh(); skinned.BakeMesh(baked, true); mesh = baked;
                }
                else
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                if (mesh == null || !mesh.isReadable) return renderer.bounds.center - rightHand.position;
                Vector3 farthest = Vector3.zero;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 candidate = renderer.transform.TransformPoint(vertex) - rightHand.position;
                    if (candidate.sqrMagnitude > farthest.sqrMagnitude) farthest = candidate;
                }
                return farthest;
            }
            finally { if (baked != null) DestroyOwned(baked); }
        }

        public Vector3 MeasureBladeWorldDirection()
        {
            if (animator == null || rightHand == null) return Vector3.zero;
            Vector3 longest = Vector3.zero;
            foreach (Renderer renderer in animator.GetComponentsInChildren<Renderer>(true))
            {
                string name = NormalizeName(renderer.name);
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (!name.Contains("katana") && !name.Contains("sword") && !name.Contains("blade")) continue;
                Vector3 direction = MeasureBladeTipDirection(renderer);
                if (direction.sqrMagnitude > longest.sqrMagnitude) longest = direction;
            }
            return longest.normalized;
        }

        private Vector3 GetVisualForward()
        {
            if (hasRequestedFacing)
            {
                return requestedFacing;
            }
            Transform basis = facingRoot != null ? facingRoot : transform;
            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private Transform AddPose(int index, HumanBodyBones bone, Vector3 guardEuler, Vector3 plungeEuler)
        {
            Transform target = ResolveBone(bone);
            if (target == null)
            {
                return null;
            }

            poses[index] = new BonePose
            {
                Bone = target,
                GuardEuler = guardEuler,
                PlungeEuler = plungeEuler,
                AuthoredRotation = target.localRotation
            };
            return target;
        }

        private static Animator FindPoseAnimator(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            Animator[] candidates = root.GetComponentsInChildren<Animator>(true);
            Animator inactiveFallback = null;
            for (int i = 0; i < candidates.Length; i++)
            {
                Animator candidate = candidates[i];
                if (candidate == null || (!candidate.isHuman &&
                    FindNamedTransform(candidate.transform, "righthand") == null))
                {
                    continue;
                }

                if (candidate.gameObject.activeInHierarchy)
                {
                    return candidate;
                }
                inactiveFallback = inactiveFallback != null ? inactiveFallback : candidate;
            }
            return inactiveFallback;
        }

        private Transform ResolveBone(HumanBodyBones bone)
        {
            if (animator == null)
            {
                return null;
            }
            if (animator.isHuman && animator.avatar != null && animator.avatar.isValid)
            {
                Transform humanoid = animator.GetBoneTransform(bone);
                if (humanoid != null)
                {
                    return humanoid;
                }
            }

            switch (bone)
            {
                case HumanBodyBones.Spine: return FindNamedTransform(animator.transform, "spine");
                case HumanBodyBones.Chest: return FindNamedTransform(animator.transform, "spine01", "chest", "spine02");
                case HumanBodyBones.Head: return FindNamedTransform(animator.transform, "head");
                case HumanBodyBones.Hips: return FindNamedTransform(animator.transform, "hips", "pelvis");
                case HumanBodyBones.LeftUpperLeg: return FindNamedTransform(animator.transform, "leftupleg", "leftthigh", "upperlegl");
                case HumanBodyBones.LeftLowerLeg: return FindNamedTransform(animator.transform, "leftleg", "leftlowerleg", "calfl");
                case HumanBodyBones.LeftFoot: return FindNamedTransform(animator.transform, "leftfoot", "footl");
                case HumanBodyBones.RightUpperLeg: return FindNamedTransform(animator.transform, "rightupleg", "rightthigh", "upperlegr");
                case HumanBodyBones.RightLowerLeg: return FindNamedTransform(animator.transform, "rightleg", "rightlowerleg", "calfr");
                case HumanBodyBones.RightFoot: return FindNamedTransform(animator.transform, "rightfoot", "footr");
                case HumanBodyBones.LeftUpperArm: return FindNamedTransform(animator.transform, "leftarm", "upperarml");
                case HumanBodyBones.LeftLowerArm: return FindNamedTransform(animator.transform, "leftforearm", "forearml", "leftlowerarm");
                case HumanBodyBones.LeftHand: return FindNamedTransform(animator.transform, "lefthand", "handl");
                case HumanBodyBones.RightUpperArm: return FindNamedTransform(animator.transform, "rightarm", "upperarmr");
                case HumanBodyBones.RightLowerArm: return FindNamedTransform(animator.transform, "rightforearm", "forearmr", "rightlowerarm");
                case HumanBodyBones.RightHand: return FindNamedTransform(animator.transform, "righthand", "handr");
                default: return null;
            }
        }

        private static Transform FindNamedTransform(Transform root, params string[] aliases)
        {
            if (root == null)
            {
                return null;
            }
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string candidate = NormalizeName(transforms[i].name);
                for (int alias = 0; alias < aliases.Length; alias++)
                {
                    if (candidate == NormalizeName(aliases[alias]))
                    {
                        return transforms[i];
                    }
                }
            }
            return null;
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            var characters = new char[value.Length];
            int count = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char character = char.ToLowerInvariant(value[i]);
                if (char.IsLetterOrDigit(character))
                {
                    characters[count++] = character;
                }
            }
            return new string(characters, 0, count);
        }

        private void CreatePersistentVisuals()
        {
            if (style == DefensePoseStyle.CatBarrier)
            {
                barrierRoot = new GameObject("Cat guard magic barrier");
                barrierRoot.transform.SetParent(transform, false);
                barrierRoot.transform.localPosition = new Vector3(0f, 0.82f, 0.48f);
                CombatOwnership.Assign(barrierRoot, clockOwner);
                barrierMaterial = CombatGlowVisuals.CreateMaterial(
                    "Cat guard barrier material",
                    new Color(0.25f, 0.92f, 1f, 0.86f));
                barrierLines = new LineRenderer[3];
                for (int ring = 0; ring < barrierLines.Length; ring++)
                {
                    barrierLines[ring] = CreateLine(barrierRoot.transform, $"Barrier ring {ring + 1}", 33,
                        0.025f + ring * 0.008f, true, barrierMaterial, false);
                    float radius = 0.48f + ring * 0.09f;
                    for (int point = 0; point < barrierLines[ring].positionCount; point++)
                    {
                        float angle = point / (float)(barrierLines[ring].positionCount - 1) * Mathf.PI * 2f;
                        float squash = ring == 1 ? 0.83f : 1f;
                        barrierLines[ring].SetPosition(point,
                            new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * squash, 0f));
                    }
                }
                barrierRoot.SetActive(false);
                return;
            }

            bladeMaterial = CombatGlowVisuals.CreateMaterial(
                "Heroine blade guard glint material",
                new Color(1f, 0.86f, 0.45f, 0.82f));
            bladeGuardLine = CreateLine(transform, "Heroine blade guard glint", 3, 0.028f, false,
                bladeMaterial, true);
            bladeGuardLine.gameObject.SetActive(false);
            plungeMaterial = CombatGlowVisuals.CreateMaterial(
                "Heroine plunge trail material",
                new Color(0.52f, 0.94f, 1f, 0.72f));
            plungeLines = new LineRenderer[3];
            for (int i = 0; i < plungeLines.Length; i++)
            {
                plungeLines[i] = CreateLine(transform, $"Heroine plunge trail {i + 1}", 3,
                    0.035f - i * 0.008f, false, plungeMaterial, true);
                plungeLines[i].gameObject.SetActive(false);
            }
        }

        private void UpdatePersistentVisuals()
        {
            float clock = CombatClock.Time(clockOwner != null ? clockOwner : gameObject);
            if (barrierRoot != null)
            {
                bool visible = guardBlend > 0.01f;
                barrierRoot.SetActive(visible);
                if (visible)
                {
                    float pulse = 1f + Mathf.Sin(clock * 8f) * 0.035f;
                    Vector3 forward = GetVisualForward();
                    barrierRoot.transform.position = transform.position + Vector3.up * 0.82f + forward * 0.48f;
                    barrierRoot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up) *
                        Quaternion.Euler(0f, 0f, clock * 18f);
                    barrierRoot.transform.localScale = Vector3.one * guardBlend * pulse;
                    for (int i = 0; i < barrierLines.Length; i++)
                    {
                        SetAlpha(barrierLines[i], guardBlend * (0.72f - i * 0.12f));
                    }
                }
            }

            if (bladeGuardLine != null)
            {
                bool visible = guardBlend > 0.01f && rightHand != null;
                bladeGuardLine.gameObject.SetActive(visible);
                if (visible)
                {
                    Vector3 axis = rightHand.rotation * swordAxisInRightHand;
                    Vector3 start = rightHand.position;
                    bladeGuardLine.SetPosition(0, start + axis * swordLength * 0.16f);
                    bladeGuardLine.SetPosition(1, start + axis * swordLength * 0.56f);
                    bladeGuardLine.SetPosition(2, start + axis * swordLength);
                    SetAlpha(bladeGuardLine, guardBlend * (0.7f + Mathf.Sin(clock * 11f) * 0.12f));
                }
            }

            if (plungeLines == null)
            {
                return;
            }

            for (int i = 0; i < plungeLines.Length; i++)
            {
                float effectivePlungeBlend = EffectivePlungeBlend;
                bool visible = effectivePlungeBlend > 0.01f && rightHand != null;
                plungeLines[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                Vector3 right = Vector3.Cross(Vector3.up, GetVisualForward()).normalized;
                Vector3 side = right * ((i - 1) * 0.055f);
                Vector3 tip = rightHand.position + side - Vector3.up * 0.34f;
                plungeLines[i].SetPosition(0, tip + Vector3.up * (0.95f + i * 0.12f));
                plungeLines[i].SetPosition(1, tip + Vector3.up * 0.34f);
                plungeLines[i].SetPosition(2, tip);
                SetAlpha(plungeLines[i], effectivePlungeBlend * (0.65f - i * 0.1f));
            }
        }

        private static LineRenderer CreateLine(
            Transform parent,
            string name,
            int points,
            float width,
            bool loop,
            Material material,
            bool worldSpace)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = worldSpace;
            line.loop = loop;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 1;
            line.startColor = line.endColor = Color.white;
            if (material != null)
            {
                line.sharedMaterial = material;
            }
            return line;
        }

        private static void SetAlpha(LineRenderer line, float alpha)
        {
            if (line == null)
            {
                return;
            }
            Color color = Color.white;
            color.a = Mathf.Clamp01(alpha);
            line.startColor = line.endColor = color;
        }

        private void RestorePose()
        {
            for (int i = 0; i < poses.Length; i++)
            {
                BonePose pose = poses[i];
                if (pose != null && pose.Bone != null && pose.WasApplied)
                {
                    pose.Bone.localRotation = pose.AuthoredRotation;
                    pose.WasApplied = false;
                }
            }
            if (hips != null && hipsPositionWasApplied)
            {
                hips.localPosition = hipsAuthoredPosition;
                hipsPositionWasApplied = false;
            }
        }

        private void DestroyPersistentVisuals()
        {
            if (barrierRoot != null) DestroyOwned(barrierRoot);
            if (bladeGuardLine != null) DestroyOwned(bladeGuardLine.gameObject);
            if (plungeLines != null)
            {
                for (int i = 0; i < plungeLines.Length; i++)
                {
                    if (plungeLines[i] != null) DestroyOwned(plungeLines[i].gameObject);
                }
            }
            if (barrierMaterial != null) DestroyOwned(barrierMaterial);
            if (bladeMaterial != null) DestroyOwned(bladeMaterial);
            if (plungeMaterial != null) DestroyOwned(plungeMaterial);
            barrierRoot = null;
            barrierLines = null;
            bladeGuardLine = null;
            plungeLines = null;
            barrierMaterial = null;
            bladeMaterial = null;
            plungeMaterial = null;
        }

        private static void DestroyOwned(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDisable()
        {
            RestorePose();
        }

        private void OnDestroy()
        {
            RestorePose();
            if (barrierMaterial != null) DestroyOwned(barrierMaterial);
            if (bladeMaterial != null) DestroyOwned(bladeMaterial);
            if (plungeMaterial != null) DestroyOwned(plungeMaterial);
            barrierMaterial = null;
            bladeMaterial = null;
            plungeMaterial = null;
        }
    }
}
