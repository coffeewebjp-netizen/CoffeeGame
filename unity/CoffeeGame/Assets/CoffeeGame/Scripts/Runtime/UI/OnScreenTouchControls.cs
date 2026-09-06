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
        private readonly Dictionary<GameInputSemantic, CombatTouchGlyph> icons = new Dictionary<GameInputSemantic, CombatTouchGlyph>();
        private readonly Dictionary<GameInputSemantic, CombatTouchGlyph> rings = new Dictionary<GameInputSemantic, CombatTouchGlyph>();
        private readonly Dictionary<GameInputSemantic, float> feedback = new Dictionary<GameInputSemantic, float>();
        private CombatTouchGlyph specialRing, stickRing;
        private Text specialValue;
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
                bool primary = button.Action == GameInputSemantic.Sword;
                bool special = button.Action == GameInputSemantic.Special;
                float meter = run?.Party?.Active?.Combat != null ? run.Party.Active.Combat.SpecialMeterNormalized : 0;
                bool ready = special && meter >= .999f;
                Color accent = special ? new Color(1f,.79f,.38f) : new Color(.55f,.9f,1f);
                float target = held ? 1 : 0;
                feedback[button.Action] = Mathf.MoveTowards(feedback[button.Action], target, Time.unscaledDeltaTime * 12);
                float press = feedback[button.Action];
                float size = rect.width * (primary ? .93f : .82f) * (1 - press * .06f);
                SetRect(buttonImages[button.Action].rectTransform, new Rect(rect.center-Vector2.one*size/2,Vector2.one*size));
                buttonImages[button.Action].color = Color.Lerp(new Color(.025f,.04f,.055f,primary ? .57f : .4f), new Color(accent.r*.24f,accent.g*.24f,accent.b*.24f,.84f),press);
                icons[button.Action].Icon(button.Action,cat);
                icons[button.Action].color = Color.Lerp(new Color(.95f,.96f,.94f, special && !ready ? .5f : .96f),accent,held || ready ? 1 : 0);
                rings[button.Action].color = held || ready ? accent : new Color(.91f,.94f,.93f,primary ? .72f : .32f);
                var label = buttonLabels[button.Action]; label.text = Label(button.Action, cat, locked);
                label.fontSize = Mathf.Max(10, Mathf.RoundToInt(11 * layout.Scale));
                label.color = held || ready ? accent : new Color(.92f,.94f,.95f,.78f);
                if(special) {
                    specialRing.Ring(meter,.042f); specialRing.color = accent;
                    specialValue.text = ready ? "READY" : Mathf.FloorToInt(meter*100)+"%";
                    specialValue.fontSize = Mathf.Max(10,Mathf.RoundToInt(11*layout.Scale));
                }
            }
            Vector2 origin = gestures.HasMoveFinger ? gestures.MoveOrigin : layout.StickCenter;
            float radius = layout.StickRadius;
            SetRect(stickImage.rectTransform, new Rect(origin.x - radius, origin.y - radius, radius * 2, radius * 2));
            stickRing.color = new Color(.92f,.96f,1f,gestures.HasMoveFinger ? .5f : .23f);
            Vector2 knob = gestures.HasMoveFinger ? gestures.MovePosition : origin;
            float diameter = radius * .65f;
            SetRect(knobImage.rectTransform, new Rect(knob.x - diameter / 2, knob.y - diameter / 2, diameter, diameter));
            SetRect(hint.rectTransform, new Rect(layout.SafeArea.xMin + 20 * layout.Scale, layout.SafeArea.yMin + 10 * layout.Scale, 300 * layout.Scale, 27 * layout.Scale));
            hint.fontSize = Mathf.Max(10, Mathf.RoundToInt(11 * layout.Scale));
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
                var rim = CreateGlyph(disk.transform,"Rim",0,0,1,1); rim.Ring(1,button.Action == GameInputSemantic.Sword ? .022f : .017f);
                var icon = CreateGlyph(disk.transform,"Icon",.14f,.17f,.86f,.89f); icon.Icon(button.Action,false);
                rings.Add(button.Action,rim); icons.Add(button.Action,icon); feedback.Add(button.Action,0);
                var label = CreateLabel(disk.transform, "Label"); label.rectTransform.anchorMin = new Vector2(-.2f,-.22f); label.rectTransform.anchorMax = new Vector2(1.2f,-.01f);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                buttonImages.Add(button.Action, disk); buttonLabels.Add(button.Action, label);
                if(button.Action == GameInputSemantic.Special) {
                    specialRing = CreateGlyph(disk.transform,"Energy",-.06f,-.06f,1.06f,1.06f);
                    specialValue = CreateLabel(disk.transform,"Energy amount");
                    specialValue.rectTransform.anchorMin = new Vector2(-.2f,1.05f); specialValue.rectTransform.anchorMax = new Vector2(1.2f,1.3f);
                    specialValue.rectTransform.offsetMin=specialValue.rectTransform.offsetMax=Vector2.zero;
                    specialValue.color=new Color(1,.79f,.38f);
                }
            }
            stickImage = CreateDisk("Movement", new Color(.025f,.04f,.055f,.12f));
            stickRing = CreateGlyph(stickImage.transform,"Movement rim",0,0,1,1); stickRing.Ring(1,.013f);
            knobImage = CreateDisk("Thumb", new Color(.85f,.91f,.94f,.2f));
            var knobRing = CreateGlyph(knobImage.transform,"Thumb rim",0,0,1,1); knobRing.Ring(1,.025f); knobRing.color=new Color(1,1,1,.38f);
            hint = CreateLabel(root.transform, "Touch hint"); hint.alignment = TextAnchor.MiddleLeft; hint.fontStyle = FontStyle.Normal;
            hint.text = "MOVE / 左スワイプ"; hint.color=new Color(.85f,.9f,.93f,.42f);
        }
        private static CombatTouchGlyph CreateGlyph(Transform parent,string name,float x,float y,float xx,float yy)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CombatTouchGlyph));go.transform.SetParent(parent,false);
            var glyph=go.GetComponent<CombatTouchGlyph>();glyph.raycastTarget=false;
            glyph.rectTransform.anchorMin=new Vector2(x,y);glyph.rectTransform.anchorMax=new Vector2(xx,yy);
            glyph.rectTransform.offsetMin=glyph.rectTransform.offsetMax=Vector2.zero;return glyph;
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
