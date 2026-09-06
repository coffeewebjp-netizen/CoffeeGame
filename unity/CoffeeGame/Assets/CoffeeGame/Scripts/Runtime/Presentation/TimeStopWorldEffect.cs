using System;
using UnityEngine;
using UnityEngine.UI;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class TimeStopWorldEffect : MonoBehaviour
    {
        private Camera worldCamera;
        private Canvas overlayCanvas;
        private RawImage worldImage;
        private Material effectMaterial;
        private RenderTexture worldTexture;
        private RenderTexture previousTarget;
        private bool active;

        public bool IsActive => active;

        public void Initialize(Camera cameraToRender)
        {
            if (cameraToRender == null)
            {
                throw new ArgumentNullException(nameof(cameraToRender));
            }

            if (worldCamera != null && worldCamera != cameraToRender && active)
            {
                RestoreCameraTarget();
            }

            worldCamera = cameraToRender;
            EnsureOverlay();
        }

        public void SetActive(bool value)
        {
            if (active == value)
            {
                return;
            }

            active = value;
            if (active)
            {
                if (worldCamera == null)
                {
                    worldCamera = GetComponent<Camera>();
                }

                EnsureOverlay();
                previousTarget = worldCamera.targetTexture;
                EnsureRenderTexture();
                worldCamera.targetTexture = worldTexture;
                overlayCanvas.enabled = true;
            }
            else
            {
                RestoreCameraTarget();
                if (overlayCanvas != null)
                {
                    overlayCanvas.enabled = false;
                }
            }
        }

        private void EnsureOverlay()
        {
            if (overlayCanvas != null)
            {
                return;
            }

            Shader shader = Resources.Load<Shader>("Materials/TimeStopWorldEffect");
            if (shader == null)
            {
                throw new InvalidOperationException("TimeStopWorldEffect shader resource is missing.");
            }

            effectMaterial = new Material(shader) { name = "Time stop world compositor" };
            var canvasObject = new GameObject("Time stop world overlay");
            overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = short.MinValue;
            canvasObject.AddComponent<CanvasScaler>();
            var imageObject = new GameObject("Vertically flipped grayscale world");
            imageObject.transform.SetParent(canvasObject.transform, false);
            worldImage = imageObject.AddComponent<RawImage>();
            worldImage.raycastTarget = false;
            worldImage.material = effectMaterial;
            worldImage.uvRect = new Rect(0f, 1f, 1f, -1f);
            RectTransform rect = worldImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            overlayCanvas.enabled = false;
        }

        private void EnsureRenderTexture()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);
            if (worldTexture != null && worldTexture.width == width && worldTexture.height == height)
            {
                return;
            }

            RenderTexture oldTexture = worldTexture;
            worldTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "Time stop world texture",
                filterMode = FilterMode.Bilinear,
                useMipMap = false
            };
            worldTexture.Create();
            worldImage.texture = worldTexture;
            if (active && worldCamera != null)
            {
                worldCamera.targetTexture = worldTexture;
            }

            DestroySafely(oldTexture);
        }

        private void RestoreCameraTarget()
        {
            if (worldCamera != null && worldCamera.targetTexture == worldTexture)
            {
                worldCamera.targetTexture = previousTarget;
            }
            previousTarget = null;
        }

        private void LateUpdate()
        {
            if (active)
            {
                EnsureRenderTexture();
            }
        }

        private void OnDisable()
        {
            if (active)
            {
                SetActive(false);
            }
        }

        private void OnDestroy()
        {
            RestoreCameraTarget();
            DestroySafely(worldTexture);
            DestroySafely(effectMaterial);
            if (overlayCanvas != null)
            {
                DestroySafely(overlayCanvas.gameObject);
            }
        }

        private static void DestroySafely(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
