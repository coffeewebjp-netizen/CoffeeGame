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
            float size = 76f * Scale, gap = 12f * Scale;
            float right = SafeArea.xMax - 24f * Scale, bottom = SafeArea.yMin + 28f * Scale;
            Rect Cell(int column, int row) => new Rect(right - (3 - column) * size - (2 - column) * gap, bottom + row * (size + gap), size, size);
            Buttons = new[] {
                new Button(GameInputSemantic.Dodge, Cell(0, 0)), new Button(GameInputSemantic.Jump, Cell(1, 0)), new Button(GameInputSemantic.Sword, Cell(2, 0)),
                new Button(GameInputSemantic.Guard, Cell(0, 1)), new Button(GameInputSemantic.Magic, Cell(1, 1)), new Button(GameInputSemantic.Special, Cell(2, 1)),
                new Button(GameInputSemantic.LockOn, new Rect(right - 2 * size - gap, bottom + 2 * (size + gap), size, 48f * Scale)),
                new Button(GameInputSemantic.SwitchCharacter, new Rect(right - size, bottom + 2 * (size + gap), size, 48f * Scale))
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
