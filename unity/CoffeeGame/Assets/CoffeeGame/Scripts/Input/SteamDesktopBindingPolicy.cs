using System;
using System.Collections.Generic;

namespace CoffeeGame.Input
{
    /// <summary>
    /// Validation rules for the keyboard/mouse events emitted by the Steam
    /// Controller desktop layout when no virtual Gamepad/XInput device exists.
    /// Navigation, menu and the original keyboard combat keys remain reserved so
    /// a desktop-profile override cannot make two actions fire at once.
    /// </summary>
    public static class SteamDesktopBindingPolicy
    {
        private static readonly HashSet<string> ReservedKeyboardControls =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "escape",
                "tab",
                "space",
                "w",
                "a",
                "s",
                "d",
                "upArrow",
                "downArrow",
                "leftArrow",
                "rightArrow",
                "f",
                "q",
                "e"
            };

        private static readonly HashSet<string> BindableMouseControls =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "leftButton",
                "rightButton",
                "middleButton",
                "backButton",
                "forwardButton"
            };

        public static bool IsBindableDesktopPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string control = GetControlName(path);
            if (path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrEmpty(control) && !ReservedKeyboardControls.Contains(control);
            }

            return path.StartsWith("<Mouse>/", StringComparison.OrdinalIgnoreCase) &&
                   BindableMouseControls.Contains(control);
        }

        public static bool IsReservedDesktopPath(string path)
        {
            return path != null &&
                   path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase) &&
                   ReservedKeyboardControls.Contains(GetControlName(path));
        }

        public static bool AreSameControl(string firstPath, string secondPath)
        {
            return !string.IsNullOrWhiteSpace(firstPath) &&
                   !string.IsNullOrWhiteSpace(secondPath) &&
                   string.Equals(firstPath.Trim(), secondPath.Trim(), StringComparison.OrdinalIgnoreCase);
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
