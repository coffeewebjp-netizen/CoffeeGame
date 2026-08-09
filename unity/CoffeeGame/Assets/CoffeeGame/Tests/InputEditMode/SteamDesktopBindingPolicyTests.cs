using NUnit.Framework;

namespace CoffeeGame.Input.Tests
{
    public sealed class SteamDesktopBindingPolicyTests
    {
        [TestCase("<Keyboard>/enter")]
        [TestCase("<Keyboard>/space")]
        [TestCase("<Keyboard>/pageUp")]
        [TestCase("<Keyboard>/pageDown")]
        [TestCase("<Keyboard>/home")]
        [TestCase("<Mouse>/leftButton")]
        [TestCase("<Mouse>/rightButton")]
        public void DesktopButtons_AreAccepted(string path)
        {
            Assert.That(SteamDesktopBindingPolicy.IsBindableDesktopPath(path), Is.True);
        }

        [TestCase("<Keyboard>/escape")]
        [TestCase("<Keyboard>/tab")]
        [TestCase("<Keyboard>/upArrow")]
        [TestCase("<Keyboard>/w")]
        [TestCase("<Keyboard>/f")]
        [TestCase("<Gamepad>/buttonSouth")]
        [TestCase("<Mouse>/scroll")]
        public void MenuMovementAndLegacyKeyboardControls_AreRejected(string path)
        {
            Assert.That(SteamDesktopBindingPolicy.IsBindableDesktopPath(path), Is.False);
        }

        [Test]
        public void SameDesktopControl_IsCaseInsensitiveButDeviceSpecific()
        {
            Assert.That(
                SteamDesktopBindingPolicy.AreSameControl(
                    "<Keyboard>/pageUp",
                    "<KEYBOARD>/PAGEUP"),
                Is.True);
            Assert.That(
                SteamDesktopBindingPolicy.AreSameControl(
                    "<Keyboard>/enter",
                    "<Mouse>/leftButton"),
                Is.False);
        }
    }
}
