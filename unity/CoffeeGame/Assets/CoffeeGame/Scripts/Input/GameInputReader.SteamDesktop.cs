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

        private static readonly GameInputSemantic[] PersistedSteamDesktopSemantics =
        {
            GameInputSemantic.Jump,
            GameInputSemantic.Sword,
            GameInputSemantic.Special,
            GameInputSemantic.Magic
        };


        [Serializable]
        private sealed class SavedSteamDesktopBindings
        {
            public int version = 1;
            public List<SavedSteamDesktopBinding> bindings = new List<SavedSteamDesktopBinding>();
        }


        [Serializable]
        private sealed class SavedSteamDesktopBinding
        {
            public string semantic;
            public string path;
        }


        public string GetSteamDesktopBindingDescription(GameInputSemantic semantic)
        {
            return GetBindingDescriptionForGroup(semantic, SteamDesktopGroup);
        }


        public string SaveSteamDesktopBindingOverridesAsJson()
        {
            EnsureInitialized();
            var saved = new SavedSteamDesktopBindings();
            foreach (GameInputSemantic semantic in PersistedSteamDesktopSemantics)
            {
                int bindingIndex = GetBindingIndexForGroup(semantic, SteamDesktopGroup);
                if (bindingIndex < 0)
                {
                    continue;
                }

                saved.bindings.Add(new SavedSteamDesktopBinding
                {
                    semantic = semantic.ToString(),
                    path = ResolveAction(semantic).bindings[bindingIndex].effectivePath
                });
            }
            return JsonUtility.ToJson(saved);
        }


        public void LoadSteamDesktopBindingOverridesFromJson(string json)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            SavedSteamDesktopBindings saved = JsonUtility.FromJson<SavedSteamDesktopBindings>(json);
            if (saved == null || saved.version != 1 || saved.bindings == null)
            {
                throw new FormatException("Unsupported Steam Desktop binding save format.");
            }

            RemoveProfileBindingOverrides(PersistedSteamDesktopSemantics, SteamDesktopGroup);
            var usedControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SavedSteamDesktopBinding entry in saved.bindings)
            {
                string canonicalControl = entry == null ? string.Empty : entry.path?.Trim();
                if (entry == null ||
                    !Enum.TryParse(entry.semantic, true, out GameInputSemantic semantic) ||
                    Array.IndexOf(PersistedSteamDesktopSemantics, semantic) < 0 ||
                    !SteamDesktopBindingPolicy.IsBindableDesktopPath(entry.path) ||
                    !usedControls.Add(canonicalControl))
                {
                    continue;
                }

                int bindingIndex = GetBindingIndexForGroup(semantic, SteamDesktopGroup);
                if (bindingIndex >= 0)
                {
                    ResolveAction(semantic).ApplyBindingOverride(bindingIndex, entry.path);
                }
            }
            BindingsChanged?.Invoke();
        }


        private static bool IsAnySteamDesktopButtonPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                foreach (KeyControl key in keyboard.allKeys)
                {
                    if (key != null && key.isPressed && !IsIgnorableKeyboardLatchKey(key))
                    {
                        return true;
                    }
                }
            }

            Mouse mouse = Mouse.current;
            return mouse != null &&
                   (mouse.leftButton.isPressed ||
                    mouse.rightButton.isPressed ||
                    mouse.middleButton.isPressed ||
                    mouse.forwardButton.isPressed ||
                    mouse.backButton.isPressed);
        }
    }
}
