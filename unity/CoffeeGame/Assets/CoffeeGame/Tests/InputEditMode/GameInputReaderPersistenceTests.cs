using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoffeeGame.Input.Tests
{
    public sealed class GameInputReaderPersistenceTests
    {
        private const string CurrentPrefsKey = "CoffeeGame.Input.GamepadBindings.v2";
        private const string DesktopPrefsKey = "CoffeeGame.Input.SteamDesktopBindings.v1";
        private const string LegacyPrefsKey = "CoffeeGame.Input.BindingOverrides.v1";

        private GameObject firstObject;
        private GameObject secondObject;
        private bool hadCurrentPrefs;
        private bool hadLegacyPrefs;
        private bool hadDesktopPrefs;
        private string currentPrefsValue;
        private string desktopPrefsValue;
        private string legacyPrefsValue;

        [SetUp]
        public void SetUp()
        {
            hadCurrentPrefs = PlayerPrefs.HasKey(CurrentPrefsKey);
            hadLegacyPrefs = PlayerPrefs.HasKey(LegacyPrefsKey);
            hadDesktopPrefs = PlayerPrefs.HasKey(DesktopPrefsKey);
            currentPrefsValue = hadCurrentPrefs ? PlayerPrefs.GetString(CurrentPrefsKey) : null;
            desktopPrefsValue = hadDesktopPrefs ? PlayerPrefs.GetString(DesktopPrefsKey) : null;
            legacyPrefsValue = hadLegacyPrefs ? PlayerPrefs.GetString(LegacyPrefsKey) : null;
        }

        [TearDown]
        public void TearDown()
        {
            if (firstObject != null)
            {
                Object.DestroyImmediate(firstObject);
            }
            if (secondObject != null)
            {
                Object.DestroyImmediate(secondObject);
            }

            RestorePlayerPrefs(CurrentPrefsKey, hadCurrentPrefs, currentPrefsValue);
            RestorePlayerPrefs(DesktopPrefsKey, hadDesktopPrefs, desktopPrefsValue);
            RestorePlayerPrefs(LegacyPrefsKey, hadLegacyPrefs, legacyPrefsValue);
            PlayerPrefs.Save();
        }

        [Test]
        public void SemanticSave_RestoresAcrossFreshRuntimeActionIds()
        {
            GameInputReader first = CreateReader("first", out firstObject);
            int firstJumpIndex = first.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.GamepadBindingGroup);
            int firstSwordIndex = first.GetBindingIndexForGroup(GameInputSemantic.Sword, GameInputReader.GamepadBindingGroup);
            first.Actions.FindAction("Battle/Jump", true).ApplyBindingOverride(firstJumpIndex, "<Gamepad>/buttonWest");
            first.Actions.FindAction("Battle/Sword", true).ApplyBindingOverride(firstSwordIndex, "<Gamepad>/buttonSouth");
            string json = first.SaveBindingOverridesAsJson();

            GameInputReader second = CreateReader("second", out secondObject);
            second.LoadBindingOverridesFromJson(json);

            int secondJumpIndex = second.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.GamepadBindingGroup);
            int secondSwordIndex = second.GetBindingIndexForGroup(GameInputSemantic.Sword, GameInputReader.GamepadBindingGroup);
            Assert.That(second.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, secondJumpIndex), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(second.GetBindingEffectivePathAtIndex(GameInputSemantic.Sword, secondSwordIndex), Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void Reset_RemovesRuntimeOverrides()
        {
            GameInputReader reader = CreateReader("reset", out firstObject);
            int jumpIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.GamepadBindingGroup);
            InputAction jump = reader.Actions.FindAction("Battle/Jump", true);
            jump.ApplyBindingOverride(jumpIndex, "<Gamepad>/buttonWest");

            reader.ResetBindingOverrides();

            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, jumpIndex), Is.EqualTo("<Gamepad>/buttonSouth"));
        }

        [Test]
        public void Load_RejectsDuplicateCanonicalButtonAcrossDeviceLayouts()
        {
            GameInputReader reader = CreateReader("canonical-duplicate", out firstObject);
            const string json =
                "{\"version\":2,\"bindings\":[" +
                "{\"semantic\":\"Jump\",\"path\":\"<Gamepad>/buttonWest\"}," +
                "{\"semantic\":\"Sword\",\"path\":\"<XInputControllerWindows>/buttonWest\"}]}";

            reader.LoadBindingOverridesFromJson(json);

            int jumpIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.GamepadBindingGroup);
            int swordIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Sword, GameInputReader.GamepadBindingGroup);
            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, jumpIndex), Is.EqualTo("<Gamepad>/buttonWest"));
            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Sword, swordIndex), Is.EqualTo("<Gamepad>/rightTrigger"));
        }

        [Test]
        public void Load_AcceptsEastFaceButtonFromVirtualXInputLayout()
        {
            GameInputReader reader = CreateReader("east-face", out firstObject);
            const string json =
                "{\"version\":2,\"bindings\":[" +
                "{\"semantic\":\"Jump\",\"path\":\"<XInputControllerWindows>/buttonEast\"}]}";

            reader.LoadBindingOverridesFromJson(json);

            int jumpIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.GamepadBindingGroup);
            Assert.That(
                reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, jumpIndex),
                Is.EqualTo("<XInputControllerWindows>/buttonEast"));
        }

        [Test]
        public void SteamDesktopSave_RestoresSeparatelyFromGamepadProfile()
        {
            GameInputReader first = CreateReader("desktop-first", out firstObject);
            int desktopJumpIndex = first.GetBindingIndexForGroup(
                GameInputSemantic.Jump,
                GameInputReader.SteamDesktopBindingGroup);
            int gamepadJumpIndex = first.GetBindingIndexForGroup(
                GameInputSemantic.Jump,
                GameInputReader.GamepadBindingGroup);
            first.Actions.FindAction("Battle/Jump", true)
                .ApplyBindingOverride(desktopJumpIndex, "<Keyboard>/home");
            string json = first.SaveSteamDesktopBindingOverridesAsJson();

            GameInputReader second = CreateReader("desktop-second", out secondObject);
            second.LoadSteamDesktopBindingOverridesFromJson(json);

            int restoredDesktopIndex = second.GetBindingIndexForGroup(
                GameInputSemantic.Jump,
                GameInputReader.SteamDesktopBindingGroup);
            int restoredGamepadIndex = second.GetBindingIndexForGroup(
                GameInputSemantic.Jump,
                GameInputReader.GamepadBindingGroup);
            Assert.That(
                second.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, restoredDesktopIndex),
                Is.EqualTo("<Keyboard>/home"));
            Assert.That(
                second.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, restoredGamepadIndex),
                Is.EqualTo(first.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, gamepadJumpIndex)));
        }

        [Test]
        public void SteamDesktopLoad_RejectsReservedAndDuplicateControls()
        {
            GameInputReader reader = CreateReader("desktop-invalid", out firstObject);
            const string json =
                "{\"version\":1,\"bindings\":[" +
                "{\"semantic\":\"Jump\",\"path\":\"<Keyboard>/home\"}," +
                "{\"semantic\":\"Sword\",\"path\":\"<Keyboard>/home\"}," +
                "{\"semantic\":\"Special\",\"path\":\"<Keyboard>/tab\"}]}";

            reader.LoadSteamDesktopBindingOverridesFromJson(json);

            int jumpIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Jump, GameInputReader.SteamDesktopBindingGroup);
            int swordIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Sword, GameInputReader.SteamDesktopBindingGroup);
            int specialIndex = reader.GetBindingIndexForGroup(GameInputSemantic.Special, GameInputReader.SteamDesktopBindingGroup);
            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, jumpIndex), Is.EqualTo("<Keyboard>/home"));
            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Sword, swordIndex), Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Special, specialIndex), Is.EqualTo("<Keyboard>/pageUp"));
        }

        [Test]
        public void SteamDesktopLoad_AcceptsSpaceEmittedBySteamB()
        {
            GameInputReader reader = CreateReader("desktop-space", out firstObject);
            const string json =
                "{\"version\":1,\"bindings\":[" +
                "{\"semantic\":\"Jump\",\"path\":\"<Keyboard>/space\"}]}";

            reader.LoadSteamDesktopBindingOverridesFromJson(json);

            int jumpIndex = reader.GetBindingIndexForGroup(
                GameInputSemantic.Jump,
                GameInputReader.SteamDesktopBindingGroup);
            Assert.That(
                reader.GetBindingEffectivePathAtIndex(GameInputSemantic.Jump, jumpIndex),
                Is.EqualTo("<Keyboard>/space"));
        }

        private static GameInputReader CreateReader(string name, out GameObject gameObject)
        {
            gameObject = new GameObject(name);
            gameObject.SetActive(false);
            return gameObject.AddComponent<GameInputReader>();
        }

        private static void RestorePlayerPrefs(string key, bool existed, string value)
        {
            if (existed)
            {
                PlayerPrefs.SetString(key, value ?? string.Empty);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
