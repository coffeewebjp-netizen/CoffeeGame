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
            effect.AddComponent<IceInvocationEffect>().Initialize(anchor, lifetime);
            return effect;
        }

        public static void SpawnMagicRelease(Vector3 center, Vector3 facing)
        {
            Vector3 planarFacing = Vector3.ProjectOnPlane(facing, Vector3.up);
            if (planarFacing.sqrMagnitude < 0.001f)
            {
                planarFacing = Vector3.forward;
            }

            planarFacing.Normalize();
            Vector3 origin = center + Vector3.up * 0.72f + planarFacing * 0.28f;
            SpawnRing(center, 0.8f, IceCrystalVisuals.Frost, 0.24f);
            SpawnIceBurst(origin, planarFacing, 0.7f, 0.32f);
        }

        public static void SpawnPlungeImpact(Vector3 center, float radius)
        {
            SpawnRing(center, radius, new Color(0.96f, 0.93f, 0.78f), 0.28f);
            SpawnRing(center, radius * 0.55f, new Color(1f, 0.98f, 0.9f), 0.18f);
            var flash = new GameObject("Plunge glint VFX");
            flash.transform.position = center + Vector3.up * 0.04f;
            var line = flash.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = 0.035f;
            line.numCapVertices = 0;
            line.SetPosition(0, center + Vector3.up * 0.02f);
            line.SetPosition(1, center + Vector3.up * 1.15f);
            Color glint = new Color(1f, 0.97f, 0.82f, 0.95f);
            line.startColor = glint;
            line.endColor = new Color(1f, 0.97f, 0.82f, 0f);
            Material material = RuntimeMaterialFactory.CreateUnlit("Plunge glint material", glint);
            if (material != null)
            {
                line.material = material;
            }

            Object.Destroy(flash, 0.22f);
            if (material != null)
            {
                Object.Destroy(material, 0.22f);
            }

            SpawnIceBurst(center + Vector3.up * 0.12f, Vector3.up, radius * 0.7f, 0.28f);
        }

        public static void SpawnIceBurst(Vector3 origin, Vector3 direction, float radius, float lifetime)
        {
            var effect = new GameObject("Ice burst VFX");
            effect.transform.position = origin;
            effect.AddComponent<IceBurstEffect>().Initialize(direction, radius, lifetime);
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

            Color glowColor = Color.Lerp(color, new Color(1f, 0.93f, 0.62f), 0.45f);
            glowColor.a = 0.48f;
            Color coreColor = new Color(1f, 0.99f, 0.94f, 1f);
            Material glowMaterial = CombatGlowVisuals.CreateMaterial("Sword slash glow material", glowColor);
            Material coreMaterial = CombatGlowVisuals.CreateMaterial("Sword slash core material", coreColor);
            LineRenderer glow = CreateSlashLine(effect.transform, "Glow", 0.16f, Color.white, glowMaterial);
            LineRenderer core = CreateSlashLine(effect.transform, "Core", 0.035f, Color.white, coreMaterial);

            Mesh ribbonMesh = SwordSlashTrailGeometry.BuildRibbon(path, radius * 0.15f);
            MeshRenderer ribbon = null;
            Material ribbonMaterial = null;
            if (ribbonMesh != null)
            {
                var ribbonObject = new GameObject("Ribbon");
                ribbonObject.transform.SetParent(effect.transform, false);
                ribbonObject.AddComponent<MeshFilter>().sharedMesh = ribbonMesh;
                ribbon = ribbonObject.AddComponent<MeshRenderer>();
                Color ribbonColor = new Color(1f, 0.94f, 0.7f, 0.62f);
                ribbonMaterial = CombatGlowVisuals.CreateMaterial("Sword slash ribbon material", ribbonColor);
                if (ribbonMaterial != null)
                {
                    ribbon.sharedMaterial = ribbonMaterial;
                }
            }

            effect.AddComponent<SwordSlashTrailEffect>().Initialize(
                glow,
                core,
                ribbon,
                path,
                lifetime,
                glowColor,
                coreColor,
                glowMaterial,
                coreMaterial,
                ribbonMaterial);
            CombatGlowVisuals.Sparks(effect.transform.position + effect.transform.TransformVector(path[path.Length - 1]), planarFacing, false);
        }

        public static void SpawnSwordImpact(Vector3 center, Vector3 facing)
        {
            CombatGlowVisuals.Sparks(center + Vector3.up * 0.65f, facing, true);
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
            line.numCapVertices = 0;
            line.numCornerVertices = 1;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = 1200;
            line.startColor = color;
            line.endColor = color;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.12f),
                new Keyframe(0.16f, 0.7f),
                new Keyframe(0.52f, 1f),
                new Keyframe(1f, 0.04f));
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

        private sealed class IceBurstEffect : MonoBehaviour
        {
            private readonly Transform[] shards = new Transform[10];
            private readonly Vector3[] velocities = new Vector3[10];
            private readonly Material[] materials = new Material[10];
            private float lifetime;
            private float elapsed;

            public void Initialize(Vector3 direction, float radius, float duration)
            {
                Vector3 axis = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.up;
                lifetime = Mathf.Max(0.12f, duration);
                for (int index = 0; index < shards.Length; index++)
                {
                    float polar = (index / (float)shards.Length) * Mathf.PI * 2f;
                    Vector3 spray = (Quaternion.AngleAxis(polar * Mathf.Rad2Deg, axis) * Vector3.Cross(axis, Vector3.right + Vector3.forward)).normalized;
                    if (spray.sqrMagnitude < 0.01f)
                    {
                        spray = Vector3.right;
                    }

                    spray = Vector3.Slerp(axis, spray, 0.72f);
                    GameObject crystal = IceCrystalVisuals.CreateCrystal(
                        transform,
                        $"Burst shard {index + 1}",
                        new Vector3(0.035f, 0.11f, 0.035f),
                        IceCrystalVisuals.Core,
                        out Material material);
                    crystal.transform.rotation = Quaternion.LookRotation(spray) * Quaternion.Euler(90f, 0f, 0f);
                    shards[index] = crystal.transform;
                    velocities[index] = spray * (radius * UnityEngine.Random.Range(2.4f, 3.6f));
                    materials[index] = material;
                }
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                for (int index = 0; index < shards.Length; index++)
                {
                    if (shards[index] == null)
                    {
                        continue;
                    }

                    shards[index].position += velocities[index] * Time.deltaTime;
                    velocities[index] *= 0.88f;
                    shards[index].localScale = new Vector3(0.035f, 0.11f, 0.035f) * Mathf.Lerp(1f, 0.12f, t);
                }

                if (t >= 1f)
                {
                    Destroy(gameObject);
                }
            }

            private void OnDestroy()
            {
                for (int index = 0; index < materials.Length; index++)
                {
                    if (materials[index] != null)
                    {
                        Destroy(materials[index]);
                    }
                }
            }
        }

        private sealed class SwordSlashTrailEffect : MonoBehaviour
        {
            private LineRenderer glow;
            private LineRenderer core;
            private MeshRenderer ribbon;
            private Vector3[] path;
            private float lifetime;
            private float elapsed;
            private Color glowColor;
            private Color coreColor;
            private Material glowMaterial;
            private Material coreMaterial;
            private Material ribbonMaterial;

            public void Initialize(
                LineRenderer glowLine,
                LineRenderer coreLine,
                MeshRenderer ribbonRenderer,
                Vector3[] trajectory,
                float duration,
                Color baseGlowColor,
                Color baseCoreColor,
                Material ownedGlowMaterial,
                Material ownedCoreMaterial,
                Material ownedRibbonMaterial)
            {
                glow = glowLine;
                core = coreLine;
                ribbon = ribbonRenderer;
                path = trajectory;
                glowColor = baseGlowColor;
                coreColor = baseCoreColor;
                glowMaterial = ownedGlowMaterial;
                coreMaterial = ownedCoreMaterial;
                ribbonMaterial = ownedRibbonMaterial;
                lifetime = Mathf.Max(0.12f, duration);
                ApplyVisibleSegment(0, 1);
                SetRibbonOpacity(0f);
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / lifetime);
                float sweep = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.38f));
                int head = Mathf.Clamp(
                    Mathf.CeilToInt(sweep * (path.Length - 1)),
                    1,
                    path.Length - 1);
                int trailPoints = Mathf.Max(2, Mathf.CeilToInt(path.Length * 0.58f));
                int tail = Mathf.Max(0, head - trailPoints);
                ApplyVisibleSegment(tail, head);

                float opacity = t <= 0.55f ? 1f : 1f - Mathf.InverseLerp(0.55f, 1f, t);
                float ribbonOpacity = t < 0.12f
                    ? t / 0.12f
                    : t < 0.48f
                        ? 1f
                        : 1f - Mathf.InverseLerp(0.48f, 1f, t);
                ApplyTrailColor(glow, Color.white, opacity);
                ApplyTrailColor(core, Color.white, opacity);
                SetRibbonOpacity(ribbonOpacity * 0.7f);
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

            private void SetRibbonOpacity(float opacity)
            {
                if (ribbon == null)
                {
                    return;
                }

                ribbon.enabled = opacity > 0.02f;
                if (ribbonMaterial != null && ribbonMaterial.HasProperty("_BaseColor"))
                {
                    Color color = ribbonMaterial.GetColor("_BaseColor");
                    color.a = opacity;
                    ribbonMaterial.SetColor("_BaseColor", color);
                }
            }

            private void OnDestroy()
            {
                if (ribbon != null)
                {
                    MeshFilter filter = ribbon.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null) Destroy(filter.sharedMesh);
                }
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

                if (ribbonMaterial != null)
                {
                    Destroy(ribbonMaterial);
                    ribbonMaterial = null;
                }
            }
        }
    }
}
