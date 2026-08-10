using System.Collections;
using UnityEngine;
using CoffeeGame.Presentation;

namespace CoffeeGame.Combat
{
    public static class CombatVfxFactory
    {
        public static GameObject SpawnIaiCinematic(Vector3 center, Vector3 facing, float radius)
        {
            var effect = new GameObject("Iaijutsu cinematic VFX");
            effect.AddComponent<IaiCinematicEffect>().Initialize(center, facing, radius);
            return effect;
        }

        public static GameObject SpawnMagicCharge(Transform anchor, float lifetime)
        {
            var effect = new GameObject("Magic charge aura VFX");
            effect.AddComponent<MagicChargeAuraEffect>().Initialize(anchor, lifetime);
            return effect;
        }

        public static void SpawnMagicRelease(Vector3 center, Vector3 facing)
        {
            SpawnRing(center, 0.48f, new Color(0.48f, 0.92f, 1f), 0.22f);
            SpawnSwordSlash(
                center,
                facing,
                0.64f,
                new Color(0.56f, 0.94f, 1f),
                0.2f);
        }

        public static void SpawnRing(Vector3 center, float radius, Color color, float lifetime = 0.3f)
        {
            var effect = new GameObject("Combat ring VFX");
            effect.transform.position = center + Vector3.up * 0.035f;
            var line = effect.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = 49;
            line.widthMultiplier = 0.055f;
            line.startColor = color;
            line.endColor = color;

            Material material = RuntimeMaterialFactory.CreateUnlit("Combat VFX material", color);
            if (material != null)
            {
                line.material = material;
            }

            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = i / (float)(line.positionCount - 1) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            effect.AddComponent<FadeLineEffect>().Initialize(line, lifetime, material);
        }

        public static void SpawnSwordSlash(
            Vector3 center,
            Vector3 facing,
            float radius,
            Color color,
            float lifetime = 0.3f)
        {
            Vector3 planarFacing = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (radius <= 0f || planarFacing.sqrMagnitude <= 0.001f)
            {
                return;
            }

            planarFacing.Normalize();
            Camera viewCamera = Camera.main;
            Vector3 viewRight = viewCamera != null ? viewCamera.transform.right : Vector3.right;
            bool mirrored = Vector3.Dot(planarFacing, viewRight) < 0f;
            Vector3[] path = SwordSlashTrailGeometry.Build(radius * 0.92f, mirrored);

            var effect = new GameObject("Sword slash trail VFX");
            effect.transform.position = center + Vector3.up * 0.76f + planarFacing * (radius * 0.42f);
            if (viewCamera != null)
            {
                effect.transform.rotation = Quaternion.LookRotation(
                    viewCamera.transform.forward,
                    viewCamera.transform.up);
            }
            else
            {
                effect.transform.rotation = Quaternion.LookRotation(-planarFacing, Vector3.up);
            }

            Color glowColor = color;
            glowColor.a = 0.42f;
            Color coreColor = Color.Lerp(color, Color.white, 0.78f);
            coreColor.a = 0.96f;
            Material glowMaterial = RuntimeMaterialFactory.CreateUnlit("Sword slash glow material", glowColor);
            Material coreMaterial = RuntimeMaterialFactory.CreateUnlit("Sword slash core material", coreColor);
            LineRenderer glow = CreateSlashLine(effect.transform, "Glow", 0.14f, glowColor, glowMaterial);
            LineRenderer core = CreateSlashLine(effect.transform, "Core", 0.052f, coreColor, coreMaterial);

            effect.AddComponent<SwordSlashTrailEffect>().Initialize(
                glow,
                core,
                path,
                lifetime,
                glowColor,
                coreColor,
                glowMaterial,
                coreMaterial);
        }

        private static LineRenderer CreateSlashLine(
            Transform parent,
            string name,
            float width,
            Color color,
            Material material)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.loop = false;
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.sortingOrder = 1200;
            line.startColor = color;
            line.endColor = color;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.08f),
                new Keyframe(0.28f, 0.72f),
                new Keyframe(0.72f, 1f),
                new Keyframe(1f, 0.08f));
            if (material != null)
            {
                line.material = material;
            }

            return line;
        }

        private sealed class FadeLineEffect : MonoBehaviour
        {
            private LineRenderer line;
            private float lifetime;
            private float elapsed;
            private Material ownedMaterial;

            public void Initialize(LineRenderer target, float duration, Material material)
            {
                line = target;
                ownedMaterial = material;
                lifetime = Mathf.Max(0.05f, duration);
                transform.localScale = Vector3.one * 0.1f;
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                transform.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, t);
                Color color = line.startColor;
                color.a = 1f - t;
                line.startColor = color;
                line.endColor = color;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                if (ownedMaterial != null)
                {
                    Destroy(ownedMaterial);
                    ownedMaterial = null;
                }
            }
        }

        private sealed class SwordSlashTrailEffect : MonoBehaviour
        {
            private LineRenderer glow;
            private LineRenderer core;
            private Vector3[] path;
            private float lifetime;
            private float elapsed;
            private Color glowColor;
            private Color coreColor;
            private Material glowMaterial;
            private Material coreMaterial;

            public void Initialize(
                LineRenderer glowLine,
                LineRenderer coreLine,
                Vector3[] trajectory,
                float duration,
                Color baseGlowColor,
                Color baseCoreColor,
                Material ownedGlowMaterial,
                Material ownedCoreMaterial)
            {
                glow = glowLine;
                core = coreLine;
                path = trajectory;
                glowColor = baseGlowColor;
                coreColor = baseCoreColor;
                glowMaterial = ownedGlowMaterial;
                coreMaterial = ownedCoreMaterial;
                lifetime = Mathf.Max(0.12f, duration);
                ApplyVisibleSegment(0, 1);
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.72f));
                int head = Mathf.Clamp(
                    Mathf.CeilToInt(sweep * (path.Length - 1)),
                    1,
                    path.Length - 1);
                int trailPoints = Mathf.Max(5, Mathf.CeilToInt(path.Length * 0.38f));
                int tail = Mathf.Max(0, head - trailPoints);
                ApplyVisibleSegment(tail, head);

                float opacity = t <= 0.7f ? 1f : 1f - Mathf.InverseLerp(0.7f, 1f, t);
                ApplyTrailColor(glow, glowColor, opacity);
                ApplyTrailColor(core, coreColor, opacity);
                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }

            private void ApplyVisibleSegment(int tail, int head)
            {
                int count = Mathf.Max(2, head - tail + 1);
                glow.positionCount = count;
                core.positionCount = count;
                for (int index = 0; index < count; index++)
                {
                    Vector3 point = path[Mathf.Min(head, tail + index)];
                    glow.SetPosition(index, point);
                    core.SetPosition(index, point);
                }
            }

            private static void ApplyTrailColor(LineRenderer line, Color color, float opacity)
            {
                Color tail = color;
                tail.a *= opacity * 0.12f;
                Color head = color;
                head.a *= opacity;
                line.startColor = tail;
                line.endColor = head;
            }

            private void OnDestroy()
            {
                if (glowMaterial != null)
                {
                    Destroy(glowMaterial);
                    glowMaterial = null;
                }

                if (coreMaterial != null)
                {
                    Destroy(coreMaterial);
                    coreMaterial = null;
                }
            }
        }
    }
}
