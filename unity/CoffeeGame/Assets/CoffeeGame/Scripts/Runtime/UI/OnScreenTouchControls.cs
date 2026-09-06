using System.Collections.Generic;
using CoffeeGame.Combat;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CoffeeGame.UI
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public sealed class OnScreenTouchControls : MonoBehaviour
    {
        private GameInputReader input;
        private CombatRunController run;
        private bool visible;
        private bool focusLost, paused;
        private bool Suspended => focusLost || paused;
        private Canvas touchCanvas;
        private Sprite circleSprite;
        private Font font;
        private bool ownsFont;
        private readonly Dictionary<GameInputSemantic, Image> buttonImages = new Dictionary<GameInputSemantic, Image>();
        private readonly Dictionary<GameInputSemantic, Text> buttonLabels = new Dictionary<GameInputSemantic, Text>();
        private Image stickImage, knobImage;
        private Text hint;
        private Texture2D circleTexture;
        private readonly TouchGestureRouter gestures = new TouchGestureRouter();
        private readonly HashSet<int> activeTouchIds = new HashSet<int>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private TouchControlLayout layout;
        private Vector2 screenSize;
        private Rect rawSafeArea;
        public TouchControlLayout Layout => layout;
        public bool IsVisible => visible && input != null && input.UsesTouchOverlay;

        public void Initialize(GameInputReader reader, CombatRunController controller = null)
        { input = reader; run = controller; ApplyLandscapeOrientation(); RefreshLayout(); }
        public void SetVisible(bool value) { if (visible && !value) ResetTouches(); visible = value; if (touchCanvas != null) touchCanvas.enabled = IsVisible && !Suspended; }
        private void RefreshLayout()
        {
            var size = new Vector2(Screen.width, Screen.height); Rect safe = Screen.safeArea;
            if (layout != null && size == screenSize && rawSafeArea == safe) return;
            ResetTouches(); screenSize = size; rawSafeArea = safe; layout = new TouchControlLayout(size.x, size.y, safe);
        }
        private void Update()
        {
            RefreshLayout();
            if (!IsVisible || Suspended || input.Context != GameInputContext.Battle || Touchscreen.current == null)
            { ResetTouches(); return; }
            gestures.BeginFrame();
            // Ended slots retain old IDs. Only currently pressed IDs define
            // ownership, so an old slot cannot cancel a recycled active ID.
            activeTouchIds.Clear();
            foreach (var touch in Touchscreen.current.touches) if (touch.press.isPressed) activeTouchIds.Add(touch.touchId.ReadValue());
            gestures.ReleaseMissingTouches(activeTouchIds);
            foreach (var touch in Touchscreen.current.touches)
            {
                if (!touch.press.isPressed) continue;
                Vector2 point = touch.position.ReadValue(); bool began = touch.press.wasPressedThisFrame;
                gestures.Process(touch.touchId.ReadValue(), point, began, layout, began && IsOverMenuControl(point));
            }
            gestures.EndFrame();
            input.SetTouchMove(gestures.Move); input.SetTouchCamera(gestures.Camera);
            input.SetTouchGuardHeld(gestures.IsHeld(GameInputSemantic.Guard));
            foreach (var action in gestures.Presses) input.QueueTouchPress(action);
        }
        private bool IsOverMenuControl(Vector2 point)
        {
            if (EventSystem.current == null) return false;
            uiHits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            foreach (var hit in uiHits) if (hit.gameObject.GetComponentInParent<Selectable>() != null) return true;
            return false;
        }
        private void LateUpdate()
        {
            if (touchCanvas != null) touchCanvas.enabled = IsVisible && !Suspended;
            if (!IsVisible || layout == null || Suspended) return;
            EnsureVisuals(); touchCanvas.enabled = true;
            bool cat = run?.Party?.Active != null && run.Party.Active.IsCat;
            bool locked = run != null && run.GetComponent<TargetLockController>() != null && run.GetComponent<TargetLockController>().IsLocked;
            foreach (var button in layout.Buttons)
            {
                Rect rect = button.Bounds;
                bool held = gestures.IsHeld(button.Action) || button.Action == GameInputSemantic.LockOn && locked;
                Color color = held ? new Color(.25f, .77f, 1, .78f) : new Color(.08f, .12f, .19f, .68f);
                SetRect(buttonImages[button.Action].rectTransform, rect);
                buttonImages[button.Action].color = color;
                var label = buttonLabels[button.Action]; label.text = Label(button.Action, cat, locked);
                label.fontSize = Mathf.Max(12, Mathf.RoundToInt(20 * layout.Scale));
            }
            Vector2 origin = gestures.HasMoveFinger ? gestures.MoveOrigin : layout.StickCenter;
            float radius = layout.StickRadius;
            SetRect(stickImage.rectTransform, new Rect(origin.x - radius, origin.y - radius, radius * 2, radius * 2));
            Vector2 knob = gestures.HasMoveFinger ? gestures.MovePosition : origin;
            float diameter = radius * .65f;
            SetRect(knobImage.rectTransform, new Rect(knob.x - diameter / 2, knob.y - diameter / 2, diameter, diameter));
            SetRect(hint.rectTransform, new Rect(layout.SafeArea.xMin + 20 * layout.Scale, layout.SafeArea.yMin + 10 * layout.Scale, 300 * layout.Scale, 27 * layout.Scale));
            hint.fontSize = Mathf.Max(11, Mathf.RoundToInt(16 * layout.Scale));
        }
        public static string Label(GameInputSemantic action, bool cat, bool locked = false)
        {
            switch (action)
            {
                case GameInputSemantic.Jump: return "跳ぶ";
                case GameInputSemantic.Sword: return cat ? "連弾" : "斬る";
                case GameInputSemantic.Dodge: return "回避";
                case GameInputSemantic.Guard: return "防御";
                case GameInputSemantic.Magic: return cat ? "大魔法" : "魔法";
                case GameInputSemantic.Special: return cat ? "時止め" : "必殺";
                case GameInputSemantic.LockOn: return locked ? "解除" : "固定";
                case GameInputSemantic.SwitchCharacter: return "切替";
                default: return action.ToString();
            }
        }
        private static void SetRect(RectTransform transform, Rect rect)
        { transform.anchorMin = transform.anchorMax = transform.pivot = Vector2.zero; transform.anchoredPosition = rect.position; transform.sizeDelta = rect.size; }
        private void ResetTouches()
        {
            gestures.Reset();
            if (input == null) return;
            input.SetTouchMove(Vector2.zero); input.SetTouchCamera(Vector2.zero); input.SetTouchGuardHeld(false); input.ClearQueuedTouchPresses();
        }
        private static void ApplyLandscapeOrientation()
        {
            if (!Application.isMobilePlatform) return;
            Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }
        private void OnApplicationFocus(bool focus) { focusLost = !focus; if (!focus) ResetTouches(); }
        private void OnApplicationPause(bool value) { paused = value; if (value) ResetTouches(); }
        private void OnDisable() { ResetTouches(); if (touchCanvas != null) touchCanvas.enabled = false; }
        private void EnsureVisuals()
        {
            if (touchCanvas != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Noto Sans JP", "Arial" }, 28);
            ownsFont = font != null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Touch Controls", typeof(RectTransform), typeof(Canvas)); root.transform.SetParent(transform, false);
            touchCanvas = root.GetComponent<Canvas>(); touchCanvas.renderMode = RenderMode.ScreenSpaceOverlay; touchCanvas.sortingOrder = 110;
            circleSprite = Sprite.Create(CircleTexture, new Rect(0, 0, 64, 64), Vector2.one * .5f);
            foreach (var button in layout.Buttons)
            {
                var disk = CreateDisk(button.Action.ToString(), new Color(.08f, .12f, .19f, .68f));
                var outline = disk.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.78f, .9f, 1f, .38f); outline.effectDistance = Vector2.one * 2;
                var label = CreateLabel(disk.transform, "Label"); label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                buttonImages.Add(button.Action, disk); buttonLabels.Add(button.Action, label);
            }
            stickImage = CreateDisk("Movement", new Color(.8f, .91f, 1f, .16f)); knobImage = CreateDisk("Thumb", new Color(.8f, .91f, 1f, .36f));
            hint = CreateLabel(root.transform, "Touch hint"); hint.alignment = TextAnchor.MiddleLeft; hint.fontStyle = FontStyle.Normal;
            hint.text = "左：移動　右の空き：カメラ";
        }
        private Image CreateDisk(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(touchCanvas.transform, false);
            var result = go.GetComponent<Image>(); result.sprite = circleSprite; result.color = color; result.raycastTarget = false; return result;
        }
        private Text CreateLabel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            var result = go.GetComponent<Text>(); result.font = font; result.color = Color.white; result.fontStyle = FontStyle.Bold;
            result.alignment = TextAnchor.MiddleCenter; result.raycastTarget = false; return result;
        }
        private Texture2D CircleTexture
        {
            get
            {
                if (circleTexture != null) return circleTexture;
                const int size = 64; circleTexture = new Texture2D(size, size, TextureFormat.ARGB32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
                var center = Vector2.one * (size - 1) * .5f;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    circleTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(((size - 1) * .5f - Vector2.Distance(new Vector2(x, y), center)) / 2.4f)));
                circleTexture.Apply(); return circleTexture;
            }
        }
        private void OnDestroy() { if (touchCanvas != null) Destroy(touchCanvas.gameObject); if (circleSprite != null) Destroy(circleSprite); if (circleTexture != null) Destroy(circleTexture); if (ownsFont && font != null) Destroy(font); }
    }
}
