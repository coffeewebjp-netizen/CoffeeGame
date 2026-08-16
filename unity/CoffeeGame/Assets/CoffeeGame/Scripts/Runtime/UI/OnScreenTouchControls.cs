using CoffeeGame.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CoffeeGame.UI
{
    /// <summary>
    /// Pixel-first virtual stick and action pad for Android. Writes into
    /// <see cref="GameInputReader"/> instead of mixing Touchscreen bindings
    /// with keyboard or gamepad groups.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class OnScreenTouchControls : MonoBehaviour
    {
        private GameInputReader input;
        private bool visible;
        private int moveFingerId = -1;
        private int cameraFingerId = -1;
        private Vector2 moveOrigin;
        private Vector2 lastCameraPosition;
        private bool jumpHeld;
        private bool swordHeld;
        private bool specialHeld;
        private bool magicHeld;
        private GUIStyle labelStyle;
        private GUIStyle captionStyle;

        public void Initialize(GameInputReader inputReader)
        {
            input = inputReader;
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            if (!isVisible)
            {
                ResetTouches();
            }
        }

        private void Update()
        {
            if (input == null || !input.UsesTouchOverlay)
            {
                ResetTouches();
                return;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                input.SetTouchMove(Vector2.zero);
                input.SetTouchCamera(Vector2.zero);
                return;
            }

            Vector2 move = Vector2.zero;
            Vector2 camera = Vector2.zero;
            bool sawMove = false;
            bool jump = false;
            bool sword = false;
            bool special = false;
            bool magic = false;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed && !touch.press.wasPressedThisFrame)
                {
                    continue;
                }

                Vector2 position = touch.position.ReadValue();
                int fingerId = touch.touchId.ReadValue();
                if (IsInside(position, JumpRect))
                {
                    jump = true;
                }
                else if (IsInside(position, SwordRect))
                {
                    sword = true;
                }
                else if (IsInside(position, SpecialRect))
                {
                    special = true;
                }
                else if (IsInside(position, MagicRect))
                {
                    magic = true;
                }
                else if (moveFingerId == fingerId || (moveFingerId < 0 && IsInside(position, MovePadRect)))
                {
                    if (moveFingerId < 0)
                    {
                        moveFingerId = fingerId;
                        moveOrigin = MovePadCenter;
                    }

                    move = Vector2.ClampMagnitude((position - moveOrigin) / MoveRadius, 1f);
                    sawMove = true;
                }
                else if (position.x > Screen.width * 0.45f)
                {
                    if (cameraFingerId != fingerId)
                    {
                        cameraFingerId = fingerId;
                        lastCameraPosition = position;
                    }
                    else
                    {
                        Vector2 delta = position - lastCameraPosition;
                        camera = new Vector2(delta.x / 48f, -delta.y / 64f);
                        lastCameraPosition = position;
                    }
                }
            }

            if (!sawMove)
            {
                moveFingerId = -1;
            }

            if (cameraFingerId >= 0)
            {
                bool cameraStillHeld = false;
                foreach (TouchControl touch in touchscreen.touches)
                {
                    if (touch.touchId.ReadValue() == cameraFingerId && touch.press.isPressed)
                    {
                        cameraStillHeld = true;
                        break;
                    }
                }

                if (!cameraStillHeld)
                {
                    cameraFingerId = -1;
                }
            }

            input.SetTouchMove(move);
            input.SetTouchCamera(camera);
            if (jump && !jumpHeld)
            {
                input.QueueTouchPress(GameInputSemantic.Jump);
            }

            if (sword && !swordHeld)
            {
                input.QueueTouchPress(GameInputSemantic.Sword);
            }

            if (special && !specialHeld)
            {
                input.QueueTouchPress(GameInputSemantic.Special);
            }

            if (magic && !magicHeld)
            {
                input.QueueTouchPress(GameInputSemantic.Magic);
            }

            jumpHeld = jump;
            swordHeld = sword;
            specialHeld = special;
            magicHeld = magic;
        }

        private void OnGUI()
        {
            if (!visible || input == null || !input.UsesTouchOverlay)
            {
                return;
            }

            EnsureStyles();
            DrawPad(MovePadRect, "移動");
            DrawButton(JumpRect, "跳");
            DrawButton(SwordRect, "刀");
            DrawButton(SpecialRect, "居合");
            DrawButton(MagicRect, "氷");
            GUI.Label(
                new Rect(Screen.width * 0.42f, Screen.height - Scaled(56f), Screen.width * 0.2f, Scaled(36f)),
                "右ドラッグ: カメラ",
                captionStyle);
        }

        private void ResetTouches()
        {
            moveFingerId = -1;
            cameraFingerId = -1;
            jumpHeld = false;
            swordHeld = false;
            specialHeld = false;
            magicHeld = false;
            if (input != null)
            {
                input.SetTouchMove(Vector2.zero);
                input.SetTouchCamera(Vector2.zero);
            }
        }

        private static Rect MovePadRect =>
            new Rect(Scaled(28f), Screen.height - Scaled(280f), Scaled(240f), Scaled(240f));

        private static Vector2 MovePadCenter => MovePadRect.center;

        private static float MoveRadius => MovePadRect.width * 0.42f;

        private static Rect JumpRect => ButtonRect(0, 1);
        private static Rect SwordRect => ButtonRect(1, 1);
        private static Rect SpecialRect => ButtonRect(0, 0);
        private static Rect MagicRect => ButtonRect(1, 0);

        private static Rect ButtonRect(int column, int row)
        {
            float size = Scaled(108f);
            float gap = Scaled(16f);
            float left = Screen.width - Scaled(28f) - (size * 2f) - gap;
            float top = Screen.height - Scaled(28f) - (size * 2f) - gap;
            return new Rect(left + column * (size + gap), top + row * (size + gap), size, size);
        }

        private static float Scaled(float value)
        {
            return value * Mathf.Clamp(Screen.dpi <= 1f ? 1f : Screen.dpi / 160f, 1f, 3.2f);
        }

        private static bool IsInside(Vector2 screenPosition, Rect rect)
        {
            float y = Screen.height - screenPosition.y;
            return rect.Contains(new Vector2(screenPosition.x, y));
        }

        private void DrawPad(Rect rect, string label)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.16f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, label, labelStyle);
        }

        private void DrawButton(Rect rect, string label)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0.99f, 0.66f, 0.24f, 0.28f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(rect, label, labelStyle);
        }

        private void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Scaled(28f)),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            labelStyle.normal.textColor = Color.white;
            captionStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(Scaled(16f))
            };
            captionStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }
    }
}
