using CoffeeGame.Actors;
using CoffeeGame.Combat;
using UnityEngine;

namespace CoffeeGame.Audio
{
    [DisallowMultipleComponent]
    public sealed class HeroineCombatVoice : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip magic, dodge;
        private AudioClip[] sword;
        private int swordIndex;
        private AudioClip[] hurt;
        private int hurtIndex;
        private float nextHurtAt;
        private Health health;
        private bool paused;
        public string LastPlayedClip { get; private set; }

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.85f;
            const string root = "Audio/Voices/Heroine/";
            magic = Resources.Load<AudioClip>(root + "magic_02_freeze");
            dodge = Resources.Load<AudioClip>(root + "dodge_02_over_here");
            sword = new[] { Resources.Load<AudioClip>(root + "sword_01_ya"), Resources.Load<AudioClip>(root + "sword_02_ha") };
            hurt = new[] { Resources.Load<AudioClip>(root + "hurt_01_u"), Resources.Load<AudioClip>(root + "hurt_02_ita"), Resources.Load<AudioClip>(root + "hurt_03_chi") };
            health = GetComponent<Health>();
            if (health != null) health.Damaged += OnDamaged;
        }

        public void Sword()
        {
            if (source.isPlaying || GetComponent<SpecialCombatVoice>()?.IsSpeaking == true) return;
            Play(sword[swordIndex++ % sword.Length]);
        }
        public void Magic() => Play(magic);
        public void Dodge() => Play(dodge);
        public void Stop() { if (source != null) source.Stop(); paused = false; }
        private void OnDamaged(Health actor, DamageInfo damage)
        {
            if (damage.Amount <= 0 || damage.IsGuarded || Time.unscaledTime < nextHurtAt ||
                GetComponent<SpecialCombatVoice>()?.IsSpeaking == true) return;
            nextHurtAt = Time.unscaledTime + .85f;
            Play(hurt[hurtIndex++ % hurt.Length]);
        }
        private void Update()
        {
            if (source == null) return;
            bool freeze = CombatClock.IsPaused || (TimeStopController.Instance != null && TimeStopController.Instance.IsFrozen(gameObject));
            if (freeze && source.isPlaying) { source.Pause(); paused = true; }
            else if (!freeze && paused) { source.UnPause(); paused = false; }
        }
        private void OnDisable() => Stop();
        private void OnDestroy() { if (health != null) health.Damaged -= OnDamaged; }
        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            // One voice channel: a new major action replaces the previous line.
            source.Stop();
            source.clip = clip;
            LastPlayedClip = clip.name;
            paused = false;
            source.Play();
        }
    }
}
