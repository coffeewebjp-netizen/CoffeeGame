using CoffeeGame.Actors;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.Combat
{
    [DefaultExecutionOrder(-175), DisallowMultipleComponent]
    public sealed class TargetLockController : MonoBehaviour
    {
        public const float AcquireRange = 18f;
        public const float ReleaseRange = 24f;
        private PartyRuntime party;
        private CombatRunController run;
        private GameInputReader input;
        private Camera worldCamera;
        private PlayerMotor3D owner;
        public Health Target { get; private set; }
        public bool IsLocked => Target != null;
        public bool AutomaticInput { get; set; } = true;

        public void Initialize(PartyRuntime members, CombatRunController controller, GameInputReader reader, Camera camera)
        {
            party = members; run = controller; input = reader; worldCamera = camera;
            run.StateChanged += OnRunStateChanged;
        }

        private void Update()
        {
            if (!AutomaticInput) return;
            var actor = party != null ? party.Active : null;
            bool playing = run != null && run.Mode == CombatRunMode.Playing && input != null && input.Context == GameInputContext.Battle;
            Tick(actor != null && actor.Targetable ? actor.Motor : null, playing, playing && input.LockOnPressed);
        }

        // One edge from the logical input; a held stick never repeats the toggle.
        public void Tick(PlayerMotor3D activeMotor, bool playing, bool togglePressed)
        {
            if (owner != activeMotor) { Clear(); owner = activeMotor; }
            if (!playing || owner == null || !owner.CanMove) { Clear(); return; }
            if (Target != null && !IsValidTarget(Target, owner.transform.position, ReleaseRange)) Clear();
            if (!togglePressed) return;
            if (Target != null) { Clear(); return; }
            Health nearest = PartyTargeting.NearestEnemy(owner.transform.position);
            if (IsValidTarget(nearest, owner.transform.position, AcquireRange))
            {
                Target = nearest;
                owner.LockedTarget = Target;
            }
        }

        public static bool IsValidTarget(Health target, Vector3 from, float range) =>
            target != null && target.isActiveAndEnabled && target.IsAlive && target.Team == DamageTeam.Enemy &&
            (target.transform.position - from).sqrMagnitude <= range * range;

        public void Clear()
        {
            if (owner != null) owner.LockedTarget = null;
            Target = null;
        }

        private void OnRunStateChanged() { if (run.Mode != CombatRunMode.Playing) Clear(); }
        private void OnDisable() => Clear();
        private void OnDestroy() { Clear(); if (run != null) run.StateChanged -= OnRunStateChanged; }

        private void OnGUI()
        {
            if (Target == null || worldCamera == null || !Target.IsAlive) return;
            Vector3 point = worldCamera.WorldToScreenPoint(Target.transform.position + Vector3.up * .8f);
            if (point.z <= 0f) return;
            bool inverted = TimeStopController.Instance != null && TimeStopController.Instance.IsActive;
            float x = point.x, y = inverted ? point.y : Screen.height - point.y;
            float size = Mathf.Clamp(Screen.height * .035f, 18f, 38f);
            Color previous = GUI.color;
            GUI.color = new Color(1f, .87f, .34f, .95f);
            for (int side = -1; side <= 1; side += 2)
            for (int vertical = -1; vertical <= 1; vertical += 2)
            {
                GUI.DrawTexture(new Rect(x + side * size - (side > 0 ? 10f : 0f), y + vertical * size, 10f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x + side * size, y + vertical * size - (vertical > 0 ? 10f : 0f), 2f, 10f), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
