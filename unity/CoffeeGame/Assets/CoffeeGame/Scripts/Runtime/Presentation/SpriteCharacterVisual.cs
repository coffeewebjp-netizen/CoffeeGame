using System.Collections;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class SpriteCharacterVisual : MonoBehaviour, ICharacterVisual
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float baseScale = 1f;

        private Coroutine actionRoutine;
        private CharacterAction locomotion = CharacterAction.Idle;
        private Vector3 baseLocalScale;
        private Color baseColor = Color.white;

        public SpriteRenderer Renderer => spriteRenderer;

        public void Initialize(Sprite sprite, float scale, Camera cameraToFace)
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }

            spriteRenderer.sprite = sprite;
            baseScale = Mathf.Max(0.01f, scale);
            transform.localScale = Vector3.one * baseScale;
            baseLocalScale = transform.localScale;
            baseColor = spriteRenderer.color;

            CameraFacingBillboard billboard = GetComponent<CameraFacingBillboard>();
            if (billboard == null)
            {
                billboard = gameObject.AddComponent<CameraFacingBillboard>();
            }
            billboard.SetCamera(cameraToFace);
        }

        public void ResetState(Vector3 worldDirection)
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
                actionRoutine = null;
            }

            locomotion = CharacterAction.Idle;
            transform.localRotation = Quaternion.identity;
            transform.localScale = baseLocalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            SetFacing(worldDirection);
        }

        public void SetFacing(Vector3 worldDirection)
        {
            if (spriteRenderer == null || Mathf.Abs(worldDirection.x) < 0.05f)
            {
                return;
            }

            spriteRenderer.flipX = worldDirection.x < 0f;
        }

        public void SetLocomotion(CharacterAction action, float normalizedSpeed)
        {
            locomotion = action;
            if (actionRoutine != null)
            {
                return;
            }

            float pulse = action == CharacterAction.Run ? 0.045f : action == CharacterAction.Walk ? 0.025f : 0f;
            float phase = Time.time * (action == CharacterAction.Run ? 15f : 9f);
            transform.localScale = baseLocalScale * (1f + Mathf.Sin(phase) * pulse);
        }

        public void PlayAction(CharacterAction action, float duration)
        {
            if (actionRoutine != null)
            {
                StopCoroutine(actionRoutine);
            }
            actionRoutine = StartCoroutine(AnimateAction(action, Mathf.Max(0.05f, duration)));
        }

        public void SetAirHeight(float height)
        {
            // The actor root already moves on Y. This hook remains for a later Animator visual.
        }

        public void SetTint(Color color)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
        }

        private IEnumerator AnimateAction(CharacterAction action, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scalePulse = Mathf.Sin(t * Mathf.PI) * 0.1f;
                float angle = action == CharacterAction.SpinRelease ? t * 360f :
                    action == CharacterAction.Sword || action == CharacterAction.AirSlash ? Mathf.Sin(t * Mathf.PI) * -12f : 0f;
                transform.localScale = baseLocalScale * (1f + scalePulse);
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                yield return null;
            }

            transform.localRotation = Quaternion.identity;
            transform.localScale = baseLocalScale;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = baseColor;
            }
            actionRoutine = null;
            SetLocomotion(locomotion, 0f);
        }
    }
}
