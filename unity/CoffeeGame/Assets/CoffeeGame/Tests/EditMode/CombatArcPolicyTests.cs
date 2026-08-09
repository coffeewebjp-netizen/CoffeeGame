using CoffeeGame.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Combat.Tests
{
    public sealed class CombatArcPolicyTests
    {
        [Test]
        public void FrontArc_UsesSameThresholdAsReadableOverlay()
        {
            Assert.That(
                CombatArcPolicy.FrontArcHalfAngleDegrees,
                Is.EqualTo(Mathf.Acos(CombatArcPolicy.FrontArcDotThreshold) * Mathf.Rad2Deg)
                    .Within(0.0001f));
            Assert.That(
                CombatArcPolicy.Contains(Vector3.right, Quaternion.Euler(0f, 82f, 0f) * Vector3.right),
                Is.True);
            Assert.That(
                CombatArcPolicy.Contains(Vector3.right, Quaternion.Euler(0f, 90f, 0f) * Vector3.right),
                Is.False);
        }

        [Test]
        public void FrontArc_TreatsCoincidentTargetsAsInside()
        {
            Assert.That(CombatArcPolicy.Contains(Vector3.forward, Vector3.zero), Is.True);
            Assert.That(CombatArcPolicy.Contains(Vector3.zero, Vector3.forward), Is.False);
        }
    }
}
