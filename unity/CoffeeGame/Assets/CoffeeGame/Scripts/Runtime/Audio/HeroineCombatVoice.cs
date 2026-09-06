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
        }

        public void Sword()
        {
            if (source.isPlaying) return;
            Play(sword[swordIndex++ % sword.Length]);
        }
        public void Magic() => Play(magic);
        public void Dodge() => Play(dodge);
        public void Stop() { if (source != null) source.Stop(); }
        private void Play(AudioClip clip)
        {
            if (clip == null) return;
            // One voice channel: a new major action replaces the previous line.
            source.Stop();
            source.clip = clip;
            source.Play();
        }
    }
}
