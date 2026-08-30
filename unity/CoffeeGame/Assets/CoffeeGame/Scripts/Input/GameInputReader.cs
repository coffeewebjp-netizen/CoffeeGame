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

        private const string KeyboardGroup = "Keyboard";

        public const string GamepadBindingGroup = "Gamepad";

        public const string SteamDesktopBindingGroup = "SteamDesktop";

        public const string TouchBindingGroup = "Touch";

        public const string InputModePlayerPrefsKey = "CoffeeGame.Input.Mode.v1";

        private const string GamepadGroup = GamepadBindingGroup;

        private const string SteamDesktopGroup = SteamDesktopBindingGroup;

        private const string TouchGroup = TouchBindingGroup;


        [SerializeField] private bool enableBattleOnEnable = true;

        [SerializeField] private bool loadSavedBindingsOnAwake = true;

        [SerializeField] private bool saveBindingsAfterRebind = true;

        [SerializeField, Min(3f)] private float rebindTimeoutSeconds = 10f;

        [SerializeField] private string bindingOverridesPlayerPrefsKey = "CoffeeGame.Input.GamepadBindings.v2";

        [SerializeField] private string desktopBindingOverridesPlayerPrefsKey = "CoffeeGame.Input.SteamDesktopBindings.v1";


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

        private InputAction _dodge;

        private InputAction _pause;

        private InputAction _navigate;

        private InputAction _confirm;

        private InputAction _cancel;

        private InputAction _uiPause;

        private InputAction _battleSettings;

        private InputAction _uiSettings;

        private bool _suppressActionsUntilRelease;

        private InputControl _lastControl;

        private IDisposable _rawButtonSubscription;

        private InputMode _preferredInputMode;

        private string _selectedBindingGroup = string.Empty;


        public Vector2 Move
        {
            get
            {
                if (_suppressActionsUntilRelease)
                {
                    return Vector2.zero;
                }

                if (UsesTouchOverlay)
                {
                    return _touchMove;
                }

                return _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
            }
        }

        public float CameraYaw
        {
            get
            {
                if (_suppressActionsUntilRelease)
                {
                    return 0f;
                }

                if (UsesTouchOverlay)
                {
                    return _touchCamera.x;
                }

                return _cameraYaw != null ? _cameraYaw.ReadValue<float>() : 0f;
            }
        }

        public float CameraPitch
        {
            get
            {
                if (_suppressActionsUntilRelease)
                {
                    return 0f;
                }

                if (UsesTouchOverlay)
                {
                    return _touchCamera.y;
                }

                return _cameraPitch != null ? _cameraPitch.ReadValue<float>() : 0f;
            }
        }

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

        public Vector2 Navigate
        {
            get
            {
                if (_suppressActionsUntilRelease)
                {
                    return Vector2.zero;
                }

                if (UsesTouchOverlay)
                {
                    return _touchMove;
                }

                return _navigate != null ? _navigate.ReadValue<Vector2>() : Vector2.zero;
            }
        }

        public bool JumpPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchJumpPressed) || (_jump != null && _jump.WasPressedThisFrame()));

        public bool SwordPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchSwordPressed) || (_sword != null && _sword.WasPressedThisFrame()));

        public bool SpecialPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchSpecialPressed) || (_special != null && _special.WasPressedThisFrame()));

        public bool MagicPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchMagicPressed) || (_magic != null && _magic.WasPressedThisFrame()));

        public bool DodgePressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchDodgePressed) || (_dodge != null && _dodge.WasPressedThisFrame()));

        public bool PausePressed =>
            !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchPausePressed) ||
             (_pause != null && _pause.WasPressedThisFrame()) ||
             (_uiPause != null && _uiPause.WasPressedThisFrame()));

        public bool ConfirmPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchConfirmPressed) || (_confirm != null && _confirm.WasPressedThisFrame()));

        public bool CancelPressed => !_suppressActionsUntilRelease &&
            ((UsesTouchOverlay && _touchCancelPressed) || (_cancel != null && _cancel.WasPressedThisFrame()));

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
            : Application.isMobilePlatform
                ? InputMode.TouchOnScreen
                : HasConnectedGamepad
                    ? InputMode.ControllerGamepad
                    : InputMode.KeyboardMouse;

        public bool UsesTouchOverlay => SelectedInputMode == InputMode.TouchOnScreen;

        public bool HasConnectedGamepad => Gamepad.all.Count > 0;

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
            InputMode.TouchOnScreen => "タッチ（画面操作）",
            _ => "入力方式を選択してください"
        };

        public string ActiveControllerProfileName => ActiveInputProfileName;

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

        public event Action DodgeTriggered;

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


        private void LateUpdate()
        {
            ClearQueuedTouchPresses();
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
                case InputMode.TouchOnScreen:
                    bindingGroup = TouchGroup;
                    message = "画面タッチを使用します。横画面で、左半分をスワイプして押しっぱなしで移動、右半分でカメラです。";
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


        /// <summary>
        /// Returns to Battle after the rival answer editor. Keyboard/IME text entry
        /// can leave stale Gamepad pressed-state and must not keep the release gate
        /// closed. A held South/trigger from the previous UI confirm is still gated.
        /// </summary>
        public void RestoreBattleAfterTextEntry()
        {
            EnsureInitialized();
            Keyboard.current?.SetIMEEnabled(false);
            ReenableConnectedGamepads();

            if (HasConnectedGamepad
                && SelectedInputMode != InputMode.TouchOnScreen
                && (SelectedInputMode == InputMode.ControllerGamepad
                    || SelectedInputMode == InputMode.Unselected
                    || _preferredInputMode == InputMode.ControllerGamepad))
            {
                _selectedBindingGroup = GamepadGroup;
                SelectedInputMode = InputMode.ControllerGamepad;
                _preferredInputMode = InputMode.ControllerGamepad;
                if (Gamepad.current != null)
                {
                    LastUsedDevice = Gamepad.current;
                }
            }

            EnableBattle();
            if (!IsCombatLeakControlHeld())
            {
                _suppressActionsUntilRelease = false;
            }
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


        private bool IsAnyContextSwitchControlActuated()
        {
            if (IsCombatLeakControlHeld() || IsAnyNativeGamepadStickActuated())
            {
                return true;
            }

            // Native Gamepad battle must ignore leftover keyboard / IME state
            // and stale synthetic Gamepad buttons after text entry.
            if (SelectedInputMode == InputMode.ControllerGamepad
                && Context == GameInputContext.Battle)
            {
                return false;
            }

            return IsAnySteamDesktopButtonPressed();
        }
    }
}
