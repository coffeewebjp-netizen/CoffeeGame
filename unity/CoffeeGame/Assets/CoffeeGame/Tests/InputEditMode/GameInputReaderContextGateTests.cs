using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class GameInputReaderContextGateTests : InputTestFixture
    {
        private GameObject readerObject;
        private Gamepad gamepad;
        private bool hadModePreference;
        private int savedModePreference;

        public override void Setup()
        {
            base.Setup();
            hadModePreference = PlayerPrefs.HasKey(GameInputReader.InputModePlayerPrefsKey);
            savedModePreference = hadModePreference
                ? PlayerPrefs.GetInt(GameInputReader.InputModePlayerPrefsKey)
                : 0;
            gamepad = InputSystem.AddDevice<Gamepad>();
            readerObject = new GameObject("context-gate-reader");
        }

        public override void TearDown()
        {
            if (readerObject != null)
            {
                Object.DestroyImmediate(readerObject);
            }
            RestoreModePreference();
            base.TearDown();
        }

        [Test]
        public void HeldSouth_DoesNotBecomeJumpWhenUiSwitchesToBattle()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectGamepad(reader);
            reader.EnableUI();
            TickReader(reader);

            Press(gamepad.buttonSouth);
            Assert.That(reader.ConfirmPressed, Is.True);

            reader.EnableBattle();
            InputSystem.Update();
            Assert.That(reader.JumpPressed, Is.False,
                "The South press used to confirm/start must not leak into Battle/Jump.");

            Release(gamepad.buttonSouth);
            TickReader(reader);
            Press(gamepad.buttonSouth);
            Assert.That(reader.JumpPressed, Is.True,
                "A fresh South press after release must still trigger Jump.");
        }

        [Test]
        public void HeldSelect_DoesNotRetoggleSettingsAcrossMapSwitch()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectGamepad(reader);
            reader.EnableBattle();
            TickReader(reader);

            Press(gamepad.selectButton);
            Assert.That(reader.SettingsPressed, Is.True);

            reader.EnableUI();
            InputSystem.Update();
            Assert.That(reader.SettingsPressed, Is.False,
                "The Select press used to open settings must not immediately close it.");

            Release(gamepad.selectButton);
            TickReader(reader);
            Press(gamepad.selectButton);
            Assert.That(reader.SettingsPressed, Is.True,
                "A fresh Select press after release must still toggle settings.");
        }

        [Test]
        public void HeldLeftStick_DoesNotMoveSettingsSelectionUntilNeutral()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectGamepad(reader);
            reader.EnableBattle();
            TickReader(reader);

            Set(gamepad.leftStick, Vector2.up);
            Assert.That(reader.Move.y, Is.GreaterThan(0.5f));

            reader.EnableUI();
            InputSystem.Update();
            TickReader(reader);
            Assert.That(reader.Navigate, Is.EqualTo(Vector2.zero),
                "A held movement stick must remain gated after opening settings.");

            Set(gamepad.leftStick, Vector2.zero);
            TickReader(reader);
            Set(gamepad.leftStick, Vector2.up);
            Assert.That(reader.Navigate.y, Is.GreaterThan(0.5f),
                "A fresh stick movement after neutral must navigate settings.");
        }

        [Test]
        public void ControllerProfile_KeepsKeyboardSettingsRecoveryAvailable()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectGamepad(reader);
            reader.EnableBattle();
            TickReader(reader);

            Press(keyboard.tabKey);

            Assert.That(reader.SettingsPressed, Is.True,
                "Tab must remain available when View/Select cannot open settings.");
        }

        [Test]
        public void ControllerProfile_KeepsKeyboardMenuRecoveryAvailable()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            SelectGamepad(reader);
            reader.EnableUI();
            TickReader(reader);

            Press(keyboard.downArrowKey);
            Assert.That(reader.Navigate.y, Is.LessThan(-0.5f));
            Release(keyboard.downArrowKey);
            TickReader(reader);

            Press(keyboard.enterKey);
            Assert.That(reader.ConfirmPressed, Is.True);
            Release(keyboard.enterKey);
            TickReader(reader);

            Press(keyboard.escapeKey);
            Assert.That(reader.CancelPressed, Is.True);
        }

        private static void TickReader(GameInputReader reader)
        {
            reader.RefreshContextSwitchReleaseGate();
        }

        private static void SelectGamepad(GameInputReader reader)
        {
            reader.BeginInputModeSelection();
            Assert.That(
                reader.TrySelectInputMode(InputMode.ControllerGamepad, out string message),
                Is.True,
                message);
        }

        private void RestoreModePreference()
        {
            if (hadModePreference)
            {
                PlayerPrefs.SetInt(GameInputReader.InputModePlayerPrefsKey, savedModePreference);
            }
            else
            {
                PlayerPrefs.DeleteKey(GameInputReader.InputModePlayerPrefsKey);
            }
            PlayerPrefs.Save();
        }
    }
}
