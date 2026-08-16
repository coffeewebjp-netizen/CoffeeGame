using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class GameInputReaderCameraOrbitTests : InputTestFixture
    {
        private GameObject readerObject;
        private bool hadModePreference;
        private int savedModePreference;

        public override void Setup()
        {
            base.Setup();
            hadModePreference = PlayerPrefs.HasKey(GameInputReader.InputModePlayerPrefsKey);
            savedModePreference = hadModePreference
                ? PlayerPrefs.GetInt(GameInputReader.InputModePlayerPrefsKey)
                : 0;
            readerObject = new GameObject("camera-orbit-reader");
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
        public void RightStickControlsYawAndPitchOnlyInBattleContext()
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();
            Assert.That(reader.TrySelectInputMode(InputMode.ControllerGamepad, out string message), Is.True, message);
            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();

            Set(gamepad.rightStick, new Vector2(0.8f, 0.9f));
            Assert.That(reader.CameraYaw, Is.GreaterThan(0.5f));
            Assert.That(reader.CameraPitch, Is.GreaterThan(0.5f));

            Set(gamepad.rightStick, Vector2.zero);
            reader.EnableUI();
            InputSystem.Update();
            reader.RefreshContextSwitchReleaseGate();
            Set(gamepad.rightStick, new Vector2(0.8f, 0.9f));
            Assert.That(reader.CameraYaw, Is.Zero);
            Assert.That(reader.CameraPitch, Is.Zero);
        }

        [Test]
        public void ZCAndRVProvideKeyboardCameraOrbit()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();
            Assert.That(reader.TrySelectInputMode(InputMode.KeyboardMouse, out string message), Is.True, message);
            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();

            Press(keyboard.cKey);
            Assert.That(reader.CameraYaw, Is.GreaterThan(0.5f));
            Release(keyboard.cKey);
            Press(keyboard.zKey);
            Assert.That(reader.CameraYaw, Is.LessThan(-0.5f));
            Release(keyboard.zKey);
            Press(keyboard.rKey);
            Assert.That(reader.CameraPitch, Is.GreaterThan(0.5f));
            Release(keyboard.rKey);
            Press(keyboard.vKey);
            Assert.That(reader.CameraPitch, Is.LessThan(-0.5f));
        }

        [Test]
        public void RightMouseDragIsIgnoredOutsideBattle()
        {
            InputSystem.AddDevice<Keyboard>();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();
            Assert.That(reader.TrySelectInputMode(InputMode.KeyboardMouse, out string message), Is.True, message);
            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();

            Press(mouse.rightButton);
            Set(mouse.delta, new Vector2(18f, -12f));
            Assert.That(reader.CameraPointerDelta.x, Is.GreaterThan(10f));
            Assert.That(reader.CameraPointerDelta.y, Is.LessThan(-10f));

            reader.EnableUI();
            InputSystem.Update();
            Assert.That(reader.CameraPointerDelta, Is.EqualTo(Vector2.zero));
        }
    }
}
