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
    }
}
