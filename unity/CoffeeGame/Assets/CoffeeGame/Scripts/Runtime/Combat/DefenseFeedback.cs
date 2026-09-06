using System.Collections.Generic;
using CoffeeGame.Audio;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public enum DefenseFeedbackEvent
    {
        BladeBlock,
        BarrierBlock,
        Parry,
        PerfectDodge,
        Counter,
        PlungeImpact
    }

    /// <summary>
    /// Presentation-only endpoint for confirmed defense and counter outcomes.
    /// It does not query targets or apply gameplay effects.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefenseFeedback : MonoBehaviour
    {
        private readonly List<GameObject> activeEffects = new List<GameObject>(8);
        private AudioDirector audioDirector;
        private GameObject clockOwner;

        public void Initialize(AudioDirector director, GameObject combatClockOwner = null)
        {
            audioDirector = director;
            clockOwner = combatClockOwner != null ? combatClockOwner : gameObject;
        }

        public GameObject Emit(
            DefenseFeedbackEvent feedbackEvent,
            Vector3 worldPosition,
            Vector3 facing,
            float radius = 1f,
            GameObject effectClockOwner = null)
        {
            PruneFinishedEffects();
            GameObject owner = effectClockOwner != null ? effectClockOwner :
                clockOwner != null ? clockOwner : gameObject;
            audioDirector?.Play(SoundFor(feedbackEvent), VolumeFor(feedbackEvent), owner);

            var effect = new GameObject(NameFor(feedbackEvent));
            effect.transform.position = worldPosition;
            CombatOwnership.Assign(effect, owner);
            effect.AddComponent<DefenseTransientEffect>().Initialize(
                feedbackEvent,
                facing,
                Mathf.Clamp(radius, 0.25f, 3.5f));
            activeEffects.Add(effect);
            return effect;
        }

        public void CancelAll()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i] != null)
                {
                    DestroyOwned(activeEffects[i]);
                }
            }
            activeEffects.Clear();
        }

        private void PruneFinishedEffects()
        {
            for (int i = activeEffects.Count - 1; i >= 0; i--)
            {
                if (activeEffects[i] == null)
                {
                    activeEffects.RemoveAt(i);
                }
            }
        }

        private static CombatSound SoundFor(DefenseFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case DefenseFeedbackEvent.BladeBlock: return CombatSound.BladeBlock;
                case DefenseFeedbackEvent.BarrierBlock: return CombatSound.BarrierBlock;
                case DefenseFeedbackEvent.Parry: return CombatSound.Parry;
                case DefenseFeedbackEvent.PerfectDodge: return CombatSound.PerfectDodge;
                case DefenseFeedbackEvent.Counter: return CombatSound.Counter;
                default: return CombatSound.PlungeImpact;
            }
        }

        private static float VolumeFor(DefenseFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case DefenseFeedbackEvent.BladeBlock: return 0.78f;
                case DefenseFeedbackEvent.BarrierBlock: return 0.68f;
                case DefenseFeedbackEvent.Parry: return 0.9f;
                case DefenseFeedbackEvent.PerfectDodge: return 0.78f;
                case DefenseFeedbackEvent.Counter: return 0.86f;
                default: return 0.92f;
            }
        }

        private static string NameFor(DefenseFeedbackEvent feedbackEvent)
        {
            switch (feedbackEvent)
            {
                case DefenseFeedbackEvent.BladeBlock: return "Blade block sparks VFX";
                case DefenseFeedbackEvent.BarrierBlock: return "Magic barrier block VFX";
                case DefenseFeedbackEvent.Parry: return "Parry gold stars VFX";
                case DefenseFeedbackEvent.PerfectDodge: return "Perfect dodge full pulse VFX";
                case DefenseFeedbackEvent.Counter: return "Counter strike VFX";
                default: return "Plunge shockwave debris VFX";
            }
        }

        private void OnDestroy()
        {
            CancelAll();
        }

        private static void DestroyOwned(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private sealed class DefenseTransientEffect : MonoBehaviour
        {
            private readonly LineRenderer[] lines = new LineRenderer[24];
            private readonly Transform[] debris = new Transform[8];
            private readonly Vector3[] debrisStart = new Vector3[8];
            private readonly Vector3[] debrisVelocity = new Vector3[8];
            private DefenseFeedbackEvent feedbackEvent;
            private Material material;
            private float duration;
            private float elapsed;
            private int lineCount;
            private int debrisCount;

            public void Initialize(DefenseFeedbackEvent value, Vector3 facing, float radius)
            {
                feedbackEvent = value;
                Vector3 forward = Vector3.ProjectOnPlane(facing, Vector3.up);
                if (forward.sqrMagnitude < 0.001f)
                {
                    forward = Vector3.forward;
                }
                forward.Normalize();

                switch (value)
                {
                    case DefenseFeedbackEvent.BladeBlock:
                        duration = 0.22f;
                        material = CombatGlowVisuals.CreateMaterial("Blade block spark material",
                            new Color(1f, 0.78f, 0.3f, 0.92f));
                        BuildBladeBlock(forward, radius);
                        break;
                    case DefenseFeedbackEvent.BarrierBlock:
                        duration = 0.34f;
                        material = CombatGlowVisuals.CreateMaterial("Barrier block material",
                            new Color(0.2f, 0.92f, 1f, 0.88f));
                        BuildBarrierBlock(forward, radius);
                        break;
                    case DefenseFeedbackEvent.Parry:
                        duration = 0.48f;
                        material = CombatGlowVisuals.CreateMaterial("Parry gold material",
                            new Color(1f, 0.76f, 0.18f, 0.96f));
                        BuildParry(radius);
                        break;
                    case DefenseFeedbackEvent.PerfectDodge:
                        duration = 0.54f;
                        material = CombatGlowVisuals.CreateMaterial("Perfect dodge cyan material",
                            new Color(0.16f, 0.94f, 1f, 0.92f));
                        BuildPerfectDodge(radius);
                        break;
                    case DefenseFeedbackEvent.Counter:
                        duration = 0.26f;
                        material = CombatGlowVisuals.CreateMaterial("Counter strike material",
                            new Color(1f, 0.98f, 0.82f, 1f));
                        BuildCounter(forward, radius);
                        break;
                    default:
                        duration = 0.66f;
                        material = CombatGlowVisuals.CreateMaterial("Plunge shockwave material",
                            new Color(1f, 0.78f, 0.32f, 0.88f));
                        BuildPlungeImpact(radius);
                        break;
                }
            }

            private void BuildBladeBlock(Vector3 forward, float radius)
            {
                Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                for (int i = 0; i < 7; i++)
                {
                    float angle = -1.25f + i * 0.42f;
                    Vector3 direction = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle) - forward * 0.12f).normalized;
                    float length = radius * (0.2f + (i % 3) * 0.055f);
                    AddSegment(-direction * 0.025f, direction * length, 0.024f);
                }
                AddSegment(-right * radius * 0.24f - up * radius * 0.18f,
                    right * radius * 0.24f + up * radius * 0.18f, 0.045f);
            }

            private void BuildBarrierBlock(Vector3 forward, float radius)
            {
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 up = Vector3.up;
                AddRing(right, up, radius * 0.5f, 0.034f, 33, Vector3.up * 0.72f);
                AddRing(right, up, radius * 0.68f, 0.018f, 33, Vector3.up * 0.72f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = i * Mathf.PI / 3f;
                    Vector3 inner = right * Mathf.Cos(angle) * radius * 0.32f + up * Mathf.Sin(angle) * radius * 0.32f + up * 0.72f;
                    Vector3 outer = right * Mathf.Cos(angle) * radius * 0.68f + up * Mathf.Sin(angle) * radius * 0.68f + up * 0.72f;
                    AddSegment(inner, outer, 0.018f);
                }
            }

            private void BuildParry(float radius)
            {
                Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                Vector3 center = up * 0.92f;
                AddSegment(center - right * radius * 0.56f, center + right * radius * 0.56f, 0.06f);
                AddSegment(center - up * radius * 0.56f, center + up * radius * 0.56f, 0.06f);
                AddSegment(center - (right + up).normalized * radius * 0.4f,
                    center + (right + up).normalized * radius * 0.4f, 0.03f);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2f / 5f + 0.3f;
                    Vector3 starCenter = center + right * Mathf.Cos(angle) * radius * 0.76f +
                        up * Mathf.Sin(angle) * radius * 0.5f;
                    AddStar(starCenter, right, up, radius * (0.11f + (i % 2) * 0.025f));
                }
            }

            private void BuildPerfectDodge(float radius)
            {
                AddRing(Vector3.right, Vector3.forward, radius * 0.58f, 0.045f, 41, Vector3.up * 0.08f);
                AddRing(Vector3.right, Vector3.forward, radius * 0.88f, 0.022f, 41, Vector3.up * 0.1f);
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI * 0.25f;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    AddSegment(direction * radius * 0.48f + Vector3.up * 0.12f,
                        direction * radius * 0.94f + Vector3.up * 0.12f, 0.018f);
                }
                AddSegment(Vector3.up * 0.14f, Vector3.up * (radius * 1.08f), 0.026f);
            }

            private void BuildCounter(Vector3 forward, float radius)
            {
                Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.Cross(Vector3.up, forward).normalized;
                Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                Vector3 center = forward * radius * 0.12f + up * 0.66f;
                Vector3 diagonal = (right + up * 0.62f).normalized;
                AddSegment(center - diagonal * radius * 0.55f, center + diagonal * radius * 0.55f, 0.07f);
                AddSegment(center - diagonal * radius * 0.34f - up * 0.055f,
                    center + diagonal * radius * 0.72f - up * 0.055f, 0.025f);
                AddSegment(center - right * radius * 0.24f, center + right * radius * 0.24f, 0.036f);
            }

            private void BuildPlungeImpact(float radius)
            {
                AddRing(Vector3.right, Vector3.forward, radius * 0.44f, 0.055f, 49, Vector3.up * 0.035f);
                AddRing(Vector3.right, Vector3.forward, radius * 0.72f, 0.035f, 49, Vector3.up * 0.045f);
                AddRing(Vector3.right, Vector3.forward, radius, 0.022f, 49, Vector3.up * 0.055f);
                for (int i = 0; i < debris.Length; i++)
                {
                    float angle = i * 2.399963f;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    shard.name = $"Ground debris {i + 1}";
                    shard.transform.SetParent(transform, false);
                    Collider collider = shard.GetComponent<Collider>();
                    if (collider != null)
                    {
                        collider.enabled = false;
                        if (Application.isPlaying) Destroy(collider);
                        else DestroyImmediate(collider);
                    }
                    MeshRenderer renderer = shard.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = material;
                    }
                    float distance = radius * (0.18f + (i % 3) * 0.08f);
                    debrisStart[i] = direction * distance + Vector3.up * 0.035f;
                    debrisVelocity[i] = direction * radius * (0.72f + (i % 2) * 0.18f) +
                        Vector3.up * (0.35f + (i % 3) * 0.08f);
                    shard.transform.localPosition = debrisStart[i];
                    shard.transform.localRotation = Quaternion.Euler(i * 19f, i * 31f, i * 13f);
                    shard.transform.localScale = new Vector3(0.06f, 0.035f, 0.11f) * radius;
                    debris[debrisCount++] = shard.transform;
                }
            }

            private void AddRing(
                Vector3 axisX,
                Vector3 axisY,
                float radius,
                float width,
                int points,
                Vector3 center)
            {
                if (lineCount >= lines.Length)
                {
                    return;
                }
                LineRenderer line = CombatGlowVisuals.Line(transform, "Pulse ring", material, points, width, true);
                for (int i = 0; i < points; i++)
                {
                    float angle = i / (float)(points - 1) * Mathf.PI * 2f;
                    line.SetPosition(i, center + axisX * Mathf.Cos(angle) * radius + axisY * Mathf.Sin(angle) * radius);
                }
                lines[lineCount++] = line;
            }

            private void AddSegment(Vector3 from, Vector3 to, float width)
            {
                if (lineCount >= lines.Length)
                {
                    return;
                }
                LineRenderer line = CombatGlowVisuals.Line(transform, "Flash streak", material, 2, width);
                line.widthCurve = AnimationCurve.Linear(0f, 0.35f, 1f, 1f);
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                lines[lineCount++] = line;
            }

            private void AddStar(Vector3 center, Vector3 right, Vector3 up, float radius)
            {
                if (lineCount >= lines.Length)
                {
                    return;
                }
                LineRenderer line = CombatGlowVisuals.Line(transform, "Parry star", material, 11, 0.025f, true);
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = -Mathf.PI * 0.5f + i * Mathf.PI * 4f / 10f;
                    line.SetPosition(i, center + right * Mathf.Cos(angle) * radius + up * Mathf.Sin(angle) * radius);
                }
                lines[lineCount++] = line;
            }

            private void Update()
            {
                float deltaTime = CombatClock.DeltaTime(gameObject);
                elapsed += deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float fade = 1f - Mathf.Clamp01((t - 0.45f) / 0.55f);
                float scale;
                switch (feedbackEvent)
                {
                    case DefenseFeedbackEvent.PerfectDodge:
                        scale = Mathf.Lerp(0.25f, 1.5f, Mathf.Sqrt(t));
                        break;
                    case DefenseFeedbackEvent.PlungeImpact:
                        scale = Mathf.Lerp(0.45f, 1.35f, Mathf.Sqrt(t));
                        break;
                    default:
                        scale = Mathf.Lerp(0.55f, 1.08f, Mathf.Min(1f, t * 4f));
                        break;
                }
                transform.localScale = Vector3.one * scale;

                for (int i = 0; i < lineCount; i++)
                {
                    CombatGlowVisuals.Alpha(lines[i], fade * fade);
                }
                for (int i = 0; i < debrisCount; i++)
                {
                    if (debris[i] == null)
                    {
                        continue;
                    }
                    debris[i].localPosition = debrisStart[i] + debrisVelocity[i] * elapsed +
                        Vector3.down * (1.1f * elapsed * elapsed);
                    debris[i].Rotate(160f * deltaTime, 90f * deltaTime, 120f * deltaTime, Space.Self);
                    debris[i].localScale *= Mathf.Pow(0.08f, deltaTime / duration);
                }
                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                if (material != null)
                {
                    if (Application.isPlaying) Destroy(material);
                    else DestroyImmediate(material);
                    material = null;
                }
            }
        }
    }
}
