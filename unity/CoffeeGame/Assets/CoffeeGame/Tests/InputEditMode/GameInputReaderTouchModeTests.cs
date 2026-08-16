using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class GameInputReaderTouchModeTests : InputTestFixture
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
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Touchscreen>();
            readerObject = new GameObject("touch-mode-reader");
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
        public void TouchMode_AcceptsVirtualStickAndActionButtons()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();

            Assert.That(
                reader.TrySelectInputMode(InputMode.TouchOnScreen, out string message),
                Is.True,
                message);
            Assert.That(reader.SelectedInputMode, Is.EqualTo(InputMode.TouchOnScreen));
            Assert.That(reader.UsesTouchOverlay, Is.True);
            Assert.That(reader.ActiveInputProfileName, Is.EqualTo("タッチ（画面操作）"));

            reader.EnableBattle();
            reader.RefreshContextSwitchReleaseGate();
            reader.SetTouchMove(new Vector2(0.8f, -0.2f));
            reader.QueueTouchPress(GameInputSemantic.Jump);
            reader.QueueTouchPress(GameInputSemantic.Sword);

            Assert.That(reader.Move.x, Is.EqualTo(0.8f).Within(0.02f));
            Assert.That(reader.JumpPressed, Is.True);
            Assert.That(reader.SwordPressed, Is.True);

            reader.ClearQueuedTouchPresses();
            Assert.That(reader.JumpPressed, Is.False);
            Assert.That(reader.SwordPressed, Is.False);
        }

        [Test]
        public void SwipeAndHold_KeepsMoveAfterAShortDrag()
        {
            Vector2 origin = new Vector2(200f, 200f);
            Assert.That(TouchOverlayMath.ResolveHoldMove(origin, origin), Is.EqualTo(Vector2.zero));
            Vector2 held = TouchOverlayMath.ResolveHoldMove(origin, origin + new Vector2(80f, 0f));
            Assert.That(held.x, Is.GreaterThan(0.5f));
            Assert.That(held.y, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void TouchMode_DoesNotRequireAGamepad()
        {
            GameInputReader reader = readerObject.AddComponent<GameInputReader>();
            reader.BeginInputModeSelection();
            Assert.That(reader.HasConnectedGamepad, Is.False);
            Assert.That(
                reader.TrySelectInputMode(InputMode.TouchOnScreen, out string message),
                Is.True,
                message);
        }
    }
}
