using NUnit.Framework;

namespace CoffeeGame.Input.Tests
{
    public sealed class GamepadBindingPolicyTests
    {
        [TestCase("<Gamepad>/buttonSouth")]
        [TestCase("<Gamepad>/buttonEast")]
        [TestCase("<XInputControllerWindows>/buttonEast")]
        [TestCase("<Gamepad>/buttonWest")]
        [TestCase("<Gamepad>/buttonNorth")]
        [TestCase("<Gamepad>/rightTrigger")]
        [TestCase("<XInputControllerWindows>/leftShoulder")]
        public void BindableButtons_AreAccepted(string path)
        {
            Assert.That(GamepadBindingPolicy.IsBindableGamepadPath(path), Is.True);
        }

        [TestCase("<Gamepad>/start")]
        [TestCase("<Gamepad>/select")]
        [TestCase("<Gamepad>/startButton")]
        [TestCase("<Gamepad>/selectButton")]
        [TestCase("<Gamepad>/dpad/up")]
        [TestCase("<Gamepad>/leftStick/x")]
        public void MenuAndMovementControls_AreNotAccepted(string path)
        {
            Assert.That(GamepadBindingPolicy.IsBindableGamepadPath(path), Is.False);
        }

        [Test]
        public void SameCanonicalControl_MatchesAcrossVirtualLayouts()
        {
            Assert.That(
                GamepadBindingPolicy.AreSameControl(
                    "<Gamepad>/buttonWest",
                    "<XInputControllerWindows>/buttonWest"),
                Is.True);
        }
    }
}
