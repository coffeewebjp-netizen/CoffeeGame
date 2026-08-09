using NUnit.Framework;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class Hd2dFacingPolicyTests
    {
        [Test]
        public void ResolveHorizontalFlip_PointsLeftWhenTargetIsCameraLeft()
        {
            Assert.That(Hd2dFacingPolicy.ResolveHorizontalFlip(-0.7f, true), Is.False);
        }

        [Test]
        public void ResolveHorizontalFlip_PointsRightWhenTargetIsCameraRight()
        {
            Assert.That(Hd2dFacingPolicy.ResolveHorizontalFlip(0.7f, false), Is.True);
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

        [TestCase(0f, 0f, 1f)]
        [TestCase(1f, 0f, 0f)]
        [TestCase(0.4f, 0.8f, 0.6f)]
        public void BillboardRotation_FacesSpriteFrontTowardCamera(
            float x,
            float y,
            float z)
        {
            var cameraForward = new UnityEngine.Vector3(x, y, z);
            UnityEngine.Quaternion rotation = CameraFacingBillboard.ResolveRotation(cameraForward);
            UnityEngine.Vector3 billboardForward = rotation * UnityEngine.Vector3.forward;
            var planarCameraForward = new UnityEngine.Vector3(x, 0f, z).normalized;

            Assert.That(
                UnityEngine.Vector3.Dot(billboardForward, planarCameraForward),
                Is.LessThan(-0.999f),
                "The sprite front must point back toward the camera, not away from it.");
        }

        [TestCase(1f, true)]
        [TestCase(-1f, false)]
        public void BillboardAndFlip_PointSidePoseAlongCameraRelativeMovement(
            float cameraRightAmount,
            bool expectedFlip)
        {
            UnityEngine.Quaternion rotation = CameraFacingBillboard.ResolveRotation(
                UnityEngine.Vector3.forward);
            bool flip = Hd2dFacingPolicy.ResolveHorizontalFlip(
                cameraRightAmount,
                !expectedFlip);
            UnityEngine.Vector3 localPoseDirection = flip
                ? UnityEngine.Vector3.left
                : UnityEngine.Vector3.right;
            UnityEngine.Vector3 worldPoseDirection = rotation * localPoseDirection;

            Assert.That(flip, Is.EqualTo(expectedFlip));
            Assert.That(
                UnityEngine.Vector3.Dot(worldPoseDirection, UnityEngine.Vector3.right) *
                cameraRightAmount,
                Is.GreaterThan(0.999f),
                "The rendered side pose must point in the same camera-relative direction as movement.");
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
