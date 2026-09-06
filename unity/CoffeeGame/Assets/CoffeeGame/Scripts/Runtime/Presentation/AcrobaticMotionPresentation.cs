using CoffeeGame.Combat;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    public enum AcrobaticMotionKind
    {
        GroundRoll,
        Backflip,
        Cartwheel,
        RunningSpin
    }

    /// <summary>
    /// Applies clock-owned full-body acrobatics over an ordinary controller pose.
    /// Gameplay movement and jump height remain entirely owned by the actor motor.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(250)]
    public sealed class AcrobaticMotionPresentation : MonoBehaviour
    {
        private sealed class BonePose
        {
            public Transform Bone;
            public Quaternion AuthoredRotation;
            public Quaternion AppliedRotation;
            public bool WasApplied;
        }

        private readonly BonePose[] poses = new BonePose[12];
        private Transform visualRoot;
        private GameObject clockOwner;
        private Animator animator;
        private DefensePosePresentation defensePose;
        private Transform hips;
        private Transform spine;
        private Transform chest;
        private Transform head;
        private Transform leftUpperArm;
        private Transform leftLowerArm;
        private Transform leftHand;
        private Transform rightUpperArm;
        private Transform rightLowerArm;
        private Transform rightHand;
        private Transform leftUpperLeg;
        private Transform leftLowerLeg;
        private Transform leftFoot;
        private Transform rightUpperLeg;
        private Transform rightLowerLeg;
        private Transform rightFoot;
        private Vector3 hipsAuthoredPosition;
        private Vector3 hipsAppliedPosition;
        private bool hipsPositionWasApplied;
        private float groundOffsetFromVisualRoot;
        private bool requested;
        private float motionBlend;
        private float progress;
        private Vector3 direction = Vector3.forward;
        private AcrobaticMotionKind currentKind;

        public bool HasPoseRig => animator != null && hips != null && spine != null &&
            leftUpperLeg != null && leftLowerLeg != null &&
            rightUpperLeg != null && rightLowerLeg != null;
        public bool IsPresenting => requested || motionBlend > 0.001f;
        public AcrobaticMotionKind CurrentKind => currentKind;
        public float Progress => progress;
        // Older cat controllers need an aerial spin overlay. V14 has its own
        // authored rotation, so applying both would rotate the cat twice.
        public bool UseRunningSpinFallback => clockOwner != null &&
            clockOwner.TryGetComponent<PlayerCombatController>(out var combat) && combat.IsCatMage &&
            (animator == null || animator.runtimeAnimatorController == null ||
             animator.runtimeAnimatorController.name != "SilverCatMotionV14");

        public void Initialize(Transform characterVisualRoot, GameObject owner)
        {
            RestorePose();
            visualRoot = characterVisualRoot != null ? characterVisualRoot : transform;
            clockOwner = owner != null ? owner : gameObject;
            defensePose = clockOwner.GetComponent<DefensePosePresentation>();
            requested = false;
            motionBlend = 0f;
            progress = 0f;
            RefreshRig();
        }

        public void RefreshRig()
        {
            RestorePose();
            animator = FindPoseAnimator(visualRoot != null ? visualRoot : transform);
            hips = null;
            spine = null;
            chest = null;
            head = null;
            leftUpperArm = null;
            leftLowerArm = null;
            leftHand = null;
            rightUpperArm = null;
            rightLowerArm = null;
            rightHand = null;
            leftUpperLeg = null;
            leftLowerLeg = null;
            leftFoot = null;
            rightUpperLeg = null;
            rightLowerLeg = null;
            rightFoot = null;
            for (int i = 0; i < poses.Length; i++)
            {
                poses[i] = null;
            }

            if (animator == null)
            {
                return;
            }

            hips = AddPose(0, HumanBodyBones.Hips);
            spine = AddPose(1, HumanBodyBones.Spine);
            chest = AddPose(2, HumanBodyBones.Chest);
            head = AddPose(3, HumanBodyBones.Head);
            leftUpperArm = AddPose(4, HumanBodyBones.LeftUpperArm);
            leftLowerArm = AddPose(5, HumanBodyBones.LeftLowerArm);
            rightUpperArm = AddPose(6, HumanBodyBones.RightUpperArm);
            rightLowerArm = AddPose(7, HumanBodyBones.RightLowerArm);
            leftUpperLeg = AddPose(8, HumanBodyBones.LeftUpperLeg);
            leftLowerLeg = AddPose(9, HumanBodyBones.LeftLowerLeg);
            rightUpperLeg = AddPose(10, HumanBodyBones.RightUpperLeg);
            rightLowerLeg = AddPose(11, HumanBodyBones.RightLowerLeg);
            rightFoot = ResolveBone(HumanBodyBones.RightFoot);
            leftFoot = ResolveBone(HumanBodyBones.LeftFoot);
            leftHand = ResolveBone(HumanBodyBones.LeftHand);
            rightHand = ResolveBone(HumanBodyBones.RightHand);

            float footY = MinimumY(leftFoot, rightFoot);
            groundOffsetFromVisualRoot = float.IsPositiveInfinity(footY)
                ? 0f
                : footY - visualRoot.position.y;
        }

        public void SetMotion(AcrobaticMotionKind kind, float progress01, Vector3 worldDirection)
        {
            currentKind = kind;
            progress = Mathf.Clamp01(progress01);
            Vector3 planar = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (planar.sqrMagnitude > 0.0001f)
            {
                direction = planar.normalized;
            }
            requested = true;
            if (defensePose == null && clockOwner != null)
            {
                defensePose = clockOwner.GetComponent<DefensePosePresentation>();
            }
            if (defensePose != null)
            {
                defensePose.SetGuarding(false);
                defensePose.SetPlunging(false);
                defensePose.SetPlungeRecovery(0f);
            }
        }

        public void ClearMotion()
        {
            requested = false;
        }

        private void LateUpdate()
        {
            float deltaTime = CombatClock.DeltaTime(clockOwner != null ? clockOwner : gameObject);
            if (deltaTime > 0f)
            {
                float speed = requested ? 14f : 10f;
                motionBlend = Mathf.MoveTowards(motionBlend, requested ? 1f : 0f, deltaTime * speed);
            }

            if (motionBlend > 0.001f)
            {
                ApplyMotion();
            }
            else
            {
                RestorePose();
            }
        }

        private void ApplyMotion()
        {
            if (!HasPoseRig)
            {
                return;
            }

            CaptureHipsPosition();
            float tuck = TuckEnvelope(progress) * motionBlend *
                (currentKind == AcrobaticMotionKind.GroundRoll ? 1f : 0.86f);
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
                pose.AuthoredRotation = authored;
                pose.AppliedRotation = authored;
                pose.Bone.localRotation = pose.AppliedRotation;
                pose.WasApplied = true;
            }

            Vector3 bodyForward = currentKind == AcrobaticMotionKind.Cartwheel ? GetVisualForward() :
                currentKind == AcrobaticMotionKind.Backflip ? -direction : direction;
            if (bodyForward.sqrMagnitude < 0.0001f)
            {
                bodyForward = GetVisualForward();
            }
            bodyForward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, bodyForward).normalized;
            if (currentKind == AcrobaticMotionKind.Cartwheel || currentKind == AcrobaticMotionKind.RunningSpin)
                ApplyCartwheelDirections(right, TuckEnvelope(progress) * motionBlend * (currentKind == AcrobaticMotionKind.RunningSpin ? .75f : 1f));
            else ApplyTuckDirections(bodyForward, right, tuck);

            float degrees = progress * 360f *
                (currentKind == AcrobaticMotionKind.Backflip ? -1f : 1f);
            // Blend the completed orientation along the shortest arc; scaling a nearly
            // 360-degree angle would unwind the entire flip during its recovery.
            hips.rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.AngleAxis(degrees, currentKind == AcrobaticMotionKind.Cartwheel
                    ? Vector3.Cross(Vector3.up, direction).normalized :
                    currentKind == AcrobaticMotionKind.RunningSpin ? (right + direction * .45f).normalized : right), motionBlend) * hips.rotation;
            RecordAppliedRotation(hips);

            if (currentKind == AcrobaticMotionKind.GroundRoll)
            {
                KeepGroundRollLow(tuck);
            }
            else
            {
                ApplyHipsPosition(hipsAuthoredPosition);
            }
        }

        private void ApplyCartwheelDirections(Vector3 right, float weight)
        {
            // A lateral wheel uses extended, separated legs rather than the
            // compact tuck used by forward and backward somersaults.
            AlignBone(spine, chest, Vector3.up, weight);
            AlignBone(chest, head, Vector3.up, weight);
            AlignBone(leftUpperLeg, leftLowerLeg, -Vector3.up - right * .52f, weight);
            AlignBone(leftLowerLeg, leftFoot, -Vector3.up - right * .48f, weight);
            AlignBone(rightUpperLeg, rightLowerLeg, -Vector3.up + right * .52f, weight);
            AlignBone(rightLowerLeg, rightFoot, -Vector3.up + right * .48f, weight);
            AlignBone(leftUpperArm, leftLowerArm, Vector3.up - right * .55f, weight);
            AlignBone(leftLowerArm, leftHand, Vector3.up - right * .2f, weight);
            AlignBone(rightUpperArm, rightLowerArm, Vector3.up + right * .55f, weight);
            AlignBone(rightLowerArm, rightHand, Vector3.up + right * .2f, weight);
        }

        private void ApplyTuckDirections(Vector3 forward, Vector3 right, float weight)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            // Curl the trunk in rig/world space. Imported Generic rigs do not
            // share reliable local Euler axes, and local-only offsets left the
            // rendered heroine arched like a handstand during the roll.
            AlignBone(spine, chest != null ? chest : head,
                forward * 0.94f + Vector3.up * 0.12f, weight);
            AlignBone(chest, head,
                forward * 0.7f - Vector3.up * 0.3f, weight);
            AlignBone(leftUpperLeg, leftLowerLeg,
                forward * 0.72f + Vector3.up * 0.52f + right * 0.1f, weight);
            AlignBone(leftLowerLeg, leftFoot,
                -forward * 0.92f + Vector3.up * 0.03f, weight);
            AlignBone(rightUpperLeg, rightLowerLeg,
                forward * 0.72f + Vector3.up * 0.52f - right * 0.1f, weight);
            AlignBone(rightLowerLeg, rightFoot,
                -forward * 0.92f + Vector3.up * 0.03f, weight);
            AlignBone(leftUpperArm, leftLowerArm,
                -forward * 0.12f - Vector3.up * 0.86f + right * 0.16f, weight);
            AlignBone(leftLowerArm, leftHand,
                forward * 0.62f - Vector3.up * 0.26f - right * 0.12f, weight);
            AlignBone(rightUpperArm, rightLowerArm,
                -forward * 0.12f - Vector3.up * 0.86f - right * 0.16f, weight);
            AlignBone(rightLowerArm, rightHand,
                forward * 0.62f - Vector3.up * 0.26f + right * 0.12f, weight);
        }

        private void KeepGroundRollLow(float tuck)
        {
            float minimum = MinimumY(hips, head, leftHand, rightHand, leftFoot, rightFoot,
                leftLowerLeg, rightLowerLeg);
            if (float.IsPositiveInfinity(minimum))
            {
                ApplyHipsPosition(hipsAuthoredPosition);
                return;
            }

            // Bone joints sit inside hair, clothes, hands and geta. Keep an
            // eight-centimetre mesh allowance above the authored foot plane.
            float target = visualRoot.position.y + groundOffsetFromVisualRoot + 0.08f;
            float rotating = Mathf.Sin(progress * Mathf.PI) * motionBlend;
            float floorWeight = Mathf.Clamp01(Mathf.Max(tuck, rotating));
            float verticalCorrection = Mathf.Clamp(target - minimum, -0.42f, 0.82f) * floorWeight;
            Vector3 correctedWorld = hips.parent != null
                ? hips.parent.TransformPoint(hipsAuthoredPosition) + Vector3.up * verticalCorrection
                : hipsAuthoredPosition + Vector3.up * verticalCorrection;
            Vector3 correctedLocal = hips.parent != null
                ? hips.parent.InverseTransformPoint(correctedWorld)
                : correctedWorld;
            ApplyHipsPosition(correctedLocal);
        }

        private void CaptureHipsPosition()
        {
            Vector3 current = hips.localPosition;
            hipsAuthoredPosition = hipsPositionWasApplied &&
                (current - hipsAppliedPosition).sqrMagnitude < 0.0000001f
                    ? hipsAuthoredPosition
                    : current;
        }

        private void ApplyHipsPosition(Vector3 localPosition)
        {
            hipsAppliedPosition = localPosition;
            hips.localPosition = localPosition;
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

        private Transform AddPose(int index, HumanBodyBones bone)
        {
            Transform target = ResolveBone(bone);
            if (target == null)
            {
                return null;
            }
            poses[index] = new BonePose
            {
                Bone = target,
                AuthoredRotation = target.localRotation
            };
            return target;
        }

        private Transform ResolveSpineChain(bool upper)
        {
            // Generic exports number the torso in different directions. The
            // current Meshy rig is Hips -> Spine02 -> Spine01 -> Spine -> neck.
            // Resolve anatomy from ancestry, never from the numerical suffix.
            Transform nearestHead = null, nearestHips = null;
            Transform resolvedHead = FindNamedTransform(animator.transform, "head");
            for (Transform node = resolvedHead != null ? resolvedHead.parent : null;
                node != null && node != animator.transform; node = node.parent)
            {
                string name = NormalizeName(node.name);
                if (!name.Contains("spine") && !name.EndsWith("chest")) continue;
                if (nearestHead == null) nearestHead = node;
                nearestHips = node;
            }
            return (upper ? nearestHead : nearestHips) ??
                FindNamedTransform(animator.transform, upper ? "spine01" : "spine", "chest", "spine02");
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
                case HumanBodyBones.Hips: return FindNamedTransform(animator.transform, "hips", "pelvis");
                case HumanBodyBones.Spine: return ResolveSpineChain(false);
                case HumanBodyBones.Chest: return ResolveSpineChain(true);
                case HumanBodyBones.Head: return FindNamedTransform(animator.transform, "head");
                case HumanBodyBones.LeftUpperArm: return FindNamedTransform(animator.transform, "leftarm", "upperarml");
                case HumanBodyBones.LeftLowerArm: return FindNamedTransform(animator.transform, "leftforearm", "forearml", "leftlowerarm");
                case HumanBodyBones.LeftHand: return FindNamedTransform(animator.transform, "lefthand", "handl");
                case HumanBodyBones.RightUpperArm: return FindNamedTransform(animator.transform, "rightarm", "upperarmr");
                case HumanBodyBones.RightLowerArm: return FindNamedTransform(animator.transform, "rightforearm", "forearmr", "rightlowerarm");
                case HumanBodyBones.RightHand: return FindNamedTransform(animator.transform, "righthand", "handr");
                case HumanBodyBones.LeftUpperLeg: return FindNamedTransform(animator.transform, "leftupleg", "leftthigh", "upperlegl");
                case HumanBodyBones.LeftLowerLeg: return FindNamedTransform(animator.transform, "leftleg", "leftlowerleg", "calfl");
                case HumanBodyBones.LeftFoot: return FindNamedTransform(animator.transform, "leftfoot", "footl");
                case HumanBodyBones.RightUpperLeg: return FindNamedTransform(animator.transform, "rightupleg", "rightthigh", "upperlegr");
                case HumanBodyBones.RightLowerLeg: return FindNamedTransform(animator.transform, "rightleg", "rightlowerleg", "calfr");
                case HumanBodyBones.RightFoot: return FindNamedTransform(animator.transform, "rightfoot", "footr");
                default: return null;
            }
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
                    FindNamedTransform(candidate.transform, "hips", "pelvis") == null))
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
                    string normalizedAlias = NormalizeName(aliases[alias]);
                    if (candidate == normalizedAlias || candidate.EndsWith(normalizedAlias))
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

        private Vector3 GetVisualForward()
        {
            Transform basis = visualRoot != null ? visualRoot : transform;
            Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static float TuckEnvelope(float normalizedProgress)
        {
            float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedProgress / 0.16f));
            float exit = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - normalizedProgress) / 0.18f));
            return enter * exit;
        }

        private static float MinimumY(params Transform[] points)
        {
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                {
                    minimum = Mathf.Min(minimum, points[i].position.y);
                }
            }
            return minimum;
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

        private void OnDisable()
        {
            RestorePose();
        }

        private void OnDestroy()
        {
            RestorePose();
        }
    }
}
