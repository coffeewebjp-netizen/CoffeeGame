using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace CoffeeGame.Input
{
    public sealed partial class GameInputReader : MonoBehaviour
    {

        private static readonly GameInputSemantic[] PersistedGamepadSemantics =
        {
            GameInputSemantic.Jump,
            GameInputSemantic.Sword,
            GameInputSemantic.Special,
            GameInputSemantic.Magic,
            GameInputSemantic.Dodge
        };


        [Serializable]
        private sealed class SavedGamepadBindings
        {
            public int version = 2;
            public List<SavedGamepadBinding> bindings = new List<SavedGamepadBinding>();
        }


        [Serializable]
        private sealed class SavedGamepadBinding
        {
            public string semantic;
            public string path;
        }


        /// <summary>
        /// Restores the native Gamepad profile when a player intentionally uses a
        /// connected Gamepad while Keyboard / Mouse is selected. This recovery is
        /// independent of the currently masked action bindings, so changing to the
        /// keyboard profile can never strand a still-connected controller.
        /// </summary>
        public bool RefreshNativeGamepadProfileRecovery()
        {
            EnsureInitialized();
            if (SelectedInputMode != InputMode.KeyboardMouse ||
                (Context != GameInputContext.Battle && Context != GameInputContext.UI) ||
                !HasIntentionalNativeGamepadInput())
            {
                return false;
            }

            _selectedBindingGroup = GamepadGroup;
            SelectedInputMode = InputMode.ControllerGamepad;
            _preferredInputMode = InputMode.ControllerGamepad;
            _actions.bindingMask = Context == GameInputContext.Battle
                ? InputBinding.MaskByGroup(GamepadGroup)
                : InputBinding.MaskByGroup(KeyboardGroup + ";" + GamepadGroup);
            _suppressActionsUntilRelease = true;
            LastRebindMessage = "Gamepad入力を検出したため、Controller / Gamepadへ切り替えました。";
            SavePreferredInputModeToPlayerPrefs();
            InputModeChanged?.Invoke(SelectedInputMode);
            return true;
        }


        private static bool IsAnyGamepadSettingsButtonPressed()
        {
            for (int index = 0; index < Gamepad.all.Count; index++)
            {
                Gamepad gamepad = Gamepad.all[index];
                if (gamepad != null && gamepad.selectButton.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }


        public string GetGamepadBindingDescription(GameInputSemantic semantic)
        {
            return GetBindingDescriptionForGroup(semantic, GamepadGroup);
        }


        public string SaveBindingOverridesAsJson()
        {
            EnsureInitialized();
            var saved = new SavedGamepadBindings();
            foreach (GameInputSemantic semantic in PersistedGamepadSemantics)
            {
                int bindingIndex = GetBindingIndexForGroup(semantic, GamepadGroup);
                if (bindingIndex < 0)
                {
                    continue;
                }

                saved.bindings.Add(new SavedGamepadBinding
                {
                    semantic = semantic.ToString(),
                    path = ResolveAction(semantic).bindings[bindingIndex].effectivePath
                });
            }
            return JsonUtility.ToJson(saved);
        }


        public void LoadBindingOverridesFromJson(string json)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            SavedGamepadBindings saved = JsonUtility.FromJson<SavedGamepadBindings>(json);
            if (saved == null || saved.version != 2 || saved.bindings == null)
            {
                throw new FormatException("Unsupported controller binding save format.");
            }

            RemoveProfileBindingOverrides(PersistedGamepadSemantics, GamepadGroup);
            var usedControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SavedGamepadBinding entry in saved.bindings)
            {
                string canonicalControl = entry == null
                    ? string.Empty
                    : GamepadBindingPolicy.GetControlName(entry.path);
                if (entry == null ||
                    !Enum.TryParse(entry.semantic, true, out GameInputSemantic semantic) ||
                    Array.IndexOf(PersistedGamepadSemantics, semantic) < 0 ||
                    !GamepadBindingPolicy.IsBindableGamepadPath(entry.path) ||
                    !usedControls.Add(canonicalControl))
                {
                    continue;
                }

                int bindingIndex = GetBindingIndexForGroup(semantic, GamepadGroup);
                if (bindingIndex >= 0)
                {
                    ResolveAction(semantic).ApplyBindingOverride(bindingIndex, entry.path);
                }
            }
            BindingsChanged?.Invoke();
        }


        private static bool IsAnyGamepadButtonPressed()
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                foreach (InputControl control in gamepad.allControls)
                {
                    if (control is ButtonControl button && button.isPressed)
                    {
                        return true;
                    }
                }
            }
            return false;
        }


        private static bool HasIntentionalNativeGamepadInput()
        {
            if (IsAnyGamepadButtonPressed())
            {
                return true;
            }

            const float intentionalStickMagnitude = 0.55f;
            float thresholdSquared = intentionalStickMagnitude * intentionalStickMagnitude;
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad != null &&
                    (gamepad.leftStick.ReadValue().sqrMagnitude >= thresholdSquared ||
                     gamepad.rightStick.ReadValue().sqrMagnitude >= thresholdSquared))
                {
                    return true;
                }
            }

            return false;
        }


        private static bool IsAnyNativeGamepadStickActuated()
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad != null
                    && (gamepad.leftStick.ReadValue().sqrMagnitude > 0.04f
                        || gamepad.rightStick.ReadValue().sqrMagnitude > 0.04f))
                {
                    return true;
                }
            }

            return false;
        }


        private static bool IsCombatLeakControlHeld()
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad == null)
                {
                    continue;
                }

                if (gamepad.buttonSouth.isPressed
                    || gamepad.buttonEast.isPressed
                    || gamepad.buttonWest.isPressed
                    || gamepad.buttonNorth.isPressed
                    || gamepad.startButton.isPressed
                    || gamepad.selectButton.isPressed
                    || gamepad.leftShoulder.isPressed
                    || gamepad.rightShoulder.isPressed
                    || gamepad.leftTrigger.isPressed
                    || gamepad.rightTrigger.isPressed
                    || gamepad.leftStickButton.isPressed
                    || gamepad.rightStickButton.isPressed
                    || gamepad.dpad.up.isPressed
                    || gamepad.dpad.down.isPressed
                    || gamepad.dpad.left.isPressed
                    || gamepad.dpad.right.isPressed)
                {
                    return true;
                }
            }

            return false;
        }


        private static void ReenableConnectedGamepads()
        {
            for (int index = 0; index < Gamepad.all.Count; index++)
            {
                Gamepad gamepad = Gamepad.all[index];
                if (gamepad == null)
                {
                    continue;
                }

                if (!gamepad.enabled)
                {
                    InputSystem.EnableDevice(gamepad);
                }

                InputSystem.ResetDevice(gamepad);
            }
        }
    }
}
