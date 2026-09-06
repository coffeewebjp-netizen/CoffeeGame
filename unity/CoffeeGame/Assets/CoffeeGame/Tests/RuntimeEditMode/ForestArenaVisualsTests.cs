using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class ForestArenaVisualsTests
    {
        [Test]
        public void ForestIsVisualOnlyAndDoesNotConsumeGameplayRandom()
        {
            Random.State saved = Random.state;
            var parent = new GameObject("Forest test");
            try
            {
                Random.InitState(932);
                Random.State before = Random.state;
                ForestArenaVisuals forest = ForestArenaVisuals.Create(parent.transform);
                float after = Random.value;
                Random.state = before;
                Assert.That(after, Is.EqualTo(Random.value));
                Assert.That(parent.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(parent.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(forest.TreeCount, Is.GreaterThan(40));
                Assert.That(forest.FernCount, Is.GreaterThan(80));
                Assert.That(forest.TriangleCount, Is.LessThan(500000));
                Assert.That(forest.BatchCount, Is.LessThanOrEqualTo(64));
                foreach (MeshFilter filter in parent.GetComponentsInChildren<MeshFilter>())
                {
                    Assert.That(filter.sharedMesh.bounds.size.sqrMagnitude, Is.GreaterThan(0));
                    foreach (Vector3 p in filter.sharedMesh.vertices)
                        Assert.That(float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude), Is.False);
                }
            }
            finally { Object.DestroyImmediate(parent); Random.state = saved; }
        }

        [Test]
        public void DestroyingForestReleasesOwnedMeshesAndMaterials()
        {
            var parent = new GameObject("Forest lifetime test");
            ForestArenaVisuals forest = ForestArenaVisuals.Create(parent.transform);
            Mesh mesh = forest.GetComponentInChildren<MeshFilter>().sharedMesh;
            Material surface = forest.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Material ground = forest.GroundMaterial;
            Object.DestroyImmediate(parent);
            Assert.That(mesh == null, Is.True);
            Assert.That(surface == null, Is.True);
            Assert.That(ground == null, Is.True);
        }
    }
}
