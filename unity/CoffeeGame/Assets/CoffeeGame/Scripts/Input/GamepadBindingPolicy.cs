using System;
using System.Collections.Generic;

namespace CoffeeGame.Input
{
    /// <summary>
    /// Defines the gamepad controls accepted by the in-game binding screen.
    /// East/B is deliberately reserved as cancel, while Start/View are kept for
    /// menu control. Movement controls are never accepted as attack bindings.
    /// </summary>
    public static class GamepadBindingPolicy
    {
        private static readonly HashSet<string> BindableControlNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "buttonSouth",
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
                "buttonEast",
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
