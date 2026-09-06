using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Input.Tests
{
    public sealed class TouchGestureRouterTests
    {
        private readonly TouchControlLayout layout = new TouchControlLayout(960, 540, new Rect(0, 0, 960, 540));
        private Vector2 Button(GameInputSemantic action) => layout.Buttons.Single(b => b.Action == action).Bounds.center;
        [TestCase(640, 360, 0, 0)] [TestCase(1280, 720, 0, 0)] [TestCase(1920, 1080, 0, 0)]
        [TestCase(2400, 1080, 80, 32)] [TestCase(2436, 1125, 132, 63)] [TestCase(2048, 1536, 0, 20)]
        public void ButtonsFitTheSafeAreaAndNeverOverlap(int width, int height, int insetX, int insetY)
        {
            var l = new TouchControlLayout(width, height, new Rect(insetX, insetY, width - 2 * insetX, height - 2 * insetY));
            Assert.That(l.Buttons.Select(b => b.Action).Distinct().Count(), Is.EqualTo(8));
            for (int i = 0; i < l.Buttons.Length; i++)
            {
                var r = l.Buttons[i].Bounds;
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(l.SafeArea.center.x));
                Assert.That(r.xMax, Is.LessThanOrEqualTo(l.SafeArea.xMax));
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(l.SafeArea.yMin));
                Assert.That(r.yMax, Is.LessThanOrEqualTo(l.SafeArea.yMax));
                Assert.That(r.width, Is.GreaterThanOrEqualTo(48));
                for (int j = i + 1; j < l.Buttons.Length; j++) Assert.That(r.Overlaps(l.Buttons[j].Bounds), Is.False);
            }
            Assert.That(l.SafeArea.Contains(l.StickCenter), Is.True);
            Assert.That(l.IsMoveZone(l.StickCenter), Is.True);
        }
        [Test]
        public void MoveGuardAttackAndCameraCanBeHeldByIndependentFingers()
        {
            var r = new TouchGestureRouter();
            r.BeginFrame(); r.Process(1, new Vector2(100, 120), true, layout); r.Process(2, Button(GameInputSemantic.Guard), true, layout);
            r.Process(3, Button(GameInputSemantic.Sword), true, layout); r.Process(4, new Vector2(700, 440), true, layout); r.EndFrame();
            CollectionAssert.AreEquivalent(new[] { GameInputSemantic.Guard, GameInputSemantic.Sword }, r.Presses);
            r.BeginFrame(); r.Process(1, new Vector2(180, 120), false, layout); r.Process(2, Button(GameInputSemantic.Guard), false, layout);
            r.Process(3, Button(GameInputSemantic.Sword), false, layout); r.Process(4, new Vector2(742, 440), false, layout); r.EndFrame();
            Assert.That(r.Move.x, Is.EqualTo(1).Within(.01f)); Assert.That(r.Camera.x, Is.EqualTo(1).Within(.01f));
            Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.True); Assert.That(r.Presses, Is.Empty);
        }
        [Test]
        public void CrossingButtonsDoesNotChangeTheFingerRole()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, new Vector2(700, 440), true, layout); r.EndFrame();
            r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Sword), false, layout); r.EndFrame();
            Assert.That(r.Presses, Is.Empty); Assert.That(r.IsHeld(GameInputSemantic.Sword), Is.False);
            r.Reset(); r.BeginFrame(); r.Process(2, layout.StickCenter, true, layout); r.EndFrame();
            r.BeginFrame(); r.Process(2, Button(GameInputSemantic.Guard), false, layout); r.EndFrame();
            Assert.That(r.Presses, Is.Empty); Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.False); Assert.That(r.Move.magnitude, Is.GreaterThan(.9f));
        }
        [Test]
        public void GuardFingerDoesNotBecomeAnAttackWhenItSlidesAway()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Guard), true, layout); r.EndFrame();
            r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Sword), false, layout); r.EndFrame();
            Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.True); Assert.That(r.Presses, Is.Empty);
            r.BeginFrame(); r.EndFrame(); Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.False);
        }
        [Test]
        public void MenuOrFocusResetRequiresFreshTouchDown()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Jump), true, layout); r.EndFrame();
            r.Reset(); r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Jump), false, layout); r.EndFrame();
            Assert.That(r.Presses, Is.Empty); Assert.That(r.IsHeld(GameInputSemantic.Jump), Is.False);
            r.BeginFrame(); r.EndFrame(); r.BeginFrame(); r.Process(2, Button(GameInputSemantic.Jump), true, layout); r.EndFrame();
            CollectionAssert.Contains(r.Presses, GameInputSemantic.Jump);
        }
        [Test]
        public void UiTouchCannotLeakIntoMovementOrCamera()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, layout.StickCenter, true, layout, true); r.EndFrame();
            r.BeginFrame(); r.Process(1, Button(GameInputSemantic.Special), false, layout); r.EndFrame();
            Assert.That(r.Move, Is.EqualTo(Vector2.zero)); Assert.That(r.Camera, Is.EqualTo(Vector2.zero)); Assert.That(r.Presses, Is.Empty);
        }
        [Test]
        public void ReleaseLetsANewFingerAcquireTheStickInTheSameFrame()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, layout.StickCenter, true, layout); r.EndFrame();
            r.BeginFrame(); r.Release(1); r.Process(2, layout.StickCenter, true, layout); r.EndFrame();
            r.BeginFrame(); r.Process(2, layout.StickCenter + Vector2.up * 70, false, layout); r.EndFrame();
            Assert.That(r.Move.y, Is.GreaterThan(.9f));
        }
        [Test]
        public void LockAndSwitchAreOnePressPerTouch()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, Button(GameInputSemantic.LockOn), true, layout); r.Process(2, Button(GameInputSemantic.SwitchCharacter), true, layout); r.EndFrame();
            CollectionAssert.AreEquivalent(new[] { GameInputSemantic.LockOn, GameInputSemantic.SwitchCharacter }, r.Presses);
            r.BeginFrame(); r.Process(1, Button(GameInputSemantic.LockOn), false, layout); r.Process(2, Button(GameInputSemantic.SwitchCharacter), false, layout); r.EndFrame();
            Assert.That(r.Presses, Is.Empty);
        }
        [TestCase(30, 1f)] [TestCase(60, 2f)] [TestCase(120, 2.5f)]
        public void SameCameraSwipeTurnsTheSameAngleAcrossScreensAndFrameRates(int frames, float scale)
        {
            var l = new TouchControlLayout(1200 * scale, 540 * scale, new Rect(0, 0, 1200 * scale, 540 * scale));
            var r = new TouchGestureRouter(); Vector2 origin = new Vector2(650, 440) * scale;
            r.BeginFrame(); r.Process(1, origin, true, l); r.EndFrame(); float angle = 0;
            for (int frame = 1; frame <= frames; frame++)
            {
                r.BeginFrame(); r.Process(1, origin + Vector2.right * (450 * scale * frame / frames), false, l); r.EndFrame();
                angle += TouchOverlayMath.ResolveCameraOrbit(r.Camera).x;
            }
            Assert.That(angle, Is.EqualTo(81).Within(.01f));
            Assert.That(r.Presses, Is.Empty);
        }
        [Test]
        public void RecycledIdWithANewTouchDownAcquiresANewRole()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(2, Button(GameInputSemantic.Guard), true, layout); r.EndFrame();
            r.BeginFrame(); r.Process(2, Button(GameInputSemantic.Sword), true, layout); r.EndFrame();
            Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.False);
            CollectionAssert.Contains(r.Presses, GameInputSemantic.Sword);
        }
        [Test]
        public void LiveIdsKeepOwnershipAndReleaseMissingOwnersBeforeAcquisition()
        {
            var r = new TouchGestureRouter(); r.BeginFrame(); r.Process(1, layout.StickCenter, true, layout); r.Process(2, Button(GameInputSemantic.Guard), true, layout); r.EndFrame();
            r.BeginFrame(); r.ReleaseMissingTouches(new HashSet<int> { 2, 3 });
            r.Process(2, Button(GameInputSemantic.Guard), false, layout); r.Process(3, layout.StickCenter, true, layout); r.EndFrame();
            Assert.That(r.IsHeld(GameInputSemantic.Guard), Is.True); Assert.That(r.HasMoveFinger, Is.True); Assert.That(r.Presses, Is.Empty);
        }
    }
}
