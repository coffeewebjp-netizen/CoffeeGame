using NUnit.Framework;
using CoffeeGame.World;
using UnityEngine;

namespace CoffeeGame.Presentation.Tests
{
    public sealed class GrasslandArenaVisualsTests
    {
        [Test]
        public void StageLayout_UsesFourByFourChunksAcrossAnExpandedStage()
        {
            Assert.That(StageLayout.ChunkColumns, Is.EqualTo(4));
            Assert.That(StageLayout.ChunkRows, Is.EqualTo(4));
            Assert.That(StageLayout.Width, Is.EqualTo(38.4f).Within(0.001f));
            Assert.That(StageLayout.Depth, Is.EqualTo(21.6f).Within(0.001f));
            Assert.That(StageLayout.GetChunkCenter(0, 0).x, Is.EqualTo(-14.4f).Within(0.001f));
            Assert.That(StageLayout.GetChunkCenter(3, 3).z, Is.EqualTo(8.1f).Within(0.001f));
        }

        [Test]
        public void FixedCameraRig_ClampsItsFollowPointInsideTheExpandedStage()
        {
            var target = new GameObject("Camera target");
            var cameraObject = new GameObject("Camera rig");
            try
            {
                target.transform.position = new Vector3(100f, 2f, 100f);
                FixedCameraRig rig = cameraObject.AddComponent<FixedCameraRig>();
                rig.Initialize(target.transform);
                rig.SetBounds(
                    StageLayout.CameraMinX,
                    StageLayout.CameraMaxX,
                    StageLayout.CameraMinZ,
                    StageLayout.CameraMaxZ);

                Assert.That(cameraObject.transform.position.x, Is.EqualTo(StageLayout.CameraMaxX).Within(0.001f));
                Assert.That(cameraObject.transform.position.z, Is.EqualTo(StageLayout.CameraMaxZ - 8.85f).Within(0.001f));

                target.transform.position = new Vector3(-100f, 2f, -100f);
                rig.Snap();

                Assert.That(cameraObject.transform.position.x, Is.EqualTo(StageLayout.CameraMinX).Within(0.001f));
                Assert.That(cameraObject.transform.position.z, Is.EqualTo(StageLayout.CameraMinZ - 8.85f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void FixedCameraRig_OrbitsAroundTheGroundedTargetAndWrapsYaw()
        {
            var target = new GameObject("Camera orbit target");
            var cameraObject = new GameObject("Camera orbit rig");
            try
            {
                target.transform.position = new Vector3(3f, 2f, -4f);
                FixedCameraRig rig = cameraObject.AddComponent<FixedCameraRig>();
                rig.Initialize(target.transform);
                rig.SetOrbitYaw(450f, true);

                Assert.That(rig.OrbitYawDegrees, Is.EqualTo(90f).Within(0.001f));
                Assert.That(cameraObject.transform.position.x, Is.EqualTo(3f - 8.85f).Within(0.001f));
                Assert.That(cameraObject.transform.position.y, Is.EqualTo(2f + 5.75f).Within(0.001f));
                Assert.That(cameraObject.transform.position.z, Is.EqualTo(-4f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void FixedCameraRig_ClampsVerticalOrbitWithoutInvertingTheCamera()
        {
            var target = new GameObject("Camera pitch target");
            var cameraObject = new GameObject("Camera pitch rig");
            try
            {
                target.transform.position = new Vector3(3f, 2f, -4f);
                FixedCameraRig rig = cameraObject.AddComponent<FixedCameraRig>();
                rig.Initialize(target.transform);
                float orbitRadius = (cameraObject.transform.position - target.transform.position).magnitude;

                rig.SetOrbitYaw(90f);
                rig.SetOrbitPitch(1000f, true);

                Assert.That(rig.OrbitYawDegrees, Is.EqualTo(90f).Within(0.001f));
                Assert.That(rig.OrbitPitchDegrees, Is.EqualTo(rig.MaximumPitchDegrees).Within(0.001f));
                Assert.That(cameraObject.transform.position.y, Is.GreaterThan(target.transform.position.y + 5.75f));
                Assert.That(
                    (cameraObject.transform.position - target.transform.position).magnitude,
                    Is.EqualTo(orbitRadius).Within(0.001f));

                rig.SetOrbitPitch(-1000f, true);

                Assert.That(rig.OrbitPitchDegrees, Is.EqualTo(rig.MinimumPitchDegrees).Within(0.001f));
                Assert.That(cameraObject.transform.position.y, Is.GreaterThan(target.transform.position.y));
                Assert.That(cameraObject.transform.position.y, Is.LessThan(target.transform.position.y + 5.75f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(target);
            }
        }

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
                Assert.That(accents.transform.Find("Stage river (visual only)"), Is.Not.Null);

                int chunkCount = 0;
                int treeCount = 0;
                int rockCount = 0;
                foreach (Transform descendant in accents.GetComponentsInChildren<Transform>(true))
                {
                    if (descendant.name.StartsWith("Stage chunk "))
                    {
                        chunkCount++;
                    }
                    else if (descendant.name.StartsWith("Tree "))
                    {
                        treeCount++;
                    }
                    else if (descendant.name.StartsWith("Rock "))
                    {
                        rockCount++;
                    }
                }

                Assert.That(chunkCount, Is.EqualTo(StageLayout.ChunkColumns * StageLayout.ChunkRows));
                Assert.That(treeCount, Is.GreaterThan(0));
                Assert.That(rockCount, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
