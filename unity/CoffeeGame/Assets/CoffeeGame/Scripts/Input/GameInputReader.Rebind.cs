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
        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;

        private InputAction _rebindAction;

        private GameInputContext _contextBeforeRebind;

        private int _rebindBindingIndex = -1;

        private string _previousOverridePath;

        private string _previousEffectivePath;

        private string _rebindBindingGroup;

        private bool _waitingForRebindButtonRelease;

        private bool _rebindTimedOut;

        private float _rebindStartedAt;

        private GameInputSemantic? _rebindSemantic;


        public string GetBindingDisplayString(GameInputSemantic semantic)
        {
            return GetBindingDisplayString(semantic, LastUsedDevice);
        }


        public string GetBindingDisplayString(GameInputSemantic semantic, InputDevice device)
        {
            EnsureInitialized();
            var action = ResolveAction(semantic);
            var labels = new List<string>();

            foreach (var control in action.controls)
            {
                if (device != null && control.device.deviceId != device.deviceId)
                {
                    continue;
                }

                var label = !string.IsNullOrWhiteSpace(control.shortDisplayName)
                    ? control.shortDisplayName
                    : control.displayName;
                if (!string.IsNullOrWhiteSpace(label) && !labels.Contains(label))
                {
                    labels.Add(label);
                }
            }

            if (labels.Count > 0)
            {
                return string.Join(" / ", labels);
            }

            return device == null ? action.GetBindingDisplayString() : "Unbound";
        }


        public string GetBindingDisplayStringAtIndex(GameInputSemantic semantic, int bindingIndex)
        {
            EnsureInitialized();
            var action = ResolveAction(semantic);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            }

            return action.GetBindingDisplayString(bindingIndex);
        }


        public string GetBindingEffectivePathAtIndex(GameInputSemantic semantic, int bindingIndex)
        {
            EnsureInitialized();
            var action = ResolveAction(semantic);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            }

            return action.bindings[bindingIndex].effectivePath ?? string.Empty;
        }


        public string GetActiveControllerBindingDescription(GameInputSemantic semantic)
        {
            if (string.IsNullOrEmpty(PreferredRebindBindingGroup))
            {
                return SelectedInputMode == InputMode.KeyboardMouse
                    ? "Keyboard / Mouse（固定）"
                    : "入力方式未選択";
            }
            return GetBindingDescriptionForGroup(semantic, PreferredRebindBindingGroup);
        }


        private string GetBindingDescriptionForGroup(GameInputSemantic semantic, string bindingGroup)
        {
            int bindingIndex = GetBindingIndexForGroup(semantic, bindingGroup);
            if (bindingIndex < 0)
            {
                return "未割当";
            }

            string display = GetBindingDisplayStringAtIndex(semantic, bindingIndex);
            string path = GetBindingEffectivePathAtIndex(semantic, bindingIndex);
            if (string.IsNullOrWhiteSpace(display))
            {
                display = "名称不明";
            }
            return string.IsNullOrWhiteSpace(path) ? display : $"{display}  [{path}]";
        }


        public int GetBindingCount(GameInputSemantic semantic)
        {
            EnsureInitialized();
            return ResolveAction(semantic).bindings.Count;
        }


        public int GetBindingIndexForGroup(GameInputSemantic semantic, string bindingGroup)
        {
            EnsureInitialized();
            var action = ResolveAction(semantic);
            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (BindingBelongsToGroup(action.bindings[index], bindingGroup))
                {
                    return index;
                }
            }
            return -1;
        }


        public bool TryStartInteractiveRebind(GameInputSemantic semantic, int bindingIndex)
        {
            EnsureInitialized();
            if (IsRebinding)
            {
                return false;
            }

            var action = ResolveAction(semantic);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
            {
                return false;
            }

            string requestedBindingGroup = GetKnownBindingGroup(action.bindings[bindingIndex]);
            if (string.IsNullOrEmpty(PreferredRebindBindingGroup) ||
                string.Equals(requestedBindingGroup, KeyboardGroup, StringComparison.OrdinalIgnoreCase))
            {
                LastRebindMessage = "Keyboard / Mouse配置は現在固定です。Controller / GamepadまたはSteam Desktop互換を選ぶとボタン配置を変更できます。";
                return false;
            }
            if (!string.Equals(
                    requestedBindingGroup,
                    PreferredRebindBindingGroup,
                    StringComparison.OrdinalIgnoreCase))
            {
                LastRebindMessage = "現在選択中ではない入力方式の配置は変更できません。";
                return false;
            }

            _rebindAction = action;
            _rebindBindingIndex = bindingIndex;
            _previousOverridePath = action.bindings[bindingIndex].overridePath;
            _previousEffectivePath = action.bindings[bindingIndex].effectivePath;
            _rebindBindingGroup = requestedBindingGroup;
            _contextBeforeRebind = Context;
            _rebindSemantic = semantic;
            _rebindTimedOut = false;
            _rebindStartedAt = Time.unscaledTime;
            LastRebindMessage = $"{GetSemanticDisplayName(semantic)}: 決定に使ったボタンを離してください…";
            DisableAll();

            InputBinding targetBinding = action.bindings[bindingIndex];
            if (BindingBelongsToGroup(targetBinding, GamepadGroup) ||
                BindingBelongsToGroup(targetBinding, SteamDesktopGroup))
            {
                // The control that selected this row is still held during the same Input
                // System update. Wait for every candidate control to be released so A/Enter
                // or a mouse click cannot immediately bind itself.
                _waitingForRebindButtonRelease = true;
            }
            else if (BindingBelongsToGroup(targetBinding, KeyboardGroup))
            {
                StartInteractiveRebindOperation();
            }
            else
            {
                ClearRebindState();
                RestoreContextAfterRebind();
                LastRebindMessage = "このスロットは再割当できません。";
                return false;
            }
            return true;
        }


        public void CancelInteractiveRebind()
        {
            if (_rebindOperation != null)
            {
                _rebindOperation.Cancel();
                return;
            }

            if (_waitingForRebindButtonRelease)
            {
                FinishInteractiveRebind(false);
            }
        }


        public bool SaveBindingOverridesToPlayerPrefs()
        {
            try
            {
                PlayerPrefs.SetString(bindingOverridesPlayerPrefsKey, SaveBindingOverridesAsJson());
                PlayerPrefs.SetString(desktopBindingOverridesPlayerPrefsKey, SaveSteamDesktopBindingOverridesAsJson());
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save input binding overrides: {exception.Message}", this);
                return false;
            }
        }


        public bool LoadBindingOverridesFromPlayerPrefs()
        {
            bool loaded = false;
            try
            {
                if (PlayerPrefs.HasKey(bindingOverridesPlayerPrefsKey))
                {
                    LoadBindingOverridesFromJson(PlayerPrefs.GetString(bindingOverridesPlayerPrefsKey));
                    loaded = true;
                }

                if (PlayerPrefs.HasKey(desktopBindingOverridesPlayerPrefsKey))
                {
                    LoadSteamDesktopBindingOverridesFromJson(PlayerPrefs.GetString(desktopBindingOverridesPlayerPrefsKey));
                    loaded = true;
                }
                return loaded;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not load input binding overrides: {exception.Message}", this);
                PlayerPrefs.DeleteKey(bindingOverridesPlayerPrefsKey);
                PlayerPrefs.DeleteKey(desktopBindingOverridesPlayerPrefsKey);
                PlayerPrefs.Save();
                return false;
            }
        }


        public void ResetBindingOverrides()
        {
            EnsureInitialized();
            _actions.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(bindingOverridesPlayerPrefsKey);
            PlayerPrefs.DeleteKey(desktopBindingOverridesPlayerPrefsKey);
            PlayerPrefs.DeleteKey("CoffeeGame.Input.BindingOverrides.v1");
            PlayerPrefs.Save();
            LastRebindMessage = "初期配置へ戻しました（保存済み）。";
            BindingsChanged?.Invoke();
        }


        private void StartInteractiveRebindOperation()
        {
            if (_rebindAction == null || _rebindBindingIndex < 0)
            {
                FinishInteractiveRebind(false);
                return;
            }

            InputBinding targetBinding = _rebindAction.bindings[_rebindBindingIndex];
            var operation = _rebindAction.PerformInteractiveRebinding(_rebindBindingIndex)
                .WithControlsExcluding("<Touchscreen>")
                .WithExpectedControlType("Button")
                .OnMatchWaitForAnother(0.08f)
                .OnComplete(_ => FinishInteractiveRebind(true))
                .OnCancel(_ => FinishInteractiveRebind(false));

            if (BindingBelongsToGroup(targetBinding, GamepadGroup))
            {
                operation
                    .WithControlsHavingToMatchPath("<Gamepad>")
                    .WithControlsExcluding("<Mouse>")
                    .WithControlsExcluding("<Pointer>")
                    .WithControlsExcluding("<Gamepad>/start")
                    .WithControlsExcluding("<Gamepad>/select")
                    .WithControlsExcluding("<Gamepad>/dpad")
                    .WithControlsExcluding("<Gamepad>/leftStick")
                    .WithControlsExcluding("<Gamepad>/rightStick");
                LastRebindMessage = $"{GetSemanticDisplayName(_rebindSemantic)}: 次に使うGamepadボタンを押してください（Start/View・Escで取消）。";
            }
            else if (BindingBelongsToGroup(targetBinding, SteamDesktopGroup))
            {
                operation
                    .WithControlsExcluding("<Gamepad>")
                    .WithControlsExcluding("<Joystick>")
                    .WithControlsExcluding("<Keyboard>/escape")
                    .WithControlsExcluding("<Keyboard>/tab")
                    .WithControlsExcluding("<Keyboard>/w")
                    .WithControlsExcluding("<Keyboard>/a")
                    .WithControlsExcluding("<Keyboard>/s")
                    .WithControlsExcluding("<Keyboard>/d")
                    .WithControlsExcluding("<Keyboard>/upArrow")
                    .WithControlsExcluding("<Keyboard>/downArrow")
                    .WithControlsExcluding("<Keyboard>/leftArrow")
                    .WithControlsExcluding("<Keyboard>/rightArrow")
                    .WithControlsExcluding("<Keyboard>/f")
                    .WithControlsExcluding("<Keyboard>/q")
                    .WithControlsExcluding("<Keyboard>/e")
                    .WithControlsExcluding("<Pointer>/position")
                    .WithControlsExcluding("<Pointer>/delta")
                    .WithControlsExcluding("<Mouse>/scroll");
                LastRebindMessage = $"{GetSemanticDisplayName(_rebindSemantic)}: Steam Controllerのボタンを押してください（Escで取消）。Keyboard/Mouseへ変換された実入力を保存します。";
            }
            else
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>");
                LastRebindMessage = $"{GetSemanticDisplayName(_rebindSemantic)}: 次に使うキーを押してください（Escで取消）。";
            }

            _rebindOperation = operation;
            operation.Start();
        }


        private void FinishInteractiveRebind(bool completed)
        {
            var operation = _rebindOperation;
            _rebindOperation = null;
            _waitingForRebindButtonRelease = false;
            operation?.Dispose();

            bool accepted = completed &&
                            _rebindAction != null &&
                            _rebindBindingIndex >= 0 &&
                            _rebindBindingIndex < _rebindAction.bindings.Count;
            string changedPath = accepted
                ? _rebindAction.bindings[_rebindBindingIndex].effectivePath
                : string.Empty;

            if (accepted && !IsBindablePathForGroup(changedPath, _rebindBindingGroup))
            {
                RestorePreviousRebindOverride();
                accepted = false;
                LastRebindMessage = _rebindBindingGroup == SteamDesktopGroup
                    ? "その入力はSteam Desktop互換の攻撃ボタンに使えません。Enter/PageUp/PageDownなどの非予約キー、またはMouseボタンを選んでください。"
                    : "その入力は攻撃ボタンに使えません。Face South/East/West/North、肩、トリガー、Stick Pressから選んでください。";
            }

            string swappedSemanticName = string.Empty;
            if (accepted && TrySwapDuplicateBinding(changedPath, _rebindBindingGroup, out GameInputSemantic swappedSemantic))
            {
                swappedSemanticName = GetSemanticDisplayName(swappedSemantic);
            }

            if (accepted)
            {
                string target = GetSemanticDisplayName(_rebindSemantic);
                string display = _rebindAction.GetBindingDisplayString(_rebindBindingIndex);
                BindingsChanged?.Invoke();
                bool saved = !saveBindingsAfterRebind || SaveBindingOverridesToPlayerPrefs();
                string swapMessage = string.IsNullOrEmpty(swappedSemanticName)
                    ? string.Empty
                    : $" {swappedSemanticName}は以前の{GetPathDisplayName(_previousEffectivePath)}へ交換しました。";
                LastRebindMessage = saved
                    ? $"保存済み: {target} = {display} [{changedPath}].{swapMessage}"
                    : $"{target} = {display} に変更しましたが、保存に失敗しました。{swapMessage}";
            }
            else if (string.IsNullOrEmpty(LastRebindMessage) || !completed)
            {
                RestorePreviousRebindOverride();
                LastRebindMessage = _rebindTimedOut
                    ? "10秒間入力がなかったため、割当変更を取り消しました。"
                    : "割当変更を取り消しました。";
            }

            _suppressActionsUntilRelease = true;
            RestoreContextAfterRebind();
            ClearRebindState();
            RebindFinished?.Invoke(accepted);
        }


        private bool TrySwapDuplicateBinding(
            string changedPath,
            string bindingGroup,
            out GameInputSemantic swappedSemantic)
        {
            swappedSemantic = default;
            GameInputSemantic[] semantics = bindingGroup == SteamDesktopGroup
                ? PersistedSteamDesktopSemantics
                : PersistedGamepadSemantics;
            foreach (GameInputSemantic semantic in semantics)
            {
                InputAction action = ResolveAction(semantic);
                int bindingIndex = GetBindingIndexForGroup(semantic, bindingGroup);
                if (bindingIndex < 0 || (action == _rebindAction && bindingIndex == _rebindBindingIndex))
                {
                    continue;
                }

                bool sameControl = bindingGroup == SteamDesktopGroup
                    ? SteamDesktopBindingPolicy.AreSameControl(action.bindings[bindingIndex].effectivePath, changedPath)
                    : GamepadBindingPolicy.AreSameControl(action.bindings[bindingIndex].effectivePath, changedPath);
                if (!sameControl)
                {
                    continue;
                }

                action.ApplyBindingOverride(bindingIndex, _previousEffectivePath);
                swappedSemantic = semantic;
                return true;
            }
            return false;
        }


        private void RestorePreviousRebindOverride()
        {
            if (_rebindAction == null || _rebindBindingIndex < 0 || _rebindBindingIndex >= _rebindAction.bindings.Count)
            {
                return;
            }

            if (string.IsNullOrEmpty(_previousOverridePath))
            {
                _rebindAction.RemoveBindingOverride(_rebindBindingIndex);
            }
            else
            {
                _rebindAction.ApplyBindingOverride(_rebindBindingIndex, _previousOverridePath);
            }
        }


        private static string GetSemanticDisplayName(GameInputSemantic? semantic)
        {
            return semantic switch
            {
                GameInputSemantic.Jump => "ジャンプ",
                GameInputSemantic.Sword => "刀攻撃",
                GameInputSemantic.Special => "回転斬り",
                GameInputSemantic.Magic => "氷魔法",
                GameInputSemantic.Pause => "一時停止",
                _ => "選択中のアクション"
            };
        }


        private static string GetPathDisplayName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "未割当";
            }
            return GamepadBindingPolicy.GetControlName(path);
        }


        private bool IsRebindCancelPressed()
        {
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                return true;
            }

            foreach (Gamepad gamepad in Gamepad.all)
            {
                if (gamepad.startButton.wasPressedThisFrame ||
                    gamepad.selectButton.wasPressedThisFrame)
                {
                    return true;
                }
            }
            return false;
        }


        private static bool IsAnyRebindCandidatePressed(string bindingGroup)
        {
            return bindingGroup == SteamDesktopGroup
                ? IsAnySteamDesktopButtonPressed()
                : IsAnyGamepadButtonPressed();
        }


        private void ClearRebindState()
        {
            _rebindAction = null;
            _rebindBindingIndex = -1;
            _previousOverridePath = null;
            _previousEffectivePath = null;
            _rebindBindingGroup = null;
            _rebindSemantic = null;
            _waitingForRebindButtonRelease = false;
            _rebindTimedOut = false;
            _rebindStartedAt = 0f;
        }


        private static bool BindingBelongsToGroup(InputBinding binding, string bindingGroup)
        {
            if (string.IsNullOrWhiteSpace(bindingGroup) || string.IsNullOrWhiteSpace(binding.groups))
            {
                return false;
            }

            string[] groups = binding.groups.Split(';');
            foreach (string group in groups)
            {
                if (string.Equals(group.Trim(), bindingGroup, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }


        private static string GetKnownBindingGroup(InputBinding binding)
        {
            if (BindingBelongsToGroup(binding, GamepadGroup))
            {
                return GamepadGroup;
            }
            if (BindingBelongsToGroup(binding, SteamDesktopGroup))
            {
                return SteamDesktopGroup;
            }
            if (BindingBelongsToGroup(binding, KeyboardGroup))
            {
                return KeyboardGroup;
            }
            return string.Empty;
        }


        private static bool IsBindablePathForGroup(string path, string bindingGroup)
        {
            return bindingGroup == SteamDesktopGroup
                ? SteamDesktopBindingPolicy.IsBindableDesktopPath(path)
                : GamepadBindingPolicy.IsBindableGamepadPath(path);
        }


        private void RemoveProfileBindingOverrides(
            GameInputSemantic[] semantics,
            string bindingGroup)
        {
            foreach (GameInputSemantic semantic in semantics)
            {
                InputAction action = ResolveAction(semantic);
                int bindingIndex = GetBindingIndexForGroup(semantic, bindingGroup);
                if (bindingIndex >= 0)
                {
                    action.RemoveBindingOverride(bindingIndex);
                }
            }
        }


        private void RestoreContextAfterRebind()
        {
            GameInputContext context = _contextBeforeRebind;
            _contextBeforeRebind = GameInputContext.None;
            if (context == GameInputContext.Battle)
            {
                EnableBattle();
            }
            else if (context == GameInputContext.UI)
            {
                EnableUI();
            }
            else
            {
                DisableAll();
            }
        }
    }
}
