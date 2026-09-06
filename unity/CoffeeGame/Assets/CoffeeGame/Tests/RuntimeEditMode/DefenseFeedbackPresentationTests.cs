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

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child.transform;
        }
    }
}
