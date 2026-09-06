using CoffeeGame.Combat;
using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class DefenseFeedbackPresentationTests
    {
        [Test]
        public void EveryFeedbackEventCreatesClockOwnedVisualWithoutActivePhysics()
        {
            var owner = new GameObject("feedback-owner");
            var host = new GameObject("feedback-host");
            DefenseFeedback feedback = host.AddComponent<DefenseFeedback>();
            feedback.Initialize(null, owner);
            try
            {
                foreach (DefenseFeedbackEvent feedbackEvent in System.Enum.GetValues(typeof(DefenseFeedbackEvent)))
                {
                    GameObject effect = feedback.Emit(
                        feedbackEvent,
                        Vector3.zero,
                        Vector3.forward,
                        1f);

                    Assert.That(effect, Is.Not.Null, feedbackEvent.ToString());
                    Assert.That(CombatOwnership.Resolve(effect), Is.SameAs(owner), feedbackEvent.ToString());
                    Assert.That(effect.GetComponentsInChildren<LineRenderer>().Length,
                        Is.GreaterThan(0), feedbackEvent.ToString());
                    foreach (Collider collider in effect.GetComponentsInChildren<Collider>())
                    {
                        Assert.That(collider.enabled, Is.False, feedbackEvent.ToString());
                    }
                    Object.DestroyImmediate(effect);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PoseStateIsExclusiveAndSafeWithoutAHumanoidRig()
        {
            var actor = new GameObject("pose-owner");
            DefensePosePresentation pose = actor.AddComponent<DefensePosePresentation>();
            try
            {
                pose.Initialize(actor.transform, DefensePoseStyle.HeroineBlade, actor);
                Assert.That(pose.HasHumanoidRig, Is.False);

                pose.SetGuarding(true);
                Assert.That(pose.IsGuarding, Is.True);
                pose.SetPlunging(true);

                Assert.That(pose.IsGuarding, Is.False);
                Assert.That(pose.IsPlunging, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void CatPoseRejectsPlungeState()
        {
            var actor = new GameObject("cat-pose-owner");
            DefensePosePresentation pose = actor.AddComponent<DefensePosePresentation>();
            try
            {
                pose.Initialize(null, DefensePoseStyle.CatBarrier, actor);
                pose.SetPlunging(true);

                Assert.That(pose.Style, Is.EqualTo(DefensePoseStyle.CatBarrier));
                Assert.That(pose.IsPlunging, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void GenericMeshyRigResolvesNamedArmChainAndMeasuredSwordAxis()
        {
            var actor = new GameObject("generic-pose-owner");
            var visual = new GameObject("generic-visual");
            visual.transform.SetParent(actor.transform, false);
            visual.AddComponent<Animator>();
            Transform spine = Child(visual.transform, "Spine", new Vector3(0f, 0.8f, 0f));
            Child(spine, "Spine01", new Vector3(0f, 0.25f, 0f));
            Transform leftArm = Child(spine, "LeftArm", new Vector3(-0.2f, 0.2f, 0f));
            Transform leftForeArm = Child(leftArm, "LeftForeArm", new Vector3(-0.24f, 0f, 0f));
            Child(leftForeArm, "LeftHand", new Vector3(-0.2f, 0f, 0f));
            Transform rightArm = Child(spine, "RightArm", new Vector3(0.2f, 0.2f, 0f));
            Transform rightForeArm = Child(rightArm, "RightForeArm", new Vector3(0.24f, 0f, 0f));
            Transform rightHand = Child(rightForeArm, "RightHand", new Vector3(0.2f, 0f, 0f));
            GameObject sword = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sword.name = "AzureMaidenKatana";
            sword.transform.SetParent(rightHand, false);
            sword.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            sword.transform.localScale = new Vector3(0.05f, 0.9f, 0.05f);

            DefensePosePresentation pose = actor.AddComponent<DefensePosePresentation>();
            try
            {
                pose.Initialize(actor.transform, DefensePoseStyle.HeroineBlade, actor);

                Assert.That(pose.HasHumanoidRig, Is.False);
                Assert.That(pose.HasPoseRig, Is.True);
                Assert.That(pose.HasSwordAxis, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void PlungeAlignsScaledSkinnedBladeToGround()
        {
            var actor = new GameObject("scaled-blade-owner");
            var visual = Child(actor.transform, "visual", Vector3.zero);
            visual.localScale = Vector3.one * .01f;
            visual.gameObject.AddComponent<Animator>();
            var arm = Child(visual, "RightArm", new Vector3(20f, 100f, 0f));
            var forearm = Child(arm, "RightForeArm", new Vector3(24f, 0f, 0f));
            var hand = Child(forearm, "RightHand", new Vector3(20f, 0f, 0f));
            var blade = Child(visual, "Katana", Vector3.zero).gameObject.AddComponent<SkinnedMeshRenderer>();
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(64f, 100f, 0f), new Vector3(65f, 100f, 0f), new Vector3(64f, 190f, 0f) };
            mesh.triangles = new[] {0, 1, 2};
            mesh.bindposes = new[] { hand.worldToLocalMatrix * blade.transform.localToWorldMatrix };
            mesh.boneWeights = new[] {
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f },
                new BoneWeight { boneIndex0 = 0, weight0 = 1f }
            };
            blade.bones = new[] {hand}; blade.rootBone = arm; blade.sharedMesh = mesh;
            var pose = actor.AddComponent<DefensePosePresentation>();
            try
            {
                pose.Initialize(visual, DefensePoseStyle.HeroineBlade, actor);
                Assert.That(pose.HasSwordAxis, Is.True);
                pose.SetPlunging(true);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(DefensePosePresentation).GetField("plungeBlend", flags).SetValue(pose, 1f);
                typeof(DefensePosePresentation).GetMethod("ApplyDirectionalPose", flags).Invoke(pose, null);
                // Independent skinning equation: do not reuse the pose's baked-axis measurement.
                Vector3 tip = hand.localToWorldMatrix.MultiplyPoint3x4(mesh.bindposes[0].MultiplyPoint3x4(mesh.vertices[2]));
                Assert.That(Vector3.Dot((tip - hand.position).normalized, Vector3.down), Is.GreaterThan(.999f));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void PlungeRecoveryLowersGenericHipsAndRestoresTheAuthoredPosition()
        {
            var actor = new GameObject("plunge-recovery-owner");
            var visual = Child(actor.transform, "visual", Vector3.zero);
            visual.gameObject.AddComponent<Animator>();
            Transform hips = Child(visual, "Hips", new Vector3(0f, 1f, 0f));
            Transform spine = Child(hips, "Spine", new Vector3(0f, .24f, 0f));
            Child(spine, "Spine01", new Vector3(0f, .2f, 0f));
            Child(spine, "Head", new Vector3(0f, .45f, 0f));
            AddArmChain(spine, "Left", -1f);
            AddArmChain(spine, "Right", 1f);
            AddLegChain(hips, "Left", -1f);
            AddLegChain(hips, "Right", 1f);
            var pose = actor.AddComponent<DefensePosePresentation>();
            Vector3 authoredHips = hips.position;
            try
            {
                pose.Initialize(visual, DefensePoseStyle.HeroineBlade, actor);
                pose.SetPlungeRecovery(1f);
                InvokePrivate(pose, "ApplyPose");

                Assert.That(authoredHips.y - hips.position.y, Is.EqualTo(.34f).Within(.001f));
                Assert.That(pose.PlungeRecovery, Is.EqualTo(1f));

                pose.SetPlungeRecovery(0f);
                InvokePrivate(pose, "ApplyPose");
                Assert.That(Vector3.Distance(hips.position, authoredHips), Is.LessThan(.001f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ProceduralGroundRollTucksAndRotatesTheGenericBodyAroundATransverseAxis()
        {
            var actor = new GameObject("roll-presentation-owner");
            var visual = Child(actor.transform, "visual", Vector3.zero);
            visual.gameObject.AddComponent<Animator>();
            Transform hips = Child(visual, "Hips", new Vector3(0f, 1f, 0f));
            Transform spine = Child(hips, "Spine", new Vector3(0f, .25f, 0f));
            Transform chest = Child(spine, "Spine01", new Vector3(0f, .22f, 0f));
            Transform head = Child(chest, "Head", new Vector3(0f, .34f, 0f));
            AddArmChain(chest, "Left", -1f);
            AddArmChain(chest, "Right", 1f);
            Transform leftFoot = AddLegChain(hips, "Left", -1f);
            AddLegChain(hips, "Right", 1f);
            var motion = actor.AddComponent<AcrobaticMotionPresentation>();
            Quaternion authoredRotation = hips.localRotation;
            Vector3 authoredPosition = hips.localPosition;
            float authoredFootDistance = Vector3.Distance(hips.position, leftFoot.position);
            float authoredGroundY = leftFoot.position.y;
            try
            {
                motion.Initialize(visual, actor);
                Assert.That(motion.HasPoseRig, Is.True);
                motion.SetMotion(AcrobaticMotionKind.GroundRoll, .5f, Vector3.forward);
                SetPrivateField(motion, "motionBlend", 1f);
                InvokePrivate(motion, "ApplyMotion");

                Assert.That(Vector3.Dot(hips.up, Vector3.up), Is.LessThan(-.98f),
                    "the body must be upside down halfway through a full forward roll");
                Assert.That(Vector3.Distance(hips.position, leftFoot.position),
                    Is.LessThan(authoredFootDistance * .7f),
                    "the knees must fold the feet toward the hips instead of rotating an upright mannequin");
                Assert.That(Mathf.Min(hips.position.y, head.position.y),
                    Is.GreaterThanOrEqualTo(authoredGroundY - .001f),
                    "the grounded roll must keep its pelvis and head above the original foot plane");
                Assert.That(motion.CurrentKind, Is.EqualTo(AcrobaticMotionKind.GroundRoll));
                Assert.That(motion.Progress, Is.EqualTo(.5f));

                InvokePrivate(motion, "RestorePose");
                Assert.That(Quaternion.Angle(hips.localRotation, authoredRotation), Is.LessThan(.01f));
                Assert.That(Vector3.Distance(hips.localPosition, authoredPosition), Is.LessThan(.001f));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ProceduralBackflipUsesTheOppositeQuarterTurnAndSupportsNamespacedBones()
        {
            var actor = new GameObject("backflip-presentation-owner");
            var visual = Child(actor.transform, "visual", Vector3.zero);
            visual.gameObject.AddComponent<Animator>();
            Transform hips = Child(visual, "mixamorig:Hips", new Vector3(0f, 1f, 0f));
            Transform spine = Child(hips, "mixamorig:Spine", new Vector3(0f, .25f, 0f));
            Transform chest = Child(spine, "mixamorig:Spine01", new Vector3(0f, .22f, 0f));
            Child(chest, "mixamorig:Head", new Vector3(0f, .34f, 0f));
            AddArmChain(chest, "Left", -1f, "mixamorig:");
            AddArmChain(chest, "Right", 1f, "mixamorig:");
            AddLegChain(hips, "Left", -1f, "mixamorig:");
            AddLegChain(hips, "Right", 1f, "mixamorig:");
            var motion = actor.AddComponent<AcrobaticMotionPresentation>();
            try
            {
                motion.Initialize(visual, actor);
                Assert.That(motion.HasPoseRig, Is.True);
                motion.SetMotion(AcrobaticMotionKind.Backflip, .25f, Vector3.back);
                SetPrivateField(motion, "motionBlend", 1f);
                InvokePrivate(motion, "ApplyMotion");

                Assert.That(Vector3.Dot(hips.up, Vector3.forward), Is.LessThan(-.98f),
                    "backward travel must produce a backward transverse quarter turn");
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        private static void AddArmChain(Transform parent, string side, float sign, string prefix = "")
        {
            Transform arm = Child(parent, prefix + side + "Arm", new Vector3(.2f * sign, .12f, 0f));
            Transform forearm = Child(arm, prefix + side + "ForeArm", new Vector3(.24f * sign, 0f, 0f));
            Child(forearm, prefix + side + "Hand", new Vector3(.18f * sign, 0f, 0f));
        }

        private static Transform AddLegChain(Transform hips, string side, float sign, string prefix = "")
        {
            Transform thigh = Child(hips, prefix + side + "UpLeg", new Vector3(.14f * sign, -.08f, 0f));
            Transform leg = Child(thigh, prefix + side + "Leg", new Vector3(0f, -.38f, 0f));
            return Child(leg, prefix + side + "Foot", new Vector3(0f, -.38f, .06f));
        }

        private static void InvokePrivate(object target, string methodName)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            target.GetType().GetMethod(methodName, flags).Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            target.GetType().GetField(fieldName, flags).SetValue(target, value);
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }
    }
}
