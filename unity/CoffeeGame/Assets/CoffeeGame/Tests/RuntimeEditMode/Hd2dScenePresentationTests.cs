using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class Hd2dScenePresentationTests
    {
        [Test]
        public void Create_EnablesAGlobalPostProfileWithoutChangingCameraProjection()
        {
            var root = new GameObject("HD-2D presentation test root");
            var cameraObject = new GameObject("HD-2D presentation test camera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                Hd2dScenePresentation presentation = Hd2dScenePresentation.Create(root.transform, camera);

                Assert.That(presentation.RuntimeVolume, Is.Not.Null);
                Assert.That(presentation.RuntimeVolume.isGlobal, Is.True);
                Assert.That(presentation.RuntimeVolume.sharedProfile, Is.Not.Null);
                Assert.That(presentation.RuntimeVolume.sharedProfile.components, Has.Count.EqualTo(3));
                Assert.That(camera.orthographic, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
