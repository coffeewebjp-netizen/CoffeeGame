using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class SilverCatMaterialTests
    {
        [Test]
        public void SilverCatPreservesAuthoredMaterialInsteadOfHeroineFallback()
        {
            var host = new GameObject("Silver cat material test");
            var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(host.transform, false);
            var texture = new Texture2D(2, 2);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", texture);
            model.GetComponent<Renderer>().sharedMaterial = material;
            try
            {
                var visual = host.AddComponent<ModelCharacterVisual>();
                visual.Initialize(model.transform, null, CharacterModelStyle.SilverCat, null);
                Assert.That(model.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(texture));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
