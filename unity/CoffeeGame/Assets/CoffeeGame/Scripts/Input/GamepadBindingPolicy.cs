using System;
using System.Collections.Generic;

namespace CoffeeGame.Input
{
    /// <summary>
    /// Defines the gamepad controls accepted by the in-game binding screen.
    /// Face buttons, shoulders, triggers and stick presses can be used for battle
    /// actions. Start/View stay reserved for menu control, while East/B remains a
    /// UI cancel only when the UI action map is active.
    /// </summary>
    public static class GamepadBindingPolicy
    {
        private static readonly HashSet<string> BindableControlNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "buttonSouth",
                "buttonEast",
                "buttonWest",
                "buttonNorth",
                "leftShoulder",
                "rightShoulder",
                "leftTrigger",
                "rightTrigger",
                "leftStickPress",
                "rightStickPress"
            };

        private static readonly HashSet<string> ReservedCancelControlNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "start",
                "startButton",
                "select",
                "selectButton"
            };

        public static bool IsBindableGamepadPath(string path)
        {
            return BindableControlNames.Contains(GetControlName(path));
        }

        public static bool IsReservedCancelPath(string path)
        {
            return ReservedCancelControlNames.Contains(GetControlName(path));
        }

        public static bool AreSameControl(string firstPath, string secondPath)
        {
            string first = GetControlName(firstPath);
            string second = GetControlName(secondPath);
            return !string.IsNullOrEmpty(first) &&
                   string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        public static string GetControlName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash + 1 < path.Length
                ? path.Substring(slash + 1)
                : path;
        }
    }
}
