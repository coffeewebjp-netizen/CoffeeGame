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
            _dodge = AddButton(_battleMap, "Dodge", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder", "<Keyboard>/leftShift");
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


        private InputAction ResolveAction(GameInputSemantic semantic)
        {
            return semantic switch
            {
                GameInputSemantic.Move => _move,
                GameInputSemantic.Jump => _jump,
                GameInputSemantic.Sword => _sword,
                GameInputSemantic.Special => _special,
                GameInputSemantic.Magic => _magic,
                GameInputSemantic.Dodge => _dodge,
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
            _dodge.performed += OnDodge;
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
            _dodge.performed -= OnDodge;
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


        private void OnDodge(InputAction.CallbackContext context)
        {
            RecordInput(context, "Battle/Dodge");
            DodgeTriggered?.Invoke();
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
    }
}
