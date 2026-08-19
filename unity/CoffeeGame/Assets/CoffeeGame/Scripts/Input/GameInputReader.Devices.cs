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
        public bool HasUnsupportedControllerDevice => FindUnsupportedControllerDevice() != null;

        public string ConnectedControllersSummary => BuildConnectedControllersSummary();

        public string ControllerCompatibilityHint => BuildControllerCompatibilityHint();


        private void RecordInput(InputAction.CallbackContext context, string actionName)
        {
            LastTriggeredAction = actionName;
            _lastControl = context.control;
            LastUsedDevice = _lastControl?.device;
            DeviceDiagnostic = new InputDeviceDiagnostic(LastUsedDevice, _lastControl, actionName, true);
            DeviceDiagnosticChanged?.Invoke(DeviceDiagnostic);
        }


        private void RecordRawButton(InputControl control)
        {
            if (control == null || control.device == null)
            {
                return;
            }

            string mappedAction = FindMappedActionName(control);
            LastRawButtonDiagnostic = new InputDeviceDiagnostic(
                control.device,
                control,
                string.IsNullOrEmpty(mappedAction) ? "未割当の生入力" : mappedAction,
                true);
            if (control.device is Gamepad)
            {
                LastGamepadButtonDiagnostic = LastRawButtonDiagnostic;
            }
            RawButtonDiagnosticChanged?.Invoke(LastRawButtonDiagnostic);
        }


        private string FindMappedActionName(InputControl control)
        {
            if (control == null)
            {
                return string.Empty;
            }

            foreach (InputActionMap map in _actions.actionMaps)
            {
                if (!map.enabled)
                {
                    continue;
                }

                foreach (InputAction action in map.actions)
                {
                    foreach (InputBinding binding in action.bindings)
                    {
                        if (!binding.isComposite &&
                            !binding.isPartOfComposite &&
                            !string.IsNullOrWhiteSpace(binding.effectivePath) &&
                            InputControlPath.Matches(binding.effectivePath, control))
                        {
                            return $"{map.name}/{action.name}";
                        }
                    }
                }
            }
            return string.Empty;
        }


        private string BuildConnectedControllersSummary()
        {
            var entries = new List<string>();
            int gamepadSlot = 0;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!IsControllerCandidate(device))
                {
                    continue;
                }

                string identity = GetDeviceIdentity(device);
                if (device is Gamepad)
                {
                    entries.Add($"Gamepad {gamepadSlot}: {identity} [{device.layout}/{device.description.interfaceName}]");
                    gamepadSlot++;
                }
                else
                {
                    entries.Add($"対象外HID: {identity} [{device.layout}/{device.description.interfaceName}]");
                }
            }

            if (entries.Count == 0)
            {
                return SelectedInputMode == InputMode.SteamDesktopCompatibility
                    ? "Gamepadなし / Steam Desktop互換 [Keyboard/Mouse]"
                    : "Gamepadなし";
            }

            if (SelectedInputMode == InputMode.SteamDesktopCompatibility)
            {
                entries.Add("Steam Desktop互換 [Keyboard/Mouse]");
            }
            return string.Join("  |  ", entries);
        }


        private string BuildControllerCompatibilityHint()
        {
            InputDevice unsupported = FindUnsupportedControllerDevice();
            if (SelectedInputMode == InputMode.Unselected)
            {
                return HasConnectedGamepad
                    ? "入力方式を選択してください。Controller / Gamepadを選ぶと、検出済みGamepadだけを受け付けます。"
                    : "Gamepadは未検出です。Keyboard / Mouseを選ぶか、SteamへCoffeeGAMEを登録してGamepadレイアウトから起動してください。Steam Desktop互換は明示的に選んだ場合だけ有効になります。";
            }

            if (SelectedInputMode == InputMode.KeyboardMouse)
            {
                return "Keyboard / Mouseのみ受付中です。接続GamepadやSteam Desktop互換へ自動では切り替わりません。";
            }

            if (SelectedInputMode == InputMode.SteamDesktopCompatibility)
            {
                return "Steam Desktop互換を使用中: A=Jump/決定、B=設定画面の取消（戦闘へ再割当可）、X=回転斬り、Y=氷魔法、RT=刀、Stick=移動。Steamの特殊操作はゲームへ届く前に処理されるため、アプリ側では停止できません。";
            }

            if (!HasConnectedGamepad)
            {
                return "選択したGamepadが切断されています。入力方式の選択画面へ戻り、再接続後にController / Gamepadを選び直してください。";
            }

            if (LastRawButtonDiagnostic.DeviceId >= 0 &&
                !IsDeviceIdAConnectedGamepad(LastRawButtonDiagnostic.DeviceId))
            {
                return "Keyboard/Mouseへ変換された入力は届いていますが、現在選択中のController / Gamepadでは受け付けません。Steamライブラリ側のGamepadレイアウトを確認するか、入力方式を選び直してください。";
            }

            foreach (Gamepad gamepad in Gamepad.all)
            {
                string layout = gamepad.layout ?? string.Empty;
                string interfaceName = gamepad.description.interfaceName ?? string.Empty;
                if (layout.IndexOf("XInput", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    interfaceName.IndexOf("XInput", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "XInput仮想Gamepadとして受付中です。物理ボタンの文字ではなく、下の実受信名とcontrol pathを基準に割り当てます。";
                }
            }

            if (unsupported != null)
            {
                return "Gamepadとして受付中です。別のcontroller-like HIDも見えていますが、選択中のGamepad bindingだけが有効です。";
            }

            return "Gamepadとして受付中です。表示は物理ABXYを推測せず、Unityが実際に受け取ったcontrol pathです。";
        }


        private static bool IsControllerCandidate(InputDevice device)
        {
            if (device is Gamepad || device is Joystick)
            {
                return true;
            }
            if (device == null)
            {
                return false;
            }

            string identity = $"{device.name} {device.displayName} {device.layout} " +
                              $"{device.description.manufacturer} {device.description.product}";
            return identity.IndexOf("steam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identity.IndexOf("controller", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identity.IndexOf("gamepad", StringComparison.OrdinalIgnoreCase) >= 0;
        }


        private static InputDevice FindUnsupportedControllerDevice()
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!(device is Gamepad) && IsControllerCandidate(device))
                {
                    return device;
                }
            }
            return null;
        }


        private static string GetDeviceIdentity(InputDevice device)
        {
            if (device == null)
            {
                return "不明な機器";
            }
            if (!string.IsNullOrWhiteSpace(device.description.product))
            {
                return device.description.product;
            }
            if (!string.IsNullOrWhiteSpace(device.displayName))
            {
                return device.displayName;
            }
            return device.name;
        }


        private static bool IsDeviceIdAConnectedGamepad(int deviceId)
        {
            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.deviceId == deviceId)
                {
                    return true;
                }
            }
            return false;
        }


        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (LastUsedDevice == null || device.deviceId != LastUsedDevice.deviceId)
            {
                return;
            }

            switch (change)
            {
                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    DeviceDiagnostic = new InputDeviceDiagnostic(device, _lastControl, LastTriggeredAction, false);
                    DeviceDiagnosticChanged?.Invoke(DeviceDiagnostic);
                    break;
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                case InputDeviceChange.ConfigurationChanged:
                    DeviceDiagnostic = new InputDeviceDiagnostic(device, _lastControl, LastTriggeredAction, true);
                    DeviceDiagnosticChanged?.Invoke(DeviceDiagnostic);
                    break;
            }
        }
    }
}
