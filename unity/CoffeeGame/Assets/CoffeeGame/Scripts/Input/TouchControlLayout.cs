using UnityEngine;

namespace CoffeeGame.Input
{
    // All rectangles use screen coordinates (bottom-left origin). Pixel density
    // does not enlarge the cluster beyond the phone's usable viewport.
    public sealed class TouchControlLayout
    {
        public readonly struct Button
        {
            public readonly GameInputSemantic Action;
            public readonly Rect Bounds;
            public Button(GameInputSemantic action, Rect bounds) { Action = action; Bounds = bounds; }
        }
        public Rect SafeArea { get; }
        public float Scale { get; }
        public float StickRadius => 64f * Scale;
        public Vector2 StickCenter => new Vector2(SafeArea.xMin + 112f * Scale, SafeArea.yMin + 116f * Scale);
        public Button[] Buttons { get; }
        public TouchControlLayout(float width, float height, Rect safeArea)
        {
            var screen = new Rect(0, 0, Mathf.Max(1, width), Mathf.Max(1, height));
            SafeArea = safeArea.width > 0 && safeArea.height > 0
                ? Rect.MinMaxRect(Mathf.Clamp(safeArea.xMin, 0, screen.width), Mathf.Clamp(safeArea.yMin, 0, screen.height),
                    Mathf.Clamp(safeArea.xMax, 0, screen.width), Mathf.Clamp(safeArea.yMax, 0, screen.height)) : screen;
            Scale = Mathf.Max(.1f, Mathf.Min(SafeArea.width / 960f, SafeArea.height / 540f));
            // Attack is the thumb's home position. Travel actions sit below/above
            // it; skills follow the inner arc, utilities sit outside that arc.
            // Hit rectangles are deliberately larger than the visible medallions.
            Rect Target(float fromRight, float fromBottom, float size) => new Rect(
                SafeArea.xMax - (fromRight + size / 2) * Scale,
                SafeArea.yMin + (fromBottom - size / 2) * Scale, size * Scale, size * Scale);
            Buttons = new[] {
                new Button(GameInputSemantic.Sword, Target(94, 102, 108)),
                new Button(GameInputSemantic.Dodge, Target(220, 68, 80)),
                new Button(GameInputSemantic.Jump, Target(66, 232, 76)),
                new Button(GameInputSemantic.Guard, Target(314, 134, 76)),
                new Button(GameInputSemantic.Magic, Target(190, 210, 84)),
                new Button(GameInputSemantic.Special, Target(292, 260, 84)),
                new Button(GameInputSemantic.LockOn, Target(68, 324, 72)),
                new Button(GameInputSemantic.SwitchCharacter, Target(168, 324, 76))
            };
        }
        public bool TryHit(Vector2 position, out GameInputSemantic action)
        {
            foreach (var button in Buttons) if (button.Bounds.Contains(position)) { action = button.Action; return true; }
            action = default; return false;
        }
        public bool IsMoveZone(Vector2 position) => SafeArea.Contains(position) && position.x < SafeArea.center.x;
        public bool IsCameraZone(Vector2 position) => SafeArea.Contains(position) && position.x >= SafeArea.center.x;
    }
}
