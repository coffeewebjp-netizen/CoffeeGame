using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Audio
{
    public enum CombatSound
    {
        SwordSwing,
        SwordHit,
        SpinCharge,
        SpinRelease,
        MagicCharge,
        IceRelease,
        Impact,
        Reward,
        LevelUp,
        Victory
    }

    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        private readonly Dictionary<CombatSound, float> pitchBySound = new Dictionary<CombatSound, float>
        {
            { CombatSound.SwordSwing, 1.12f },
            { CombatSound.SwordHit, 0.92f },
            { CombatSound.SpinCharge, 0.74f },
            { CombatSound.SpinRelease, 0.82f },
            { CombatSound.MagicCharge, 0.72f },
            { CombatSound.IceRelease, 1.08f },
            { CombatSound.Impact, 0.68f },
            { CombatSound.Reward, 1.3f },
            { CombatSound.LevelUp, 1.48f },
            { CombatSound.Victory, 1.15f }
        };

        private AudioSource musicSource;
        private AudioSource effectSource;
        private AudioClip swordClip;
        private AudioClip magicClip;

        public void Initialize()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            effectSource = gameObject.AddComponent<AudioSource>();

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.32f;
            effectSource.playOnAwake = false;
            effectSource.volume = 0.72f;

            musicSource.clip = Resources.Load<AudioClip>("Audio/Rituals_of_the_Jade_Valley");
            swordClip = Resources.Load<AudioClip>("Audio/katana-slash1");
            magicClip = Resources.Load<AudioClip>("Audio/magic-wind2");
        }

        public void StartMusic()
        {
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        public void Play(CombatSound sound, float volume = 1f)
        {
            if (effectSource == null)
            {
                return;
            }

            AudioClip clip = IsMagicSound(sound) ? magicClip : swordClip;
            if (clip == null)
            {
                return;
            }

            effectSource.pitch = pitchBySound.TryGetValue(sound, out float pitch) ? pitch : 1f;
            effectSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void SetMusicVolume(float value)
        {
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(value);
            }
        }

        public void SetEffectsVolume(float value)
        {
            if (effectSource != null)
            {
                effectSource.volume = Mathf.Clamp01(value);
            }
        }

        private static bool IsMagicSound(CombatSound sound)
        {
            return sound == CombatSound.MagicCharge || sound == CombatSound.IceRelease || sound == CombatSound.LevelUp;
        }
    }
}
