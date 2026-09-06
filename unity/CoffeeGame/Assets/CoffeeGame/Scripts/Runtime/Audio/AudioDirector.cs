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
        Victory,
        Jump,
        Land,
        BladeBlock,
        BarrierBlock,
        Parry,
        PerfectDodge,
        Counter,
        PlungeImpact
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
            { CombatSound.Victory, 1.15f },
            { CombatSound.BladeBlock, 1f },
            { CombatSound.BarrierBlock, 1f },
            { CombatSound.Parry, 1f },
            { CombatSound.PerfectDodge, 1f },
            { CombatSound.Counter, 1f },
            { CombatSound.PlungeImpact, 1f }
        };

        private AudioSource musicSource;
        private AudioSource effectSource;
        private AudioClip swordClip;
        private AudioClip magicClip;
        private AudioClip swordSwingClip;
        private AudioClip jumpClip;
        private AudioClip landClip;
        private readonly Dictionary<GameObject, AudioSource> actorSources = new Dictionary<GameObject, AudioSource>();
        private readonly Dictionary<CombatSound, AudioClip> proceduralClips = new Dictionary<CombatSound, AudioClip>();
        private float effectsVolume = 0.72f;

        public void Initialize()
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            effectSource = gameObject.AddComponent<AudioSource>();

            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = 0.24f;
            effectSource.playOnAwake = false;
            effectSource.volume = 0.72f;

            musicSource.clip = Resources.Load<AudioClip>("Audio/Music/ForestBattle20260906");
            swordClip = Resources.Load<AudioClip>("Audio/katana-slash1");
            magicClip = Resources.Load<AudioClip>("Audio/magic-wind2");
            swordSwingClip = Resources.Load<AudioClip>("Audio/Actions/sword_01_quick");
            jumpClip = Resources.Load<AudioClip>("Audio/Actions/jump_01_light");
            landClip = Resources.Load<AudioClip>("Audio/Actions/land_01_soft");
            CreateDefenseClips();
        }

        public void StartMusic()
        {
            if (musicSource != null && musicSource.clip != null && !musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }

        public void Play(CombatSound sound, float volume = 1f, GameObject owner = null)
        {
            if (effectSource == null)
            {
                return;
            }

            AudioClip clip = proceduralClips.TryGetValue(sound, out AudioClip procedural) ? procedural
                : sound == CombatSound.SwordSwing ? swordSwingClip
                : sound == CombatSound.Jump ? jumpClip
                : sound == CombatSound.Land ? landClip
                : IsMagicSound(sound) ? magicClip : swordClip;
            if (clip == null)
            {
                return;
            }

            AudioSource source = effectSource;
            if (owner != null)
            {
                if (!actorSources.TryGetValue(owner, out source) || source == null)
                {
                    var host = new GameObject("Actor combat effects");
                    host.transform.SetParent(owner.transform, false);
                    source = host.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.volume = effectsVolume;
                    actorSources[owner] = source;
                }
            }
            source.pitch = pitchBySound.TryGetValue(sound, out float pitch) ? pitch : 1f;
            source.PlayOneShot(clip, Mathf.Clamp01(volume));
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
            effectsVolume = Mathf.Clamp01(value);
            foreach (var source in actorSources.Values) if (source != null) source.volume = effectsVolume;
            if (effectSource != null)
            {
                effectSource.volume = Mathf.Clamp01(value);
            }
        }

        private static bool IsMagicSound(CombatSound sound)
        {
            return sound == CombatSound.MagicCharge || sound == CombatSound.IceRelease || sound == CombatSound.LevelUp;
        }

        private void CreateDefenseClips()
        {
            if (proceduralClips.Count > 0)
            {
                return;
            }

            proceduralClips[CombatSound.BladeBlock] = CreateProceduralClip(CombatSound.BladeBlock, 0.24f);
            proceduralClips[CombatSound.BarrierBlock] = CreateProceduralClip(CombatSound.BarrierBlock, 0.34f);
            proceduralClips[CombatSound.Parry] = CreateProceduralClip(CombatSound.Parry, 0.42f);
            proceduralClips[CombatSound.PerfectDodge] = CreateProceduralClip(CombatSound.PerfectDodge, 0.5f);
            proceduralClips[CombatSound.Counter] = CreateProceduralClip(CombatSound.Counter, 0.22f);
            proceduralClips[CombatSound.PlungeImpact] = CreateProceduralClip(CombatSound.PlungeImpact, 0.56f);
        }

        private static AudioClip CreateProceduralClip(CombatSound sound, float duration)
        {
            const int sampleRate = 24000;
            int sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];
            uint noiseState = 0x9e3779b9u ^ ((uint)sound * 747796405u);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                float normalized = i / (float)Mathf.Max(1, sampleCount - 1);
                noiseState ^= noiseState << 13;
                noiseState ^= noiseState >> 17;
                noiseState ^= noiseState << 5;
                float noise = ((noiseState & 0xffffu) / 32767.5f) - 1f;
                float sample = Synthesize(sound, time, normalized, noise);
                float attack = Mathf.Clamp01(time / 0.008f);
                float release = Mathf.Pow(1f - normalized, sound == CombatSound.PlungeImpact ? 1.6f : 2.2f);
                samples[i] = Mathf.Clamp(sample * attack * release * 0.72f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create($"Procedural {sound}", sampleCount, 1, sampleRate, false);
            clip.hideFlags = HideFlags.DontSave;
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Synthesize(CombatSound sound, float time, float normalized, float noise)
        {
            switch (sound)
            {
                case CombatSound.BladeBlock:
                {
                    float strike = Mathf.Exp(-time * 35f);
                    return Mathf.Sin(Mathf.PI * 2f * 1850f * time) * 0.62f * strike +
                           Mathf.Sin(Mathf.PI * 2f * 2730f * time) * 0.3f * Mathf.Exp(-time * 18f) +
                           noise * 0.24f * strike;
                }
                case CombatSound.BarrierBlock:
                {
                    float frequency = Mathf.Lerp(410f, 730f, normalized);
                    float shimmer = Mathf.Sin(Mathf.PI * 2f * 6f * time) * 0.18f + 0.82f;
                    return Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.52f * shimmer +
                           Mathf.Sin(Mathf.PI * 2f * frequency * 2.01f * time) * 0.24f;
                }
                case CombatSound.Parry:
                {
                    float frequency = Mathf.Lerp(980f, 2240f, Mathf.Sqrt(normalized));
                    float bell = Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.55f +
                                 Mathf.Sin(Mathf.PI * 2f * frequency * 1.51f * time) * 0.24f;
                    return bell + noise * 0.12f * Mathf.Exp(-time * 30f);
                }
                case CombatSound.PerfectDodge:
                {
                    float frequency = Mathf.Lerp(360f, 1560f, normalized);
                    float pulse = Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.46f +
                                  Mathf.Sin(Mathf.PI * 2f * frequency * 0.5f * time) * 0.2f;
                    return pulse + noise * 0.08f * Mathf.Sin(normalized * Mathf.PI);
                }
                case CombatSound.Counter:
                {
                    float snap = Mathf.Exp(-time * 42f);
                    return noise * 0.42f * snap +
                           Mathf.Sin(Mathf.PI * 2f * 138f * time) * 0.45f * Mathf.Exp(-time * 16f) +
                           Mathf.Sin(Mathf.PI * 2f * 2420f * time) * 0.28f * snap;
                }
                default:
                {
                    float lowFrequency = Mathf.Lerp(76f, 43f, normalized);
                    float boom = Mathf.Sin(Mathf.PI * 2f * lowFrequency * time) * 0.7f;
                    float ground = noise * 0.34f * Mathf.Exp(-time * 12f);
                    return boom + ground + Mathf.Sin(Mathf.PI * 2f * 310f * time) * 0.16f * Mathf.Exp(-time * 24f);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (AudioClip clip in proceduralClips.Values)
            {
                if (clip != null)
                {
                    if (Application.isPlaying) Destroy(clip);
                    else DestroyImmediate(clip);
                }
            }
            proceduralClips.Clear();
            actorSources.Clear();
        }
    }
}
