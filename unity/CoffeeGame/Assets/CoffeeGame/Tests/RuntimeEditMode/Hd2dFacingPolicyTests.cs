using NUnit.Framework;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class Hd2dFacingPolicyTests
    {
        [Test]
        public void ResolveHorizontalFlip_PointsLeftWhenTargetIsCameraLeft()
        {
            Assert.That(Hd2dFacingPolicy.ResolveHorizontalFlip(-0.7f, false), Is.True);
        }

        [Test]
        public void ResolveHorizontalFlip_PointsRightWhenTargetIsCameraRight()
        {
            Assert.That(Hd2dFacingPolicy.ResolveHorizontalFlip(0.7f, true), Is.False);
        }

        [TestCase(0.02f, true)]
        [TestCase(-0.02f, false)]
        public void ResolveHorizontalFlip_InsideDeadZoneRetainsPreviousFacing(
            float horizontalAmount,
            bool previous)
        {
            Assert.That(
                Hd2dFacingPolicy.ResolveHorizontalFlip(horizontalAmount, previous),
                Is.EqualTo(previous));
        }

        [TestCase(1f, 0f, Hd2dFacingDirection.Up)]
        [TestCase(-1f, 0f, Hd2dFacingDirection.Down)]
        [TestCase(0f, 1f, Hd2dFacingDirection.Side)]
        [TestCase(0f, -1f, Hd2dFacingDirection.Side)]
        public void ResolveDirection_SelectsCameraRelativeAxis(
            float forward,
            float side,
            Hd2dFacingDirection expected)
        {
            Assert.That(
                Hd2dFacingPolicy.ResolveDirection(
                    forward,
                    side,
                    Hd2dFacingDirection.Down),
                Is.EqualTo(expected));
        }

        [Test]
        public void ResolveDirection_NearBoundaryRetainsSideOrAxialFamily()
        {
            Assert.That(
                Hd2dFacingPolicy.ResolveDirection(0.72f, 1f, Hd2dFacingDirection.Side),
                Is.EqualTo(Hd2dFacingDirection.Side));
            Assert.That(
                Hd2dFacingPolicy.ResolveDirection(0.72f, 1f, Hd2dFacingDirection.Up),
                Is.EqualTo(Hd2dFacingDirection.Up));
        }

        [TestCase(CharacterAction.Jump, CharacterAction.Fall)]
        [TestCase(CharacterAction.Jump, CharacterAction.Land)]
        [TestCase(CharacterAction.Fall, CharacterAction.Land)]
        [TestCase(CharacterAction.Plunge, CharacterAction.Land)]
        [TestCase(CharacterAction.AirSlash, CharacterAction.Land)]
        [TestCase(CharacterAction.Hurt, CharacterAction.Land)]
        public void PhysicsTransition_OverridesAnIndefiniteAirPose(
            CharacterAction current,
            CharacterAction next)
        {
            Assert.That(
                CharacterVisualTransitionPolicy.IsForcedPhysicsTransition(current, next),
                Is.True);
        }
    }
}
