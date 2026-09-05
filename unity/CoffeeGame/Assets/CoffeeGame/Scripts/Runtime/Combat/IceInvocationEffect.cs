using UnityEngine;

namespace CoffeeGame.Combat
{
    [DisallowMultipleComponent]
    public sealed class IceInvocationEffect : MonoBehaviour
    {
        private readonly Transform[] shards = new Transform[10];
        private readonly Material[] crystalMaterials = new Material[11];
        private readonly LineRenderer[] rings = new LineRenderer[2];
        private readonly LineRenderer[] runes = new LineRenderer[12];
        private readonly LineRenderer[] streams = new LineRenderer[3];
        private Transform spear;
        private Transform anchor;
        private Material glow;
        private float lifetime;
        private float elapsed;

        public void Initialize(Transform followTarget, float duration)
        {
            anchor = followTarget;
            lifetime = Mathf.Max(0.12f, duration);
            glow = CombatGlowVisuals.CreateMaterial("Ice invocation glow", new Color(0.35f, 0.83f, 1f, 0.85f));
            for (int i = 0; i < rings.Length; i++)
                rings[i] = CombatGlowVisuals.Line(transform, "Invocation circle", glow, 64, i == 0 ? 0.045f : 0.022f, true);
            for (int i = 0; i < runes.Length; i++)
                runes[i] = CombatGlowVisuals.Line(transform, "Ice sigil", glow, 5, 0.024f);
            for (int i = 0; i < streams.Length; i++)
            {
                streams[i] = CombatGlowVisuals.Line(transform, "Rising ice spiral", glow, 40, 0.045f);
                streams[i].widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f));
            }
            for (int i = 0; i < shards.Length; i++)
            {
                GameObject crystal = IceCrystalVisuals.CreateCrystal(transform, "Charge crystal",
                    new Vector3(0.04f, 0.12f, 0.04f), IceCrystalVisuals.Frost, out Material material);
                shards[i] = crystal.transform;
                crystalMaterials[i] = material;
            }
            spear = IceCrystalVisuals.CreateCrystal(transform, "Charge spear",
                new Vector3(0.055f, 0.1f, 0.055f), IceCrystalVisuals.Core, out Material spearMaterial).transform;
            crystalMaterials[10] = spearMaterial;
            Apply(0f);
        }

        private void Update()
        {
            if (anchor == null) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            Apply(Mathf.Clamp01(elapsed / lifetime));
            if (elapsed >= lifetime) Destroy(gameObject);
        }

        private void Apply(float t)
        {
            if (anchor == null) return;
            transform.position = anchor.position;
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.16f));
            float radius = Mathf.Lerp(0.25f, 0.86f, appear);
            float alpha = appear * Mathf.Lerp(0.35f, 1f, t * t);
            for (int i = 0; i < rings.Length; i++)
            {
                float r = radius * (i == 0 ? 1f : 0.77f);
                for (int j = 0; j < 64; j++)
                {
                    float a = j * Mathf.PI * 2f / 64f;
                    rings[i].SetPosition(j, new Vector3(Mathf.Cos(a) * r, 0.045f, Mathf.Sin(a) * r));
                }
                CombatGlowVisuals.Alpha(rings[i], alpha);
            }
            for (int i = 0; i < runes.Length; i++)
            {
                float a = i * Mathf.PI / 6f - elapsed * 0.9f;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 center = radial * radius * 0.88f + Vector3.up * 0.05f;
                runes[i].SetPosition(0, center - radial * 0.055f);
                runes[i].SetPosition(1, center + tangent * 0.04f);
                runes[i].SetPosition(2, center + radial * 0.055f);
                runes[i].SetPosition(3, center - tangent * 0.04f);
                runes[i].SetPosition(4, center - radial * 0.055f);
                CombatGlowVisuals.Alpha(runes[i], alpha);
            }
            for (int i = 0; i < streams.Length; i++)
            {
                for (int j = 0; j < 40; j++)
                {
                    float u = j / 39f;
                    float a = i * Mathf.PI * 2f / 3f + u * Mathf.PI * 2.2f + elapsed * 8f;
                    float r = radius * Mathf.Lerp(0.95f, 0.19f, u) * Mathf.Lerp(1f, 0.58f, t * t);
                    streams[i].SetPosition(j, new Vector3(Mathf.Cos(a) * r, 0.08f + u * 1.1f, Mathf.Sin(a) * r));
                }
                CombatGlowVisuals.Alpha(streams[i], alpha * 0.65f);
            }
            for (int i = 0; i < shards.Length; i++)
            {
                float a = elapsed * Mathf.Lerp(4f, 10f, t) + i * Mathf.PI * 2f / shards.Length;
                float r = Mathf.Lerp(0.67f, 0.13f, t * t);
                shards[i].localPosition = new Vector3(Mathf.Cos(a) * r, 0.12f + t * 0.88f + Mathf.Sin(a * 2f) * 0.1f, Mathf.Sin(a) * r);
                shards[i].localRotation = Quaternion.Euler(35f, a * Mathf.Rad2Deg, elapsed * 130f);
                shards[i].localScale = new Vector3(0.038f, 0.12f, 0.038f) * appear * (0.7f + t * 0.5f);
            }
            spear.localPosition = Vector3.up * 0.98f;
            spear.localRotation = Quaternion.Euler(0f, elapsed * 110f, 0f);
            spear.localScale = new Vector3(0.065f, Mathf.Lerp(0.02f, 0.36f, t * t), 0.065f) * appear;
        }

        private void OnDestroy()
        {
            foreach (Material material in crystalMaterials) if (material != null) Destroy(material);
            foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
            if (glow != null) Destroy(glow);
        }
    }
}
