using System.Reflection;
using CoffeeGame.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class Hd2dFacingPolicyTests
    {
        [Test]
        public void ResolveHorizontalFlip_MirrorsLeftFacingSideArtForCameraLeftMovement()
        {
            Assert.That(Hd2dFacingPolicy.ResolveHorizontalFlip(-0.7f, false), Is.True);
        }

        [Test]
        public void ResolveHorizontalFlip_LeavesLeftFacingSideArtUnmirroredForCameraRightMovement()
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

        [TestCase(1f, false)]
        [TestCase(-1f, true)]
        public void RuntimeCameraBillboardAndLeftFacingSideArt_PointAlongCameraRelativeMovement(
            float cameraRightAmount,
            bool expectedFlip)
        {
            // CombatSliceBootstrap places the camera at (0, 5.75, -8.85) and
            // looks at (0, 0.78, 0), giving this actual camera-forward vector.
            UnityEngine.Quaternion rotation = CameraFacingBillboard.ResolveRotation(
                new UnityEngine.Vector3(0f, -4.97f, 8.85f));
            bool flip = Hd2dFacingPolicy.ResolveHorizontalFlip(
                cameraRightAmount,
                !expectedFlip);
            // hero_*_right frames visibly face image-left, so that is their
            // texture-local natural pose. flipX mirrors it to local right.
            UnityEngine.Vector3 localPoseDirection = flip
                ? UnityEngine.Vector3.right
                : UnityEngine.Vector3.left;
            UnityEngine.Vector3 worldPoseDirection = rotation * localPoseDirection;
            UnityEngine.Vector3 planarCameraRight = UnityEngine.Vector3.right;

            Assert.That(flip, Is.EqualTo(expectedFlip));
            Assert.That(
                UnityEngine.Vector3.Dot(worldPoseDirection, planarCameraRight) *
                cameraRightAmount,
                Is.GreaterThan(0.999f),
                "The rendered side pose must point in the same screen-horizontal direction as movement.");
        }

        [TestCase(CharacterAction.Sword, 1f, false, true)]
        [TestCase(CharacterAction.Sword, -1f, true, false)]
        [TestCase(CharacterAction.AirSlash, 1f, false, true)]
        [TestCase(CharacterAction.AirSlash, -1f, true, false)]
        public void HeroRightAuthoredAttackArt_PointsAlongGameplayFacing(
            CharacterAction action,
            float cameraRightAmount,
            bool expectedLocomotionFlip,
            bool expectedAttackFlip)
        {
            var cameraObject = new GameObject("Facing test camera");
            var actorObject = new GameObject("Facing test actor");
            var visualObject = new GameObject("Facing test visual");
            visualObject.transform.SetParent(actorObject.transform, false);

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.rotation = Quaternion.LookRotation(
                    new Vector3(0f, -4.97f, 8.85f));
                var visual = visualObject.AddComponent<DirectionalSpriteCharacterVisual>();

                Assert.That(
                    visual.TryInitialize("Art/HD2D/hero-hd2d", null, 1f, camera),
                    Is.True);

                Vector3 gameplayFacing = camera.transform.right * cameraRightAmount;
                visual.SetFacing(gameplayFacing);
                Assert.That(
                    visual.Renderer.flipX,
                    Is.EqualTo(expectedLocomotionFlip),
                    "Locomotion art must retain its image-left authoring contract.");

                visual.PlayAction(action, 0.34f);

                Assert.That(visual.CurrentAction, Is.EqualTo(action));
                Assert.That(
                    visual.Renderer.flipX,
                    Is.EqualTo(expectedAttackFlip),
                    $"{action} art is authored image-right and must invert the locomotion mirror.");

                Vector3 localAttackDirection = visual.Renderer.flipX
                    ? Vector3.left
                    : Vector3.right;
                Vector3 worldAttackDirection = CameraFacingBillboard.ResolveRotation(
                    camera.transform.forward) * localAttackDirection;
                Assert.That(
                    Vector3.Dot(worldAttackDirection, gameplayFacing.normalized),
                    Is.GreaterThan(0.999f),
                    "The rendered attack must point along the same direction used by combat targeting.");
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(cameraObject);
            }
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

        [Test]
        public void SpinRelease_UsesARestrainedIaiPoseInsteadOfRotatingTheWholeSprite()
        {
            var cameraObject = new GameObject("Iai pose test camera");
            var actorObject = new GameObject("Iai pose test actor");
            var visualObject = new GameObject("Iai pose test visual");
            visualObject.transform.SetParent(actorObject.transform, false);

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -4.97f, 8.85f));
                var visual = visualObject.AddComponent<DirectionalSpriteCharacterVisual>();
                Assert.That(visual.TryInitialize("Art/HD2D/hero-hd2d", null, 1f, camera), Is.True);

                visual.PlayAction(CharacterAction.SpinRelease, IaiCinematicTiming.Duration);
                typeof(DirectionalSpriteCharacterVisual)
                    .GetField("actionElapsed", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(visual, IaiCinematicTiming.Duration * 0.5f);
                typeof(DirectionalSpriteCharacterVisual)
                    .GetMethod("ApplyActionPose", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(visual, null);

                float rotation = Mathf.Abs(Mathf.DeltaAngle(
                    visual.Renderer.transform.localEulerAngles.z,
                    0f));
                Assert.That(rotation, Is.LessThan(8f));
            }
            finally
            {
                Object.DestroyImmediate(actorObject);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
