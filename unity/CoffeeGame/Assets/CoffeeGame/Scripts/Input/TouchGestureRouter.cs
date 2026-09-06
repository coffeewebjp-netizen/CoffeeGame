using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Input
{
    // A finger keeps the role it acquired on touch-down until release/cancel.
    // Moving a camera/stick finger across an action must never fire that action.
    public sealed class TouchGestureRouter
    {
        private enum Role { Ignored, Move, Camera, Button }
        private sealed class Finger { public Role Role; public GameInputSemantic Action; public Vector2 Origin, Previous; public bool Seen; }
        private readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();
        private readonly List<int> released = new List<int>();
        private readonly List<GameInputSemantic> pressed = new List<GameInputSemantic>();
        private int moveId = -1, cameraId = -1;
        public Vector2 Move { get; private set; }
        public Vector2 Camera { get; private set; }
        public Vector2 MoveOrigin { get; private set; }
        public Vector2 MovePosition { get; private set; }
        public bool HasMoveFinger => moveId >= 0;
        public IReadOnlyList<GameInputSemantic> Presses => pressed;
        public bool IsHeld(GameInputSemantic action)
        {
            foreach (var finger in fingers.Values) if (finger.Seen && finger.Role == Role.Button && finger.Action == action) return true;
            return false;
        }
        public void BeginFrame()
        {
            Move = Camera = Vector2.zero; pressed.Clear();
            foreach (var finger in fingers.Values) finger.Seen = false;
        }
        public void Process(int id, Vector2 position, bool began, TouchControlLayout layout, bool blockedByUi = false)
        {
            if (began) Release(id); // A platform may recycle an ID within one update.
            if (!fingers.TryGetValue(id, out var finger))
            {
                // A touch already down when a menu/focus/rotation reset ended
                // must be lifted before it acquires a gameplay role.
                finger = new Finger { Origin = position, Previous = position, Role = Role.Ignored };
                if (began && !blockedByUi)
                {
                    if (layout.TryHit(position, out var action))
                    {
                        finger.Role = Role.Button; finger.Action = action;
                        if (!pressed.Contains(action)) pressed.Add(action);
                    }
                    else if (moveId < 0 && layout.IsMoveZone(position)) { finger.Role = Role.Move; moveId = id; }
                    else if (cameraId < 0 && layout.IsCameraZone(position)) { finger.Role = Role.Camera; cameraId = id; }
                }
                fingers.Add(id, finger);
            }
            finger.Seen = true;
            if (finger.Role == Role.Move)
            {
                Move = TouchOverlayMath.ResolveHoldMove(Vector2.zero, (position - finger.Origin) * (TouchOverlayMath.MoveFullRadius / layout.StickRadius));
                MoveOrigin = finger.Origin; MovePosition = finger.Origin + Vector2.ClampMagnitude(position - finger.Origin, layout.StickRadius);
            }
            else if (finger.Role == Role.Camera)
            {
                var delta = (position - finger.Previous) / layout.Scale;
                Camera = new Vector2(delta.x / 42f, -delta.y / 56f);
            }
            finger.Previous = position;
        }
        public void EndFrame()
        {
            released.Clear(); foreach (var pair in fingers) if (!pair.Value.Seen) released.Add(pair.Key);
            foreach (int id in released) Release(id);
        }
        public void ReleaseMissingTouches(ISet<int> activeIds)
        {
            released.Clear(); foreach (int id in fingers.Keys) if (!activeIds.Contains(id)) released.Add(id);
            foreach (int id in released) Release(id);
        }
        public void Release(int id) { fingers.Remove(id); if (moveId == id) { moveId = -1; Move = Vector2.zero; } if (cameraId == id) { cameraId = -1; Camera = Vector2.zero; } }
        public void Reset()
        {
            fingers.Clear(); pressed.Clear(); released.Clear(); moveId = cameraId = -1;
            Move = Camera = MoveOrigin = MovePosition = Vector2.zero;
        }
    }
}
