using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class GameInputReaderInputModeTests : InputTestFixture
    {
        private GameObject readerObject;
        private Keyboard keyboard;
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
            InputSystem.AddDevice<Mouse>();
            readerObject = new GameObject("input-mode-reader");
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
        public void BeginInputModeSelection_DoesNotActivateSavedPreferenceImplicitly()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();

            Assert.That(reader.SelectedInputMode, Is.EqualTo(InputMode.Unselected));
            Assert.That(reader.Context, Is.EqualTo(GameInputContext.InputSelection));
            Assert.That(reader.Actions.bindingMask.HasValue, Is.False);

            Assert.That(
                reader.TrySelectInputMode(InputMode.KeyboardMouse, out string message),
                Is.True,
                message);
            AssertSingleBindingGroup(reader, "Keyboard");

            reader.BeginInputModeSelection();

            Assert.That(reader.SelectedInputMode, Is.EqualTo(InputMode.Unselected));
            Assert.That(reader.PreferredInputModeForSelection, Is.EqualTo(InputMode.KeyboardMouse));
            Assert.That(reader.Actions.bindingMask.HasValue, Is.False);
            Assert.That(
                PlayerPrefs.GetInt(GameInputReader.InputModePlayerPrefsKey),
                Is.EqualTo((int)InputMode.KeyboardMouse),
                "The chosen mode should be persisted only as the next chooser cursor.");
        }

        [Test]
        public void NativeControllerSelection_RequiresAConnectedGamepad()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();

            Assert.That(
                reader.TrySelectInputMode(InputMode.ControllerGamepad, out string message),
                Is.False);
            Assert.That(message, Does.Contain("Gamepadが検出されていません"));
            Assert.That(reader.SelectedInputMode, Is.EqualTo(InputMode.Unselected));
            Assert.That(reader.Actions.bindingMask.HasValue, Is.False);
        }

        [Test]
        public void SteamDesktopCompatibility_IsEnabledOnlyByExplicitSelection()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();

            Assert.That(
                reader.TrySelectInputMode(InputMode.SteamDesktopCompatibility, out string message),
                Is.True,
                message);
            Assert.That(reader.UsesSteamDesktopFallback, Is.True);
            AssertSingleBindingGroup(reader, GameInputReader.SteamDesktopBindingGroup);

            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();
            Press(keyboard.enterKey);
            Assert.That(reader.JumpPressed, Is.True);
            Release(keyboard.enterKey);
            Press(keyboard.pageUpKey);
            Assert.That(reader.SpecialPressed, Is.True);
        }

        [Test]
        public void GamepadProfile_RejectsKeyboardBindings()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();

            Assert.That(
                reader.TrySelectInputMode(InputMode.ControllerGamepad, out string message),
                Is.True,
                message);
            AssertSingleBindingGroup(reader, GameInputReader.GamepadBindingGroup);

            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();
            Press(keyboard.spaceKey);
            Assert.That(reader.JumpPressed, Is.False);
            Release(keyboard.spaceKey);
            Press(gamepad.buttonSouth);
            Assert.That(reader.JumpPressed, Is.True);
        }

        [Test]
        public void KeyboardMouseProfile_RejectsDesktopOnlyBindingsAndRebinding()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();
            Assert.That(
                reader.TrySelectInputMode(InputMode.KeyboardMouse, out string message),
                Is.True,
                message);
            Assert.That(reader.PreferredRebindBindingGroup, Is.Empty);
            AssertSingleBindingGroup(reader, "Keyboard");

            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();
            Press(keyboard.enterKey);
            Assert.That(reader.JumpPressed, Is.False);
            Release(keyboard.enterKey);
            Press(keyboard.spaceKey);
            Assert.That(reader.JumpPressed, Is.True);
            Release(keyboard.spaceKey);
            Press(keyboard.wKey);
            Assert.That(reader.Move.y, Is.GreaterThan(0.5f));

            int keyboardJumpIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Jump, "Keyboard");
            Assert.That(reader.TryStartInteractiveRebind(GameInputSemantic.Jump, keyboardJumpIndex), Is.False);
            Assert.That(reader.LastRebindMessage, Does.Contain("現在固定"));
        }

        private static void AssertSingleBindingGroup(GameInputReader reader, string expectedGroup)
        {
            Assert.That(reader.Actions.bindingMask.HasValue, Is.True);
            Assert.That(reader.Actions.bindingMask.Value.groups, Is.EqualTo(expectedGroup));
        }
    }
}
