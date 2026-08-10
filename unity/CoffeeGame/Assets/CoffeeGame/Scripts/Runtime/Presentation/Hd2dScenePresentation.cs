using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoffeeGame.Presentation
{
    [DisallowMultipleComponent]
    public sealed class Hd2dScenePresentation : MonoBehaviour
    {
        private VolumeProfile ownedProfile;

        public Volume RuntimeVolume { get; private set; }

        public static Hd2dScenePresentation Create(Transform parent, Camera camera)
        {
            var presentationObject = new GameObject("HD-2D scene presentation");
            presentationObject.transform.SetParent(parent, false);
            var presentation = presentationObject.AddComponent<Hd2dScenePresentation>();
            presentation.Initialize(camera);
            return presentation;
        }

        private void Initialize(Camera camera)
        {
            if (camera != null)
            {
                UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true;
                cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }

            RuntimeVolume = gameObject.AddComponent<Volume>();
            RuntimeVolume.isGlobal = true;
            RuntimeVolume.priority = 10f;
            ownedProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            ownedProfile.name = "CoffeeGAME HD-2D runtime profile";
            RuntimeVolume.sharedProfile = ownedProfile;

            Bloom bloom = ownedProfile.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(0.24f);
            bloom.scatter.Override(0.58f);
            bloom.highQualityFiltering.Override(true);

            ColorAdjustments color = ownedProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(-0.04f);
            color.contrast.Override(7f);
            color.saturation.Override(-3f);

            Vignette vignette = ownedProfile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.04f, 0.07f, 0.08f));
            vignette.intensity.Override(0.13f);
            vignette.smoothness.Override(0.58f);
            vignette.rounded.Override(false);
        }

        private void OnDestroy()
        {
            if (ownedProfile != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(ownedProfile);
                }
                else
                {
                    DestroyImmediate(ownedProfile);
                }
                ownedProfile = null;
            }
        }
    }
}
