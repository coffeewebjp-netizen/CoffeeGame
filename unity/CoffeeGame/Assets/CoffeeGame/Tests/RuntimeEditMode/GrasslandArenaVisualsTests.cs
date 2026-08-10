using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class GrasslandArenaVisualsTests
    {
        [Test]
        public void GrasslandTextures_AreRuntimeResourcesWithExpectedWrapping()
        {
            Texture2D ground = Resources.Load<Texture2D>(GrasslandArenaVisuals.GroundTextureResource);
            Texture2D backdrop = Resources.Load<Texture2D>(GrasslandArenaVisuals.BackdropTextureResource);

            Assert.That(ground, Is.Not.Null);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(ground.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(backdrop.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
        }

        [Test]
        public void Backdrop_IsVisualOnlyAndDoesNotCreateCollision()
        {
            var root = new GameObject("Grassland test root");
            try
            {
                GameObject backdrop = GrasslandArenaVisuals.CreateBackdrop(root.transform);

                Assert.That(backdrop.GetComponent<Collider>(), Is.Null);
                Assert.That(backdrop.GetComponent<MeshFilter>()?.sharedMesh, Is.Not.Null);
                Assert.That(backdrop.GetComponent<MeshRenderer>()?.sharedMaterial?.mainTexture, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DepthAccents_AddLitGeometryWithoutChangingCollision()
        {
            var root = new GameObject("Grassland accent test root");
            try
            {
                GameObject accents = GrasslandArenaVisuals.CreateDepthAccents(root.transform);

                Assert.That(accents.GetComponentsInChildren<Renderer>(), Has.Length.GreaterThanOrEqualTo(9));
                Assert.That(accents.GetComponentsInChildren<Collider>(), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
