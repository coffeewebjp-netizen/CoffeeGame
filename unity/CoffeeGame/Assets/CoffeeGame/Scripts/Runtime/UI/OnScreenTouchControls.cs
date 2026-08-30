using CoffeeGame.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace CoffeeGame.UI
{
    /// <summary>
    /// Landscape twin-zone overlay: left half is a swipe-and-hold move stick
    /// that appears at the finger, right half looks the camera, and combat
    /// buttons sit in the lower-right thumb cluster.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class OnScreenTouchControls : MonoBehaviour
    {
        private const float CameraPixelsPerYaw = 42f;
        private const float CameraPixelsPerPitch = 56f;

        private GameInputReader input;
        private bool visible;
        private int moveFingerId = -1;
        private int cameraFingerId = -1;
        private Vector2 moveOrigin;
        private Vector2 currentMovePosition;
        private Vector2 lastCameraPosition;
        private bool jumpHeld;
        private bool swordHeld;
        private bool specialHeld;
        private bool magicHeld;
        private bool dodgeHeld;
        private GUIStyle labelStyle;
        private Texture2D circleTexture;

        public void Initialize(GameInputReader inputReader)
        {
            input = inputReader;
            ApplyLandscapeOrientation();
        }

        public void SetVisible(bool isVisible)
        {
            visible = isVisible;
            if (!isVisible)
            {
                ResetTouches();
            }
        }

        private void OnEnable()
        {
            ApplyLandscapeOrientation();
        }

        private void Update()
        {
            if (input == null || !input.UsesTouchOverlay)
            {
                ResetTouches();
                return;
            }

            ApplyLandscapeOrientation();
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
            bool dodge = false;

            foreach (TouchControl touch in touchscreen.touches)
            {
                if (!touch.press.isPressed && !touch.press.wasPressedThisFrame)
                {
                    continue;
                }

                Vector2 position = touch.position.ReadValue();
                int fingerId = touch.touchId.ReadValue();
                bool onJump = IsInside(position, JumpRect);
                bool onSword = IsInside(position, SwordRect);
                bool onSpecial = IsInside(position, SpecialRect);
                bool onMagic = IsInside(position, MagicRect);
                bool onDodge = IsInside(position, DodgeRect);
                bool onAction = onJump || onSword || onSpecial || onMagic || onDodge;

                if (onJump)
                {
                    jump = true;
                    continue;
                }

                if (onDodge)
                {
                    dodge = true;
                    continue;
                }

                if (onSword)
                {
                    sword = true;
                    continue;
                }

                if (onSpecial)
                {
                    special = true;
                    continue;
                }

                if (onMagic)
                {
                    magic = true;
                    continue;
                }

                if (moveFingerId == fingerId || (moveFingerId < 0 && !onAction && IsLeftHalf(position)))
                {
                    if (moveFingerId < 0)
                    {
                        moveFingerId = fingerId;
                        moveOrigin = position;
                    }

                    currentMovePosition = position;
                    move = TouchOverlayMath.ResolveHoldMove(moveOrigin, position);
                    sawMove = true;
                    continue;
                }

                if (cameraFingerId == fingerId || (cameraFingerId < 0 && !onAction && IsRightHalf(position)))
                {
                    if (cameraFingerId != fingerId)
                    {
                        cameraFingerId = fingerId;
                        lastCameraPosition = position;
                    }
                    else
                    {
                        Vector2 delta = position - lastCameraPosition;
                        camera = new Vector2(delta.x / CameraPixelsPerYaw, -delta.y / CameraPixelsPerPitch);
                        lastCameraPosition = position;
                    }
                }
            }

            if (!sawMove)
            {
                moveFingerId = -1;
            }

            if (cameraFingerId >= 0 && !IsFingerHeld(touchscreen, cameraFingerId))
            {
                cameraFingerId = -1;
            }

            input.SetTouchMove(move);
            input.SetTouchCamera(camera);
            QueueIfNewlyPressed(jump, ref jumpHeld, GameInputSemantic.Jump);
            QueueIfNewlyPressed(sword, ref swordHeld, GameInputSemantic.Sword);
            QueueIfNewlyPressed(special, ref specialHeld, GameInputSemantic.Special);
            QueueIfNewlyPressed(magic, ref magicHeld, GameInputSemantic.Magic);
            QueueIfNewlyPressed(dodge, ref dodgeHeld, GameInputSemantic.Dodge);
        }

        private void OnGUI()
        {
            if (!visible || input == null || !input.UsesTouchOverlay)
            {
                return;
            }

            EnsureStyles();
            DrawActionButton(JumpRect, "跳");
            DrawActionButton(SwordRect, "刀");
            DrawActionButton(SpecialRect, "居合");
            DrawActionButton(MagicRect, "氷");
            DrawActionButton(DodgeRect, "避");

            if (moveFingerId >= 0)
            {
                DrawDynamicStick(moveOrigin, currentMovePosition);
            }
        }

        private void DrawDynamicStick(Vector2 originBottomLeft, Vector2 currentBottomLeft)
        {
            Vector2 originGui = ToGui(originBottomLeft);
            Vector2 currentGui = ToGui(currentBottomLeft);
            float ring = TouchOverlayMath.MoveFullRadius * 2f;
            DrawCircle(new Rect(originGui.x - ring * 0.5f, originGui.y - ring * 0.5f, ring, ring), new Color(1f, 1f, 1f, 0.14f));
            float knob = 56f;
            DrawCircle(new Rect(currentGui.x - knob * 0.5f, currentGui.y - knob * 0.5f, knob, knob), new Color(1f, 1f, 1f, 0.32f));
        }

        private void ResetTouches()
        {
            moveFingerId = -1;
            cameraFingerId = -1;
            jumpHeld = false;
            swordHeld = false;
            specialHeld = false;
            magicHeld = false;
            dodgeHeld = false;
            if (input != null)
            {
                input.SetTouchMove(Vector2.zero);
                input.SetTouchCamera(Vector2.zero);
            }
        }

        private static void ApplyLandscapeOrientation()
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            if (Screen.orientation != ScreenOrientation.LandscapeLeft
                && Screen.orientation != ScreenOrientation.LandscapeRight)
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
            }
        }

        private void QueueIfNewlyPressed(bool held, ref bool wasHeld, GameInputSemantic semantic)
        {
            if (held && !wasHeld)
            {
                input.QueueTouchPress(semantic);
            }

            wasHeld = held;
        }

        private static bool IsFingerHeld(Touchscreen touchscreen, int fingerId)
        {
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.touchId.ReadValue() == fingerId && touch.press.isPressed)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsLeftHalf(Vector2 screenPosition) => screenPosition.x < Screen.width * 0.5f;

        private static bool IsRightHalf(Vector2 screenPosition) => screenPosition.x >= Screen.width * 0.5f;

        private static Rect JumpRect => new Rect(
            Screen.width - Scaled(28f) - ButtonSize * 2f - Scaled(18f),
            Screen.height - Scaled(36f) - ButtonSize,
            ButtonSize,
            ButtonSize);

        private static Rect SwordRect => new Rect(
            Screen.width - Scaled(28f) - ButtonSize,
            Screen.height - Scaled(36f) - ButtonSize,
            ButtonSize * 1.08f,
            ButtonSize * 1.08f);

        private static Rect SpecialRect => new Rect(
            Screen.width - Scaled(28f) - ButtonSize * 2f - Scaled(18f),
            Screen.height - Scaled(36f) - ButtonSize * 2f - Scaled(16f),
            ButtonSize * 0.92f,
            ButtonSize * 0.92f);

        private static Rect MagicRect => new Rect(
            Screen.width - Scaled(28f) - ButtonSize,
            Screen.height - Scaled(36f) - ButtonSize * 2f - Scaled(16f),
            ButtonSize * 0.92f,
            ButtonSize * 0.92f);

        private static Rect DodgeRect => new Rect(
            Screen.width - Scaled(28f) - ButtonSize * 3f - Scaled(36f),
            Screen.height - Scaled(36f) - ButtonSize,
            ButtonSize * 0.92f,
            ButtonSize * 0.92f);

        private static float ButtonSize => Scaled(104f);

        private static float Scaled(float value)
        {
            return value * Mathf.Clamp(Screen.dpi <= 1f ? 1f : Screen.dpi / 160f, 1f, 3.2f);
        }

        private static bool IsInside(Vector2 screenPosition, Rect rect)
        {
            return rect.Contains(ToGui(screenPosition));
        }

        private static Vector2 ToGui(Vector2 screenPosition)
        {
            return new Vector2(screenPosition.x, Screen.height - screenPosition.y);
        }

        private void DrawActionButton(Rect rect, string label)
        {
            DrawCircle(rect, new Color(0.99f, 0.66f, 0.24f, 0.34f));
            GUI.Label(rect, label, labelStyle);
        }

        private void DrawCircle(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, CircleTexture);
            GUI.color = previous;
        }

        private Texture2D CircleTexture
        {
            get
            {
                if (circleTexture != null)
                {
                    return circleTexture;
                }

                const int size = 64;
                circleTexture = new Texture2D(size, size, TextureFormat.ARGB32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear
                };
                Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float radius = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), center);
                        float alpha = Mathf.Clamp01((radius - distance) / 2.4f);
                        circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                circleTexture.Apply();
                return circleTexture;
            }
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
                fontSize = Mathf.RoundToInt(Scaled(26f)),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            labelStyle.normal.textColor = Color.white;
        }

        private void OnDestroy()
        {
            if (circleTexture != null)
            {
                Destroy(circleTexture);
            }
        }
    }
}
