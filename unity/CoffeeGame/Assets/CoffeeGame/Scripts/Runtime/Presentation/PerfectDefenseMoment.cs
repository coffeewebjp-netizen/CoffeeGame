using CoffeeGame.Combat;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoffeeGame.Presentation
{
    [DefaultExecutionOrder(-250), DisallowMultipleComponent]
    public sealed class PerfectDefenseMoment : MonoBehaviour
    {
        public const float Duration = .65f;
        public const float SlowScale = .18f;
        public static PerfectDefenseMoment Instance { get; private set; }
        private CombatRunController run;
        private Volume volume;
        private VolumeProfile profile;
        private float remaining;
        private float previousScale = 1f;
        private float appliedScale = 1f;
        public bool IsActive => remaining > 0f;
        public float Remaining => remaining;
        public float Desaturation => volume != null ? volume.weight : 0f;

        private void Awake()
        {
            Instance = this;
            EnsureVisual();
        }

        private void EnsureVisual()
        {
            if (volume != null) return;
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 100f; volume.weight = 0f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<ColorAdjustments>(true).saturation.Override(-100f);
            volume.sharedProfile = profile;
        }

        public void Initialize(CombatRunController controller)
        {
            run = controller;
            run.StateChanged += OnRunStateChanged;
        }

        public bool TryBegin()
        {
            if (CombatClock.IsPaused || (run != null && run.Mode != CombatRunMode.Playing)) return false;
            // A nearby second hit cannot keep combat indefinitely in slow motion.
            if (IsActive) return false;
            EnsureVisual();
            previousScale = Time.timeScale;
            remaining = Duration;
            Apply(1f);
            return true;
        }

        private void Update() => Tick(Time.unscaledDeltaTime, run == null || run.Mode == CombatRunMode.Playing);

        public void Tick(float realDelta, bool playing)
        {
            if (!IsActive) return;
            if (!playing || CombatClock.IsPaused) { Cancel(); return; }
            remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, realDelta));
            if (remaining <= 0f) { Cancel(); return; }
            float weight = Mathf.SmoothStep(0f, 1f, remaining / .35f);
            Apply(weight);
        }

        private void Apply(float weight)
        {
            volume.weight = weight;
            appliedScale = previousScale * Mathf.Lerp(1f, SlowScale, weight);
            Time.timeScale = appliedScale;
        }

        public void Cancel()
        {
            // Pause/defeat owns a zero scale. Never unpause it when this effect ends.
            if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = previousScale;
            remaining = 0f;
            if (volume != null) volume.weight = 0f;
        }

        private void OnRunStateChanged() { if (run.Mode != CombatRunMode.Playing) Cancel(); }
        private void OnDisable() => Cancel();
        private void OnDestroy()
        {
            Cancel();
            if (run != null) run.StateChanged -= OnRunStateChanged;
            if (Instance == this) Instance = null;
            if (profile != null) { if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile); }
        }
    }
}
