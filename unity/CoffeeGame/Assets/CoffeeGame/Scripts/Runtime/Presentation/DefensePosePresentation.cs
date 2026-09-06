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
    /// Adds a small late-frame humanoid pose over the active animation without
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

        private readonly BonePose[] poses = new BonePose[8];
        private Transform visualRoot;
        private GameObject clockOwner;
        private Animator animator;
        private DefensePoseStyle style;
        private bool guarding;
        private bool plunging;
        private float guardBlend;
        private float plungeBlend;
        private GameObject barrierRoot;
        private LineRenderer[] barrierLines;
        private Material barrierMaterial;
        private LineRenderer bladeGuardLine;
        private Material bladeMaterial;
        private LineRenderer[] plungeLines;
        private Material plungeMaterial;

        public bool IsGuarding => guarding;
        public bool IsPlunging => plunging;
        public bool HasHumanoidRig => animator != null && animator.isHuman;
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
            animator = FindHumanoidAnimator(visualRoot != null ? visualRoot : transform);
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
                AddPose(2, HumanBodyBones.LeftUpperArm, new Vector3(-18f, 18f, 50f), Vector3.zero);
                AddPose(3, HumanBodyBones.LeftLowerArm, new Vector3(2f, -48f, 4f), Vector3.zero);
                AddPose(4, HumanBodyBones.LeftHand, new Vector3(-8f, -10f, 12f), Vector3.zero);
                AddPose(5, HumanBodyBones.RightUpperArm, new Vector3(-18f, -18f, -50f), Vector3.zero);
                AddPose(6, HumanBodyBones.RightLowerArm, new Vector3(2f, 48f, -4f), Vector3.zero);
                AddPose(7, HumanBodyBones.RightHand, new Vector3(-8f, 10f, -12f), Vector3.zero);
                return;
            }

            AddPose(0, HumanBodyBones.Spine, new Vector3(7f, -6f, 0f), new Vector3(14f, 0f, 0f));
            AddPose(1, HumanBodyBones.Chest, new Vector3(-3f, -9f, 0f), new Vector3(10f, 0f, 0f));
            AddPose(2, HumanBodyBones.LeftUpperArm, new Vector3(-14f, 22f, 40f), new Vector3(8f, 5f, 18f));
            AddPose(3, HumanBodyBones.LeftLowerArm, new Vector3(0f, -34f, 8f), new Vector3(0f, -12f, 0f));
            AddPose(4, HumanBodyBones.RightUpperArm, new Vector3(-24f, -28f, -62f), new Vector3(66f, -8f, -24f));
            AddPose(5, HumanBodyBones.RightLowerArm, new Vector3(2f, 48f, -12f), new Vector3(4f, 8f, -8f));
            AddPose(6, HumanBodyBones.RightHand, new Vector3(8f, 12f, -22f), new Vector3(78f, 0f, 0f));
            AddPose(7, HumanBodyBones.Head, new Vector3(-2f, 5f, 0f), new Vector3(-10f, 0f, 0f));
        }

        public void SetGuarding(bool value)
        {
            guarding = value;
            if (value)
            {
                plunging = false;
            }
        }

        public void SetPlunging(bool value)
        {
            plunging = style == DefensePoseStyle.HeroineBlade && value;
            if (plunging)
            {
                guarding = false;
            }
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
            UpdatePersistentVisuals();
        }

        private void ApplyPose()
        {
            bool wantsPose = guardBlend > 0.001f || plungeBlend > 0.001f;
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
                    plungeBlend);
                pose.AuthoredRotation = authored;
                pose.AppliedRotation = authored * guardOffset * plungeOffset;
                pose.Bone.localRotation = pose.AppliedRotation;
                pose.WasApplied = true;
            }
        }

        private void AddPose(int index, HumanBodyBones bone, Vector3 guardEuler, Vector3 plungeEuler)
        {
            Transform target = animator.GetBoneTransform(bone);
            if (target == null)
            {
                return;
            }

            poses[index] = new BonePose
            {
                Bone = target,
                GuardEuler = guardEuler,
                PlungeEuler = plungeEuler,
                AuthoredRotation = target.localRotation
            };
        }

        private static Animator FindHumanoidAnimator(Transform root)
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
                if (candidate == null || !candidate.isHuman || candidate.avatar == null || !candidate.avatar.isValid)
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
                    barrierRoot.transform.localScale = Vector3.one * guardBlend * pulse;
                    barrierRoot.transform.localRotation = Quaternion.Euler(0f, 0f, clock * 18f);
                    for (int i = 0; i < barrierLines.Length; i++)
                    {
                        SetAlpha(barrierLines[i], guardBlend * (0.72f - i * 0.12f));
                    }
                }
            }

            Transform rightHand = animator != null ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (bladeGuardLine != null)
            {
                bool visible = guardBlend > 0.01f && rightHand != null;
                bladeGuardLine.gameObject.SetActive(visible);
                if (visible)
                {
                    Vector3 right = transform.right;
                    Vector3 up = transform.up;
                    Vector3 center = rightHand.position + up * 0.12f;
                    bladeGuardLine.SetPosition(0, center - right * 0.42f + up * 0.26f);
                    bladeGuardLine.SetPosition(1, center);
                    bladeGuardLine.SetPosition(2, center + right * 0.42f - up * 0.26f);
                    SetAlpha(bladeGuardLine, guardBlend * (0.7f + Mathf.Sin(clock * 11f) * 0.12f));
                }
            }

            if (plungeLines == null)
            {
                return;
            }

            for (int i = 0; i < plungeLines.Length; i++)
            {
                bool visible = plungeBlend > 0.01f && rightHand != null;
                plungeLines[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                Vector3 side = transform.right * ((i - 1) * 0.055f);
                Vector3 tip = rightHand.position + side - transform.up * 0.34f;
                plungeLines[i].SetPosition(0, tip + transform.up * (0.95f + i * 0.12f));
                plungeLines[i].SetPosition(1, tip + transform.up * 0.34f);
                plungeLines[i].SetPosition(2, tip);
                SetAlpha(plungeLines[i], plungeBlend * (0.65f - i * 0.1f));
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
