using CoffeeGame.Combat;
using UnityEngine;

namespace CoffeeGame.Audio
{
    // Optional Owner-supplied lines. A missing file stays silent.
    [DisallowMultipleComponent]
    public sealed class SpecialCombatVoice : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip clip;
        private bool paused;
        public string ResourcePath { get; private set; }
        public bool HasClip => clip != null;
        public bool IsSpeaking => source != null && (source.isPlaying || paused);
        public int PlaybackCount { get; private set; }

        public void Initialize(bool cat)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = .85f;
            ResourcePath = cat ? "Audio/Voices/SilverCat/special_01" : "Audio/Voices/Heroine/special_01";
            clip = Resources.Load<AudioClip>(ResourcePath);
        }

        public void Play()
        {
            if (clip == null || source == null) return;
            GetComponent<HeroineCombatVoice>()?.Stop();
            source.Stop(); source.clip = clip; source.Play(); paused = false;
            PlaybackCount++;
        }

        public void Stop()
        {
            if (source != null) source.Stop();
            paused = false;
        }

        private void Update()
        {
            if (source == null) return;
            var stop = TimeStopController.Instance;
            bool freeze = CombatClock.IsPaused || (stop != null && stop.IsActive && stop.IsFrozen(gameObject));
            if (freeze && source.isPlaying) { source.Pause(); paused = true; }
            else if (!freeze && paused) { source.UnPause(); paused = false; }
        }
        private void OnDisable() => Stop();
    }
}
