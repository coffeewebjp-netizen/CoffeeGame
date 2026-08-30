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
        private Vector2 _touchMove;

        private Vector2 _touchCamera;

        private bool _touchJumpPressed;

        private bool _touchSwordPressed;

        private bool _touchSpecialPressed;

        private bool _touchMagicPressed;

        private bool _touchDodgePressed;

        private bool _touchPausePressed;

        private bool _touchConfirmPressed;

        private bool _touchCancelPressed;


        public void SetTouchMove(Vector2 move)
        {
            _touchMove = Vector2.ClampMagnitude(move, 1f);
        }


        public void SetTouchCamera(Vector2 camera)
        {
            _touchCamera = camera;
        }


        public void QueueTouchPress(GameInputSemantic semantic)
        {
            switch (semantic)
            {
                case GameInputSemantic.Jump:
                    _touchJumpPressed = true;
                    break;
                case GameInputSemantic.Sword:
                    _touchSwordPressed = true;
                    break;
                case GameInputSemantic.Special:
                    _touchSpecialPressed = true;
                    break;
                case GameInputSemantic.Magic:
                    _touchMagicPressed = true;
                    break;
                case GameInputSemantic.Dodge:
                    _touchDodgePressed = true;
                    break;
                case GameInputSemantic.Pause:
                    _touchPausePressed = true;
                    break;
                case GameInputSemantic.Confirm:
                    _touchConfirmPressed = true;
                    break;
                default:
                    break;
            }
        }


        public void QueueTouchCancel()
        {
            _touchCancelPressed = true;
        }


        public void ClearQueuedTouchPresses()
        {
            _touchJumpPressed = false;
            _touchSwordPressed = false;
            _touchSpecialPressed = false;
            _touchMagicPressed = false;
            _touchDodgePressed = false;
            _touchPausePressed = false;
            _touchConfirmPressed = false;
            _touchCancelPressed = false;
        }
    }
}
