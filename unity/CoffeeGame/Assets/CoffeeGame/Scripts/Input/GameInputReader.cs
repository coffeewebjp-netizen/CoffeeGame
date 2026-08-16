using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
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
        Confirm
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
        SteamDesktopCompatibility
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

    [DefaultExecutionOrder(-200)]
    public sealed class GameInputReader : MonoBehaviour
    {
        private const string KeyboardGroup = "Keyboard";
        public const string GamepadBindingGroup = "Gamepad";
        public const string SteamDesktopBindingGroup = "SteamDesktop";
        public const string InputModePlayerPrefsKey = "CoffeeGame.Input.Mode.v1";
        private const string GamepadGroup = GamepadBindingGroup;
        private const string SteamDesktopGroup = SteamDesktopBindingGroup;

        [SerializeField] private bool enableBattleOnEnable = true;
        [SerializeField] private bool loadSavedBindingsOnAwake = true;
        [SerializeField] private bool saveBindingsAfterRebind = true;
        [SerializeField, Min(3f)] private float rebindTimeoutSeconds = 10f;
        [SerializeField] private string bindingOverridesPlayerPrefsKey = "CoffeeGame.Input.GamepadBindings.v2";
        [SerializeField] private string desktopBindingOverridesPlayerPrefsKey = "CoffeeGame.Input.SteamDesktopBindings.v1";

        private static readonly GameInputSemantic[] PersistedGamepadSemantics =
        {
            GameInputSemantic.Jump,
            GameInputSemantic.Sword,
            GameInputSemantic.Special,
            GameInputSemantic.Magic
        };

        private static readonly GameInputSemantic[] PersistedSteamDesktopSemantics =
        {
            GameInputSemantic.Jump,
            GameInputSemantic.Sword,
            GameInputSemantic.Special,
            GameInputSemantic.Magic
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

        private InputActionAsset _actions;
        private InputActionMap _battleMap;
        private InputActionMap _uiMap;
        private InputAction _move;
        private InputAction _cameraYaw;
        private InputAction _cameraPitch;
        private InputAction _jump;
        private InputAction _sword;
        private InputAction _special;
        private InputAction _magic;
        private InputAction _pause;
        private InputAction _navigate;
        private InputAction _confirm;
        private InputAction _cancel;
        private InputAction _uiPause;
        private InputAction _battleSettings;
        private InputAction _uiSettings;
        private InputActionRebindingExtensions.RebindingOperation _rebindOperation;
        private InputAction _rebindAction;
        private GameInputContext _contextBeforeRebind;
        private int _rebindBindingIndex = -1;
        private string _previousOverridePath;
        private string _previousEffectivePath;
        private string _rebindBindingGroup;
        private bool _waitingForRebindButtonRelease;
        private bool _suppressActionsUntilRelease;
        private bool _rebindTimedOut;
        private float _rebindStartedAt;
        private GameInputSemantic? _rebindSemantic;
        private InputControl _lastControl;
        private IDisposable _rawButtonSubscription;
        private InputMode _preferredInputMode;
        private string _selectedBindingGroup = string.Empty;

        public Vector2 Move => !_suppressActionsUntilRelease && _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public float CameraYaw =>
            !_suppressActionsUntilRelease && _cameraYaw != null ? _cameraYaw.ReadValue<float>() : 0f;
        public float CameraPitch =>
            !_suppressActionsUntilRelease && _cameraPitch != null ? _cameraPitch.ReadValue<float>() : 0f;
        public Vector2 CameraPointerDelta
        {
            get
            {
                if (_suppressActionsUntilRelease || Context != GameInputContext.Battle)
                {
                    return Vector2.zero;
                }

                Mouse mouse = Mouse.current;
                return mouse != null && mouse.rightButton.isPressed
                    ? mouse.delta.ReadValue()
                    : Vector2.zero;
            }
        }
        public float CameraPointerDeltaX => CameraPointerDelta.x;
        public Vector2 Navigate => !_suppressActionsUntilRelease && _navigate != null ? _navigate.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => !_suppressActionsUntilRelease && _jump != null && _jump.WasPressedThisFrame();
        public bool SwordPressed => !_suppressActionsUntilRelease && _sword != null && _sword.WasPressedThisFrame();
        public bool SpecialPressed => !_suppressActionsUntilRelease && _special != null && _special.WasPressedThisFrame();
        public bool MagicPressed => !_suppressActionsUntilRelease && _magic != null && _magic.WasPressedThisFrame();
        public bool PausePressed =>
            !_suppressActionsUntilRelease &&
            ((_pause != null && _pause.WasPressedThisFrame()) ||
             (_uiPause != null && _uiPause.WasPressedThisFrame()));
        public bool ConfirmPressed => !_suppressActionsUntilRelease && _confirm != null && _confirm.WasPressedThisFrame();
        public bool CancelPressed => !_suppressActionsUntilRelease && _cancel != null && _cancel.WasPressedThisFrame();
        public bool SettingsPressed =>
            !_suppressActionsUntilRelease &&
            ((_battleSettings != null && _battleSettings.WasPressedThisFrame()) ||
             (_uiSettings != null && _uiSettings.WasPressedThisFrame()) ||
             IsAnyGamepadSettingsButtonPressed());
        public bool IsRebinding => _rebindOperation != null || _waitingForRebindButtonRelease;
        public bool IsWaitingForRebindButtonRelease => _waitingForRebindButtonRelease;
        public GameInputSemantic? RebindingSemantic => _rebindSemantic;
        public float RebindSecondsRemaining => IsRebinding
            ? Mathf.Max(0f, rebindTimeoutSeconds - (Time.unscaledTime - _rebindStartedAt))
            : 0f;
        public GameInputContext Context { get; private set; }
        public string LastTriggeredAction { get; private set; } = string.Empty;
        public string LastRebindMessage { get; private set; } = string.Empty;
        public InputDevice LastUsedDevice { get; private set; }
        public bool LastUsedInputIsGamepad => LastUsedDevice is Gamepad;
        public InputDeviceDiagnostic DeviceDiagnostic { get; private set; } =
            new InputDeviceDiagnostic(null, null, string.Empty, false);
        public string CurrentDeviceDiagnostic => DeviceDiagnostic.ToString();
        public InputDeviceDiagnostic LastRawButtonDiagnostic { get; private set; } =
            new InputDeviceDiagnostic(null, null, string.Empty, false);
        public InputDeviceDiagnostic LastGamepadButtonDiagnostic { get; private set; } =
            new InputDeviceDiagnostic(null, null, string.Empty, false);
        public InputMode SelectedInputMode { get; private set; } = InputMode.Unselected;
        public InputMode PreferredInputModeForSelection => _preferredInputMode != InputMode.Unselected
            ? _preferredInputMode
            : HasConnectedGamepad
                ? InputMode.ControllerGamepad
                : InputMode.KeyboardMouse;
        public bool HasConnectedGamepad => Gamepad.all.Count > 0;
        public bool HasUnsupportedControllerDevice => FindUnsupportedControllerDevice() != null;
        public bool UsesSteamDesktopFallback => SelectedInputMode == InputMode.SteamDesktopCompatibility;
        public string PreferredRebindBindingGroup => SelectedInputMode switch
        {
            InputMode.ControllerGamepad => GamepadGroup,
            InputMode.SteamDesktopCompatibility => SteamDesktopGroup,
            _ => string.Empty
        };
        public string ActiveInputProfileName => SelectedInputMode switch
        {
            InputMode.KeyboardMouse => "Keyboard / Mouse",
            InputMode.ControllerGamepad => "Controller / Gamepad",
            InputMode.SteamDesktopCompatibility => "Steam Desktop互換（Keyboard/Mouse変換）",
            _ => "入力方式を選択してください"
        };
        public string ActiveControllerProfileName => ActiveInputProfileName;
        public string ConnectedControllersSummary => BuildConnectedControllersSummary();
        public string ControllerCompatibilityHint => BuildControllerCompatibilityHint();
        public InputActionAsset Actions
        {
            get
            {
                EnsureInitialized();
                return _actions;
            }
        }

        public event Action<Vector2> MoveChanged;
        public event Action JumpTriggered;
        public event Action SwordTriggered;
        public event Action SpecialTriggered;
        public event Action MagicTriggered;
        public event Action PauseTriggered;
        public event Action ConfirmTriggered;
        public event Action<InputDeviceDiagnostic> DeviceDiagnosticChanged;
        public event Action<InputDeviceDiagnostic> RawButtonDiagnosticChanged;
        public event Action BindingsChanged;
        public event Action<bool> RebindFinished;
        public event Action<InputMode> InputModeChanged;

        private void Awake()
        {
            EnsureInitialized();
            LoadPreferredInputModeFromPlayerPrefs();
            if (loadSavedBindingsOnAwake)
            {
                LoadBindingOverridesFromPlayerPrefs();
            }
        }

        private void OnEnable()
        {
            EnsureInitialized();
            InputSystem.onDeviceChange += OnDeviceChange;
            _rawButtonSubscription = InputSystem.onAnyButtonPress.Call(RecordRawButton);
            if (enableBattleOnEnable)
            {
                BeginInputModeSelection();
            }
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            _rawButtonSubscription?.Dispose();
            _rawButtonSubscription = null;
            CancelInteractiveRebind();
            DisableAll();
        }

        private void Update()
        {
            if (!IsRebinding)
            {
                RefreshNativeGamepadProfileRecovery();
                RefreshContextSwitchReleaseGate();
                return;
            }

            if (Time.unscaledTime - _rebindStartedAt >= Mathf.Max(3f, rebindTimeoutSeconds))
            {
                _rebindTimedOut = true;
                CancelInteractiveRebind();
                return;
            }

            if (IsRebindCancelPressed())
            {
                CancelInteractiveRebind();
                return;
            }

            if (_waitingForRebindButtonRelease && !IsAnyRebindCandidatePressed(_rebindBindingGroup))
            {
                _waitingForRebindButtonRelease = false;
                StartInteractiveRebindOperation();
            }
        }

        /// <summary>
        /// Releases the Battle/UI transition guard after every relevant control is neutral.
        /// Normally called by Update; exposed so deterministic input tests and non-standard
        /// hosts can advance the guard without relying on a rendered frame.
        /// </summary>
        public void RefreshContextSwitchReleaseGate()
        {
            if (_suppressActionsUntilRelease && !IsAnyContextSwitchControlActuated())
            {
                _suppressActionsUntilRelease = false;
            }
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

        private void OnDestroy()
        {
            UnsubscribeActions();
            if (_actions == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_actions);
            }
            else
            {
                DestroyImmediate(_actions);
            }
        }

        /// <summary>
        /// Enables the startup input chooser without activating a gameplay profile.
        /// The previous preference is intentionally retained only as the chooser's
        /// initial cursor; every process launch still requires an explicit choice.
        /// </summary>
        public void BeginInputModeSelection()
        {
            EnsureInitialized();
            if (IsRebinding)
            {
                CancelInteractiveRebind();
            }

            _battleMap.Disable();
            _actions.bindingMask = null;
            _uiMap.Enable();
            SelectedInputMode = InputMode.Unselected;
            Context = GameInputContext.InputSelection;
            _suppressActionsUntilRelease = true;
            InputModeChanged?.Invoke(SelectedInputMode);
        }

        /// <summary>
        /// Selects exactly one binding profile. Steam Desktop compatibility is never
        /// chosen implicitly because those events are indistinguishable from ordinary
        /// keyboard and mouse input once they reach Unity.
        /// </summary>
        public bool TrySelectInputMode(InputMode mode, out string message)
        {
            EnsureInitialized();
            if (Context != GameInputContext.InputSelection)
            {
                message = "入力方式の選択画面を開いてから選択してください。";
                return false;
            }

            string bindingGroup;
            switch (mode)
            {
                case InputMode.KeyboardMouse:
                    bindingGroup = KeyboardGroup;
                    message = "Keyboard / Mouseを使用します。";
                    break;
                case InputMode.ControllerGamepad:
                    if (!HasConnectedGamepad)
                    {
                        message = "Gamepadが検出されていません。CoffeeGAMEをSteamライブラリへ登録し、専用のGamepadレイアウトから起動してください。必要な場合だけ『Steam Desktop互換で続ける』を選べます。";
                        return false;
                    }
                    bindingGroup = GamepadGroup;
                    message = "Controller / Gamepadを使用します。";
                    break;
                case InputMode.SteamDesktopCompatibility:
                    bindingGroup = SteamDesktopGroup;
                    message = "Steam Desktop互換を使用します。SteamがKeyboard/Mouseへ変換した入力だけを受け付けます。";
                    break;
                default:
                    message = "使用する入力方式を選択してください。";
                    return false;
            }

            _actions.bindingMask = InputBinding.MaskByGroup(bindingGroup);
            _selectedBindingGroup = bindingGroup;
            SelectedInputMode = mode;
            _preferredInputMode = mode;
            _suppressActionsUntilRelease = true;

            if (!SavePreferredInputModeToPlayerPrefs())
            {
                message += " 前回値の保存には失敗しました。";
            }

            InputModeChanged?.Invoke(SelectedInputMode);
            return true;
        }

        public void EnableBattle()
        {
            EnsureInitialized();
            _actions.bindingMask = string.IsNullOrWhiteSpace(_selectedBindingGroup)
                ? null
                : InputBinding.MaskByGroup(_selectedBindingGroup);
            _uiMap.Disable();
            _battleMap.Enable();
            Context = GameInputContext.Battle;
            _suppressActionsUntilRelease = true;
        }

        public void DisableBattle()
        {
            if (_battleMap == null)
            {
                return;
            }

            _battleMap.Disable();
            if (Context == GameInputContext.Battle)
            {
                Context = GameInputContext.None;
            }
        }

        public void EnableUI()
        {
            EnsureInitialized();
            _battleMap.Disable();
            // Menus accept both native keyboard and Gamepad so either device can
            // recover the settings flow. Steam Desktop's synthetic Space=Cancel
            // binding stays isolated unless that compatibility mode was selected.
            string uiBindingGroups = KeyboardGroup + ";" + GamepadGroup;
            if (SelectedInputMode == InputMode.SteamDesktopCompatibility)
            {
                uiBindingGroups += ";" + SteamDesktopGroup;
            }
            _actions.bindingMask = InputBinding.MaskByGroup(uiBindingGroups);
            _uiMap.Enable();
            Context = GameInputContext.UI;
            _suppressActionsUntilRelease = true;
        }

        public void DisableUI()
        {
            if (_uiMap == null)
            {
                return;
            }

            _uiMap.Disable();
            if (Context == GameInputContext.UI)
            {
                Context = GameInputContext.None;
            }
        }

        public void DisableAll()
        {
            _battleMap?.Disable();
            _uiMap?.Disable();
            Context = GameInputContext.None;
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

        public string GetGamepadBindingDescription(GameInputSemantic semantic)
        {
            return GetBindingDescriptionForGroup(semantic, GamepadGroup);
        }

        public string GetSteamDesktopBindingDescription(GameInputSemantic semantic)
        {
            return GetBindingDescriptionForGroup(semantic, SteamDesktopGroup);
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

        private void LoadPreferredInputModeFromPlayerPrefs()
        {
            _preferredInputMode = InputMode.Unselected;
            try
            {
                if (!PlayerPrefs.HasKey(InputModePlayerPrefsKey))
                {
                    return;
                }

                int savedValue = PlayerPrefs.GetInt(
                    InputModePlayerPrefsKey,
                    (int)InputMode.Unselected);
                if (Enum.IsDefined(typeof(InputMode), savedValue))
                {
                    InputMode savedMode = (InputMode)savedValue;
                    if (savedMode != InputMode.Unselected)
                    {
                        _preferredInputMode = savedMode;
                        return;
                    }
                }

                PlayerPrefs.DeleteKey(InputModePlayerPrefsKey);
                PlayerPrefs.Save();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not load the preferred input mode: {exception.Message}", this);
            }
        }

        private bool SavePreferredInputModeToPlayerPrefs()
        {
            try
            {
                PlayerPrefs.SetInt(InputModePlayerPrefsKey, (int)_preferredInputMode);
                PlayerPrefs.Save();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save the preferred input mode: {exception.Message}", this);
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

        private void EnsureInitialized()
        {
            if (_actions != null)
            {
                return;
            }

            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            _actions.name = "CoffeeGame Runtime Input";

            _battleMap = _actions.AddActionMap("Battle");
            _move = _battleMap.AddAction("Move", InputActionType.Value);
            _move.expectedControlType = "Vector2";
            AddMovementBindings(_move);
            _cameraYaw = _battleMap.AddAction("CameraYaw", InputActionType.Value);
            _cameraYaw.expectedControlType = "Axis";
            AddCameraYawBindings(_cameraYaw);
            _cameraPitch = _battleMap.AddAction("CameraPitch", InputActionType.Value);
            _cameraPitch.expectedControlType = "Axis";
            AddCameraPitchBindings(_cameraPitch);
            _jump = AddButton(_battleMap, "Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth", "<Keyboard>/enter");
            _sword = AddButton(_battleMap, "Sword", "<Keyboard>/f", "<Gamepad>/rightTrigger", "<Mouse>/leftButton");
            _sword.AddBinding("<Mouse>/leftButton", groups: KeyboardGroup);
            _special = AddButton(_battleMap, "Special", "<Keyboard>/q", "<Gamepad>/buttonWest", "<Keyboard>/pageUp");
            _magic = AddButton(_battleMap, "Magic", "<Keyboard>/e", "<Gamepad>/buttonNorth", "<Keyboard>/pageDown");
            _pause = AddButton(_battleMap, "Pause", "<Keyboard>/escape", "<Gamepad>/start", "<Keyboard>/escape");
            _battleSettings = AddSettingsButton(_battleMap);

            _uiMap = _actions.AddActionMap("UI");
            _navigate = _uiMap.AddAction("Navigate", InputActionType.Value);
            _navigate.expectedControlType = "Vector2";
            AddNavigationBindings(_navigate);
            _confirm = _uiMap.AddAction("Confirm", InputActionType.Button);
            _confirm.AddBinding("<Keyboard>/enter", groups: MenuFallbackGroups);
            _confirm.AddBinding("<Gamepad>/buttonSouth", groups: GamepadGroup);
            _confirm.AddBinding("<Gamepad>/start", groups: GamepadGroup);
            _cancel = _uiMap.AddAction("Cancel", InputActionType.Button);
            _cancel.expectedControlType = "Button";
            _cancel.AddBinding("<Keyboard>/escape", groups: MenuFallbackGroups);
            _cancel.AddBinding("<Keyboard>/space", groups: SteamDesktopGroup);
            _cancel.AddBinding("<Gamepad>/buttonEast", groups: GamepadGroup);
            _uiPause = AddButton(_uiMap, "Pause", "<Keyboard>/escape", "<Gamepad>/start", "<Keyboard>/escape");
            _uiSettings = AddSettingsButton(_uiMap);

            SubscribeActions();
        }

        private static InputAction AddButton(
            InputActionMap map,
            string name,
            string keyboardPath,
            string gamepadPath,
            string steamDesktopPath)
        {
            var action = map.AddAction(name, InputActionType.Button);
            action.expectedControlType = "Button";
            action.AddBinding(keyboardPath, groups: KeyboardGroup);
            action.AddBinding(gamepadPath, groups: GamepadGroup);
            if (!string.IsNullOrWhiteSpace(steamDesktopPath))
            {
                action.AddBinding(steamDesktopPath, groups: SteamDesktopGroup);
            }
            return action;
        }

        private static InputAction AddSettingsButton(InputActionMap map)
        {
            InputAction action = map.AddAction("InputSettings", InputActionType.Button);
            action.expectedControlType = "Button";
            action.AddBinding("<Keyboard>/tab", groups: MenuFallbackGroups);
            action.AddBinding("<Gamepad>/select", groups: GamepadGroup);
            return action;
        }

        private const string MenuFallbackGroups =
            KeyboardGroup + ";" + GamepadGroup + ";" + SteamDesktopGroup;

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

        private InputAction ResolveAction(GameInputSemantic semantic)
        {
            return semantic switch
            {
                GameInputSemantic.Move => _move,
                GameInputSemantic.Jump => _jump,
                GameInputSemantic.Sword => _sword,
                GameInputSemantic.Special => _special,
                GameInputSemantic.Magic => _magic,
                GameInputSemantic.Pause => _pause,
                GameInputSemantic.Navigate => _navigate,
                GameInputSemantic.Confirm => _confirm,
                _ => throw new ArgumentOutOfRangeException(nameof(semantic), semantic, null)
            };
        }

        private void SubscribeActions()
        {
            _move.performed += OnMove;
            _move.canceled += OnMove;
            _cameraYaw.performed += OnCameraYaw;
            _cameraYaw.canceled += OnCameraYaw;
            _cameraPitch.performed += OnCameraPitch;
            _cameraPitch.canceled += OnCameraPitch;
            _navigate.performed += OnNavigate;
            _navigate.canceled += OnNavigate;
            _jump.performed += OnJump;
            _sword.performed += OnSword;
            _special.performed += OnSpecial;
            _magic.performed += OnMagic;
            _pause.performed += OnPause;
            _uiPause.performed += OnUiPause;
            _confirm.performed += OnConfirm;
            _cancel.performed += OnCancel;
            _battleSettings.performed += OnBattleSettings;
            _uiSettings.performed += OnUiSettings;
        }

        private void UnsubscribeActions()
        {
            if (_move == null)
            {
                return;
            }

            _move.performed -= OnMove;
            _move.canceled -= OnMove;
            _cameraYaw.performed -= OnCameraYaw;
            _cameraYaw.canceled -= OnCameraYaw;
            _cameraPitch.performed -= OnCameraPitch;
            _cameraPitch.canceled -= OnCameraPitch;
            _navigate.performed -= OnNavigate;
            _navigate.canceled -= OnNavigate;
            _jump.performed -= OnJump;
            _sword.performed -= OnSword;
            _special.performed -= OnSpecial;
            _magic.performed -= OnMagic;
            _pause.performed -= OnPause;
            _uiPause.performed -= OnUiPause;
            _confirm.performed -= OnConfirm;
            _cancel.performed -= OnCancel;
            _battleSettings.performed -= OnBattleSettings;
            _uiSettings.performed -= OnUiSettings;
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Move");
            MoveChanged?.Invoke(context.ReadValue<Vector2>());
        }

        private void OnCameraYaw(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/CameraYaw");
        }

        private void OnCameraPitch(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/CameraPitch");
        }

        private void OnJump(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Jump");
            JumpTriggered?.Invoke();
        }

        private void OnNavigate(InputAction.CallbackContext context)
        {
            RecordInput(context, "UI/Navigate");
        }

        private void OnSword(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Sword");
            SwordTriggered?.Invoke();
        }

        private void OnSpecial(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Special");
            SpecialTriggered?.Invoke();
        }

        private void OnMagic(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Magic");
            MagicTriggered?.Invoke();
        }

        private void OnPause(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Pause");
            PauseTriggered?.Invoke();
        }

        private void OnUiPause(InputAction.CallbackContext context)
        {
            RecordInput(context, "UI/Pause");
            PauseTriggered?.Invoke();
        }

        private void OnConfirm(InputAction.CallbackContext context)
        {
            RecordInput(context, "UI/Confirm");
            ConfirmTriggered?.Invoke();
        }

        private void OnCancel(InputAction.CallbackContext context)
        {
            RecordInput(context, "UI/Cancel");
        }

        private void OnBattleSettings(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/InputSettings");
        }

        private void OnUiSettings(InputAction.CallbackContext context)
        {
            RecordInput(context, "UI/InputSettings");
        }

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

        private static bool IsAnyRebindCandidatePressed(string bindingGroup)
        {
            return bindingGroup == SteamDesktopGroup
                ? IsAnySteamDesktopButtonPressed()
                : IsAnyGamepadButtonPressed();
        }

        private bool IsAnyContextSwitchControlActuated()
        {
            if (IsAnyNativeGamepadControlActuated())
            {
                return true;
            }

            // Native Gamepad battle must ignore leftover keyboard / IME state
            // from the rival answer editor. Those keys are not battle actions.
            if (SelectedInputMode == InputMode.ControllerGamepad
                && Context == GameInputContext.Battle)
            {
                return false;
            }

            return IsAnySteamDesktopButtonPressed();
        }

        private static bool IsAnyNativeGamepadControlActuated()
        {
            if (IsAnyGamepadButtonPressed())
            {
                return true;
            }

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
