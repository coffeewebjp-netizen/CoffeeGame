using UnityEngine;

namespace CoffeeGame.Combat
{
    public readonly struct IaiCinematicFrame
    {
        public IaiCinematicFrame(float blackoutAlpha, float flashAlpha, float slashAlpha, float slashProgress)
        {
            BlackoutAlpha = blackoutAlpha;
            FlashAlpha = flashAlpha;
            SlashAlpha = slashAlpha;
            SlashProgress = slashProgress;
        }

        public float BlackoutAlpha { get; }
        public float FlashAlpha { get; }
        public float SlashAlpha { get; }
        public float SlashProgress { get; }
    }

    public static class IaiCinematicTiming
    {
        public const float StrikeTime = 0.18f;
        public const float Duration = 0.58f;

        public static IaiCinematicFrame Sample(float elapsed)
        {
            float time = Mathf.Clamp(elapsed, 0f, Duration);
            float blackout = time < 0.065f
                ? Mathf.SmoothStep(0f, 0.97f, time / 0.065f)
                : time < 0.34f
                    ? 0.97f
                    : Mathf.Lerp(0.97f, 0f, Mathf.InverseLerp(0.34f, Duration, time));

            float slashProgress = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.075f, StrikeTime, time));
            float slashAlpha = time < 0.075f
                ? 0f
                : time < 0.29f
                    ? 1f
                    : Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.29f, 0.48f, time));

            float flashDistance = Mathf.Abs(time - 0.205f);
            float flash = flashDistance >= 0.055f
                ? 0f
                : 1f - flashDistance / 0.055f;
            flash = Mathf.SmoothStep(0f, 1f, flash);

            return new IaiCinematicFrame(blackout, flash, slashAlpha, slashProgress);
        }
    }

    [DisallowMultipleComponent]
    public sealed class IaiCinematicEffect : MonoBehaviour
    {
        private Vector3 worldCenter;
        private Vector3 worldFacing;
        private float radius;
        private float elapsed;
        private bool strikeVisualEmitted;

        public void Initialize(Vector3 center, Vector3 facing, float effectRadius)
        {
            worldCenter = center;
            worldFacing = Vector3.ProjectOnPlane(facing, Vector3.up).normalized;
            if (worldFacing.sqrMagnitude < 0.01f)
            {
                worldFacing = Vector3.forward;
            }
            radius = Mathf.Max(0.1f, effectRadius);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!strikeVisualEmitted && elapsed >= IaiCinematicTiming.StrikeTime)
            {
                strikeVisualEmitted = true;
                CombatVfxFactory.SpawnRing(
                    worldCenter,
                    radius,
                    new Color(1f, 0.9f, 0.58f),
                    0.32f);
                CombatVfxFactory.SpawnSwordSlash(
                    worldCenter,
                    worldFacing,
                    radius * 1.08f,
                    new Color(1f, 0.96f, 0.82f),
                    0.3f);
            }

            if (elapsed >= IaiCinematicTiming.Duration)
            {
                Destroy(gameObject);
            }
        }

        private void OnGUI()
        {
            IaiCinematicFrame frame = IaiCinematicTiming.Sample(elapsed);
            if (frame.BlackoutAlpha <= 0f && frame.FlashAlpha <= 0f && frame.SlashAlpha <= 0f)
            {
                return;
            }

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.depth = -32000;

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            GUI.color = new Color(0.005f, 0.008f, 0.015f, frame.BlackoutAlpha);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);

            if (frame.SlashAlpha > 0f)
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                float width = Mathf.Max(Screen.width, Screen.height) * 1.65f * frame.SlashProgress;
                GUIUtility.RotateAroundPivot(-14f, center);

                GUI.color = new Color(0.36f, 0.86f, 1f, frame.SlashAlpha * 0.42f);
                GUI.DrawTexture(new Rect(center.x - width * 0.5f, center.y - 12f, width, 24f), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.985f, 0.9f, frame.SlashAlpha);
                GUI.DrawTexture(new Rect(center.x - width * 0.5f, center.y - 2.5f, width, 5f), Texture2D.whiteTexture);

                GUI.matrix = previousMatrix;
                GUIUtility.RotateAroundPivot(-19f, center + new Vector2(0f, Screen.height * 0.035f));
                GUI.color = new Color(0.72f, 0.92f, 1f, frame.SlashAlpha * 0.46f);
                GUI.DrawTexture(new Rect(center.x - width * 0.43f, center.y - 1f, width * 0.86f, 2f), Texture2D.whiteTexture);
            }

            GUI.matrix = previousMatrix;
            if (frame.FlashAlpha > 0f)
            {
                GUI.color = new Color(1f, 1f, 1f, frame.FlashAlpha * 0.94f);
                GUI.DrawTexture(screen, Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }
    }
}
