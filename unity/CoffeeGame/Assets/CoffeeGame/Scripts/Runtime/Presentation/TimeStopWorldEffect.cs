using System;
using System.Collections.Generic;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class TimeStopWorldEffect : MonoBehaviour
    {
        public const float TransitionSeconds = .65f;
        private Camera worldCamera, actorCamera;
        private Canvas overlay;
        private RawImage image;
        private Material material;
        private RenderTexture worldTexture, actorTexture, previousTarget;
        private bool requested, compositing;
        private float progress;
        private readonly List<(Renderer renderer, bool previous)> hidden = new List<(Renderer, bool)>();
        private Renderer[] renderers = Array.Empty<Renderer>();
        private readonly HashSet<Renderer> actors = new HashSet<Renderer>();
        private readonly Dictionary<Renderer, bool> classification = new Dictionary<Renderer, bool>();
        public bool IsActive => requested;
        public bool IsCompositing => compositing;
        public float Progress => progress;
        public float BackgroundVerticalScale => Mathf.Cos(Mathf.PI * Mathf.SmoothStep(0, 1, progress));

        public void Initialize(Camera value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (worldCamera != value) StopImmediately();
            worldCamera = value;
        }

        public void SetActive(bool value)
        {
            requested = value;
            if (!value || compositing) return;
            if (worldCamera == null) worldCamera = GetComponent<Camera>();
            EnsureOverlay();
            previousTarget = worldCamera.targetTexture;
            compositing = true;
            EnsureTextures();
            worldCamera.targetTexture = worldTexture;
            SyncActorCamera();
            overlay.enabled = true;
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
        }

        public void Advance(float seconds)
        {
            if (!compositing) return;
            progress = Mathf.MoveTowards(progress, requested ? 1 : 0, Mathf.Max(0, seconds) / TransitionSeconds);
            material.SetFloat("_Progress", progress);
            if (!requested && progress <= 0) StopImmediately();
        }

        private void LateUpdate()
        {
            if (!compositing) return;
            Advance(Time.deltaTime);
            if (!compositing) return;
            EnsureTextures();
            SyncActorCamera();
        }

        private void SyncActorCamera()
        {
            actorCamera.CopyFrom(worldCamera);
            actorCamera.transform.SetPositionAndRotation(worldCamera.transform.position, worldCamera.transform.rotation);
            actorCamera.clearFlags = CameraClearFlags.SolidColor;
            actorCamera.backgroundColor = Color.clear;
            actorCamera.targetTexture = actorTexture;
            actorCamera.depth = worldCamera.depth + 1;
            actorCamera.allowHDR = false;
            actorCamera.allowMSAA = false;
            actorCamera.enabled = worldCamera.enabled;
            actorCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        }

        public static bool IsActorRenderer(Renderer renderer)
        {
            if (renderer == null) return false;
            if (renderer.GetComponentInParent<Health>() != null) return true;
            var owner = CombatOwnership.Resolve(renderer.gameObject);
            return owner != null && owner.GetComponentInParent<Health>() != null;
        }

        private void BeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!compositing || camera != worldCamera && camera != actorCamera) return;
            RestoreRenderers();
            if (camera == worldCamera)
            {
                // Refresh once per frame so newly spawned owned spells stay with their caster.
                renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
                actors.Clear();
                foreach (var renderer in renderers)
                {
                    if (!classification.TryGetValue(renderer, out bool isActor))
                        classification[renderer] = isActor = IsActorRenderer(renderer);
                    if (isActor) actors.Add(renderer);
                }
            }
            foreach (var renderer in renderers)
            {
                if (renderer == null || (camera == worldCamera ? !actors.Contains(renderer) : actors.Contains(renderer))) continue;
                hidden.Add((renderer, renderer.forceRenderingOff));
                renderer.forceRenderingOff = true;
            }
        }

        private void EndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == worldCamera || camera == actorCamera) RestoreRenderers();
        }

        private void RestoreRenderers()
        {
            foreach (var item in hidden) if (item.renderer != null) item.renderer.forceRenderingOff = item.previous;
            hidden.Clear();
        }

        private void EnsureOverlay()
        {
            if (overlay != null) return;
            var shader = Resources.Load<Shader>("Materials/TimeStopWorldEffect");
            if (shader == null) throw new InvalidOperationException("Missing time stop compositor shader.");
            material = new Material(shader) { name = "Time stop background and upright actors" };
            overlay = new GameObject("Time stop composite").AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = short.MinValue;
            image = new GameObject("Animated background with upright actors").AddComponent<RawImage>();
            image.transform.SetParent(overlay.transform, false);
            image.raycastTarget = false;
            image.material = material;
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            overlay.enabled = false;
            actorCamera = new GameObject("Time stop upright actor camera").AddComponent<Camera>();
            actorCamera.enabled = false;
        }

        private void EnsureTextures()
        {
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            if (worldTexture != null && worldTexture.width == width && worldTexture.height == height) return;
            Free(worldTexture); Free(actorTexture);
            worldTexture = Texture("Time stop background", width, height);
            actorTexture = Texture("Time stop upright actors", width, height);
            worldCamera.targetTexture = worldTexture;
            image.texture = worldTexture;
            material.SetTexture("_ActorTex", actorTexture);
        }

        private static RenderTexture Texture(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            { name = name, filterMode = FilterMode.Bilinear, useMipMap = false };
            texture.Create();
            return texture;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        // Uses the live camera filtering and the same compositor material as the screen.
        public void CaptureComposite(RenderTexture destination)
        {
            if (!compositing) throw new InvalidOperationException("Time stop composite is inactive.");
            SyncActorCamera();
            RenderPipeline.SubmitRenderRequest(worldCamera, new RenderPipeline.StandardRequest { destination = worldTexture });
            RenderPipeline.SubmitRenderRequest(actorCamera, new RenderPipeline.StandardRequest { destination = actorTexture });
            Graphics.Blit(worldTexture, destination, material);
        }
#endif

        public void StopImmediately()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            RestoreRenderers();
            if (compositing && worldCamera != null && worldCamera.targetTexture == worldTexture) worldCamera.targetTexture = previousTarget;
            previousTarget = null;
            requested = compositing = false;
            progress = 0;
            if (material != null) material.SetFloat("_Progress", 0);
            if (overlay != null) overlay.enabled = false;
            if (actorCamera != null) actorCamera.enabled = false;
            actors.Clear(); classification.Clear(); renderers = Array.Empty<Renderer>();
        }

        private void OnDisable() => StopImmediately();
        private void OnDestroy()
        {
            StopImmediately();
            Free(worldTexture); Free(actorTexture); Free(material);
            if (overlay != null) Free(overlay.gameObject);
            if (actorCamera != null) Free(actorCamera.gameObject);
        }

        private static void Free(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
