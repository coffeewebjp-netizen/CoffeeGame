using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    public static class GrasslandArenaVisuals
    {
        public const string GroundTextureResource = "Art/Environment/Grassland/grass-ground";
        public const string BackdropTextureResource = "Art/Environment/Grassland/grassland-backdrop";

        public static Material CreateGroundMaterial()
        {
            Material material = RuntimeMaterialFactory.CreateLit(
                "Grassland ground",
                new Color(0.72f, 0.78f, 0.66f));
            if (material == null)
            {
                return null;
            }

            Texture2D texture = Resources.Load<Texture2D>(GroundTextureResource);
            if (texture == null)
            {
                Debug.LogWarning($"Grassland ground texture is missing at Resources/{GroundTextureResource}.");
            }
            else
            {
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(3.6f, 2.4f);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.04f);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }
            return material;
        }

        public static GameObject CreateBackdrop(Transform parent)
        {
            var backdrop = new GameObject("Grassland distant backdrop");
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.localPosition = new Vector3(0f, 1.45f, 4.35f);
            backdrop.transform.localRotation = Quaternion.identity;
            backdrop.transform.localScale = new Vector3(12.6f, 7.1f, 1f);

            MeshFilter meshFilter = backdrop.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateBackdropMesh();

            MeshRenderer renderer = backdrop.AddComponent<MeshRenderer>();
            Material material = RuntimeMaterialFactory.CreateUnlit("Grassland distant backdrop", Color.white);
            Texture2D texture = Resources.Load<Texture2D>(BackdropTextureResource);
            if (texture == null)
            {
                Debug.LogWarning($"Grassland backdrop is missing at Resources/{BackdropTextureResource}.");
            }
            else if (material != null)
            {
                material.mainTexture = texture;
            }

            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return backdrop;
        }

        private static Mesh CreateBackdropMesh()
        {
            var mesh = new Mesh
            {
                name = "Grassland backdrop quad"
            };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
