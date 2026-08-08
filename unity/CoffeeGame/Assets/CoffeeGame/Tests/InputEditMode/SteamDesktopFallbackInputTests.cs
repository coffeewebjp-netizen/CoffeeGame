using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class SteamDesktopFallbackInputTests : InputTestFixture
    {
        private GameObject readerObject;
        private Keyboard keyboard;
        private Mouse mouse;
        private bool hadModePreference;
        private int savedModePreference;

        public override void Setup()
        {
            base.Setup();
            hadModePreference = PlayerPrefs.HasKey(GameInputReader.InputModePlayerPrefsKey);
            savedModePreference = hadModePreference
                ? PlayerPrefs.GetInt(GameInputReader.InputModePlayerPrefsKey)
                : 0;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            readerObject = new GameObject("steam-desktop-reader");
        }

        public override void TearDown()
        {
            if (readerObject != null)
            {
                Object.DestroyImmediate(readerObject);
            }
            if (hadModePreference)
            {
                PlayerPrefs.SetInt(GameInputReader.InputModePlayerPrefsKey, savedModePreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(GameInputReader.InputModePlayerPrefsKey);
            }
            PlayerPrefs.Save();
            base.TearDown();
        }

        [Test]
        public void DesktopDefaults_TriggerBattleActionsWithoutGamepad()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectMode(reader, InputMode.SteamDesktopCompatibility);
            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();

            Assert.That(reader.UsesSteamDesktopFallback, Is.True);
            Assert.That(reader.PreferredRebindBindingGroup, Is.EqualTo(GameInputReader.SteamDesktopBindingGroup));

            Press(keyboard.enterKey);
            Assert.That(reader.JumpPressed, Is.True, "Steam A/Enter must jump.");
            Release(keyboard.enterKey);

            Press(keyboard.pageUpKey);
            Assert.That(reader.SpecialPressed, Is.True, "Steam X/PageUp must use spin attack.");
            Release(keyboard.pageUpKey);

            Press(keyboard.pageDownKey);
            Assert.That(reader.MagicPressed, Is.True, "Steam Y/PageDown must use ice magic.");
            Release(keyboard.pageDownKey);

            Press(mouse.leftButton);
            Assert.That(reader.SwordPressed, Is.True, "Steam RT/Mouse Left must use the sword.");
        }

        [Test]
        public void DesktopUi_UsesEnterToConfirmSpaceToCancelAndTabForSettings()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectMode(reader, InputMode.SteamDesktopCompatibility);
            reader.EnableUI();
            reader.RefreshContextSwitchReleaseGate();

            Press(keyboard.enterKey);
            Assert.That(reader.ConfirmPressed, Is.True);
            Release(keyboard.enterKey);

            Press(keyboard.spaceKey);
            Assert.That(reader.CancelPressed, Is.True);
            Assert.That(reader.ConfirmPressed, Is.False);
            Release(keyboard.spaceKey);

            Press(keyboard.tabKey);
            Assert.That(reader.SettingsPressed, Is.True);
        }

        [Test]
        public void KeyboardMouseMode_KeepsOriginalCombatBindingsAndMouseSword()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectMode(reader, InputMode.KeyboardMouse);
            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();

            Press(keyboard.spaceKey);
            Assert.That(reader.JumpPressed, Is.True);
            Release(keyboard.spaceKey);

            Press(keyboard.fKey);
            Assert.That(reader.SwordPressed, Is.True);
            Release(keyboard.fKey);

            Press(keyboard.qKey);
            Assert.That(reader.SpecialPressed, Is.True);
            Release(keyboard.qKey);

            Press(keyboard.eKey);
            Assert.That(reader.MagicPressed, Is.True);
            Release(keyboard.eKey);

            Press(mouse.leftButton);
            Assert.That(reader.SwordPressed, Is.True);
        }

        private static void SelectMode(GameInputReader reader, InputMode mode)
        {
            reader.BeginInputModeSelection();
            Assert.That(reader.TrySelectInputMode(mode, out string message), Is.True, message);
        }
    }
}
