using CoffeeGame.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Combat
{
    // Explicitly referenced shader variants keep additive fading available in players.
    internal static class CombatGlowVisuals
    {
        public static Material CreateMaterial(string name, Color color)
        {
            Shader shader = Resources.Load<Shader>("Materials/CombatGlow");
            if (shader == null)
                throw new System.InvalidOperationException("CombatGlow shader resource is missing.");
            var material = new Material(shader) { name = name };
            RuntimeMaterialFactory.SetSrgbColor(material, "_BaseColor", color);
            return material;
        }

        public static LineRenderer Line(Transform parent, string name, Material material,
            int points, float width, bool loop = false)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.startColor = line.endColor = Color.white;
            return line;
        }

        public static void Alpha(LineRenderer line, float alpha)
        {
            line.startColor = line.endColor = new Color(1f, 1f, 1f, alpha);
        }

        public static void Sparks(Vector3 position, Vector3 facing, bool impact)
        {
            var effect = new GameObject(impact ? "Sword impact sparks" : "Sword speed sparks");
            effect.transform.position = position;
            effect.AddComponent<CutSparks>().Initialize(facing, impact);
        }

        private sealed class CutSparks : MonoBehaviour
        {
            private readonly LineRenderer[] lines = new LineRenderer[12];
            private readonly Vector3[] directions = new Vector3[12];
            private Material material;
            private float elapsed;
            private float duration;
            private float reach;

            public void Initialize(Vector3 facing, bool impact)
            {
                duration = impact ? 0.24f : 0.2f;
                reach = impact ? 0.74f : 0.48f;
                material = CreateMaterial("Cut spark glow", new Color(1f, 0.78f, 0.38f, 0.85f));
                Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
                Vector3 up = Camera.main != null ? Camera.main.transform.up : Vector3.up;
                for (int i = 0; i < lines.Length; i++)
                {
                    float angle = i * 2.399963f;
                    directions[i] = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle) + facing * 0.15f).normalized;
                    lines[i] = Line(transform, "Spark", material, 2, impact ? 0.033f : 0.018f);
                    lines[i].widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                }
            }

            private void Update()
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < lines.Length; i++)
                {
                    float distance = reach * (0.6f + i % 3 * 0.2f) * Mathf.Sqrt(t);
                    lines[i].SetPosition(0, directions[i] * Mathf.Max(0f, distance - 0.24f * (1f - t)));
                    lines[i].SetPosition(1, directions[i] * distance);
                    Alpha(lines[i], (1f - t) * (1f - t));
                }
                if (t >= 1f) Destroy(gameObject);
            }

            private void OnDestroy() { if (material != null) Destroy(material); }
        }
    }
}
