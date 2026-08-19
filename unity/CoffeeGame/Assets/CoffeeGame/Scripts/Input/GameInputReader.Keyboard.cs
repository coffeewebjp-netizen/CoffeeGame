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

        private static void AddNavigationBindings(InputAction action)
        {
            var wasd = action.AddCompositeBinding("2DVector");
            action.ChangeBinding(wasd.bindingIndex).WithGroups(MenuFallbackGroups);
            wasd
                .With("Up", "<Keyboard>/w", groups: MenuFallbackGroups)
                .With("Down", "<Keyboard>/s", groups: MenuFallbackGroups)
                .With("Left", "<Keyboard>/a", groups: MenuFallbackGroups)
                .With("Right", "<Keyboard>/d", groups: MenuFallbackGroups);

            var arrows = action.AddCompositeBinding("2DVector");
            action.ChangeBinding(arrows.bindingIndex).WithGroups(MenuFallbackGroups);
            arrows
                .With("Up", "<Keyboard>/upArrow", groups: MenuFallbackGroups)
                .With("Down", "<Keyboard>/downArrow", groups: MenuFallbackGroups)
                .With("Left", "<Keyboard>/leftArrow", groups: MenuFallbackGroups)
                .With("Right", "<Keyboard>/rightArrow", groups: MenuFallbackGroups);
            action.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            action.AddBinding("<Gamepad>/dpad", groups: GamepadGroup);
        }


        private static void AddMovementBindings(InputAction action)
        {
            const string keyboardAndDesktopGroups = KeyboardGroup + ";" + SteamDesktopGroup;
            var wasd = action.AddCompositeBinding("2DVector");
            action.ChangeBinding(wasd.bindingIndex).WithGroups(keyboardAndDesktopGroups);
            wasd
                .With("Up", "<Keyboard>/w", groups: keyboardAndDesktopGroups)
                .With("Down", "<Keyboard>/s", groups: keyboardAndDesktopGroups)
                .With("Left", "<Keyboard>/a", groups: keyboardAndDesktopGroups)
                .With("Right", "<Keyboard>/d", groups: keyboardAndDesktopGroups);

            var arrows = action.AddCompositeBinding("2DVector");
            action.ChangeBinding(arrows.bindingIndex).WithGroups(keyboardAndDesktopGroups);
            arrows
                .With("Up", "<Keyboard>/upArrow", groups: keyboardAndDesktopGroups)
                .With("Down", "<Keyboard>/downArrow", groups: keyboardAndDesktopGroups)
                .With("Left", "<Keyboard>/leftArrow", groups: keyboardAndDesktopGroups)
                .With("Right", "<Keyboard>/rightArrow", groups: keyboardAndDesktopGroups);
            action.AddBinding("<Gamepad>/leftStick", groups: GamepadGroup);
            action.AddBinding("<Gamepad>/dpad", groups: GamepadGroup);
        }


        private static void AddCameraYawBindings(InputAction action)
        {
            var keys = action.AddCompositeBinding("1DAxis");
            action.ChangeBinding(keys.bindingIndex).WithGroups(MenuFallbackGroups);
            keys
                .With("Negative", "<Keyboard>/z", groups: MenuFallbackGroups)
                .With("Positive", "<Keyboard>/c", groups: MenuFallbackGroups);
            action.AddBinding("<Gamepad>/rightStick/x", groups: GamepadGroup)
                .WithProcessor("axisDeadzone(min=0.18,max=0.95)");
        }


        private static void AddCameraPitchBindings(InputAction action)
        {
            var keys = action.AddCompositeBinding("1DAxis");
            action.ChangeBinding(keys.bindingIndex).WithGroups(MenuFallbackGroups);
            keys
                .With("Negative", "<Keyboard>/v", groups: MenuFallbackGroups)
                .With("Positive", "<Keyboard>/r", groups: MenuFallbackGroups);
            action.AddBinding("<Gamepad>/rightStick/y", groups: GamepadGroup)
                .WithProcessor("axisDeadzone(min=0.18,max=0.95)");
        }


        /// <summary>
        /// Lock and IME latch keys stay "pressed" after Japanese text entry.
        /// They must not keep the Battle/UI release gate closed.
        /// </summary>
        private static bool IsIgnorableKeyboardLatchKey(KeyControl key)
        {
            if (key == null)
            {
                return true;
            }

            switch (key.keyCode)
            {
                case Key.CapsLock:
                case Key.NumLock:
                case Key.ScrollLock:
                case Key.OEM1:
                case Key.OEM2:
                case Key.OEM3:
                case Key.OEM4:
                case Key.OEM5:
                    return true;
                default:
                    return false;
            }
        }
    }
}
