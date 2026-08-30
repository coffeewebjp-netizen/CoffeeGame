using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace CoffeeGame.Input
{
    public enum GameInputSemantic
    {
        Move,
        Jump,
        Sword,
        Special,
        Magic,
        Pause,
        Navigate,
        Confirm,
        Dodge
    }

    public enum GameInputContext
    {
        None,
        InputSelection,
        Battle,
        UI
    }

    public enum InputMode
    {
        Unselected,
        KeyboardMouse,
        ControllerGamepad,
        SteamDesktopCompatibility,
        TouchOnScreen
    }

    [Serializable]
    public readonly struct InputDeviceDiagnostic
    {
        public InputDeviceDiagnostic(InputDevice device, InputControl control, string actionName, bool connected)
        {
            DeviceId = device != null ? device.deviceId : -1;
            DeviceName = device != null ? device.name : string.Empty;
            DisplayName = device != null ? device.displayName : string.Empty;
            Layout = device != null ? device.layout : string.Empty;
            InterfaceName = device != null ? device.description.interfaceName : string.Empty;
            Manufacturer = device != null ? device.description.manufacturer : string.Empty;
            Product = device != null ? device.description.product : string.Empty;
            ControlPath = control != null ? control.path : string.Empty;
            ControlDisplayName = GetControlDisplayName(control);
            ActionName = actionName ?? string.Empty;
            IsConnected = connected;
        }

        public int DeviceId { get; }
        public string DeviceName { get; }
        public string DisplayName { get; }
        public string Layout { get; }
        public string InterfaceName { get; }
        public string Manufacturer { get; }
        public string Product { get; }
        public string ControlPath { get; }
        public string ControlDisplayName { get; }
        public string ActionName { get; }
        public bool IsConnected { get; }

        public override string ToString()
        {
            if (DeviceId < 0)
            {
                return "No input device used yet";
            }

            var identity = !string.IsNullOrWhiteSpace(Product) ? Product : DisplayName;
            var mapping = string.IsNullOrWhiteSpace(ControlDisplayName)
                ? ActionName
                : $"{ActionName} <- {ControlDisplayName}";
            var state = IsConnected ? "connected" : "disconnected";
            return $"{identity} [{Layout}/{InterfaceName}, {state}] {mapping} ({ControlPath})";
        }

        private static string GetControlDisplayName(InputControl control)
        {
            if (control == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(control.shortDisplayName))
            {
                return control.shortDisplayName;
            }

            if (!string.IsNullOrWhiteSpace(control.displayName))
            {
                return control.displayName;
            }

            return InputControlPath.ToHumanReadableString(
                control.path,
                InputControlPath.HumanReadableStringOptions.OmitDevice |
                InputControlPath.HumanReadableStringOptions.UseShortNames,
                control);
        }
    }
}
