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

        public static GameObject CreateDepthAccents(Transform parent)
        {
            var root = new GameObject("Grassland depth accents");
            root.transform.SetParent(parent, false);

            Material grassMaterial = RuntimeMaterialFactory.CreateUnlit(
                "Grassland blade material",
                new Color(0.34f, 0.58f, 0.22f));
            Material stoneMaterial = RuntimeMaterialFactory.CreateLit(
                "Grassland stone material",
                new Color(0.62f, 0.64f, 0.55f));
            if (stoneMaterial != null && stoneMaterial.HasProperty("_Smoothness"))
            {
                stoneMaterial.SetFloat("_Smoothness", 0.12f);
            }

            Mesh grassMesh = CreateCrossedGrassMesh();
            var owner = root.AddComponent<GrasslandDepthAccentOwner>();
            owner.Initialize(grassMesh, grassMaterial, stoneMaterial);

            Vector3[] clumpPositions =
            {
                new Vector3(-4.45f, 0.015f, -2.38f),
                new Vector3(-3.55f, 0.015f, 2.48f),
                new Vector3(-0.55f, 0.015f, 2.62f),
                new Vector3(3.72f, 0.015f, 2.42f),
                new Vector3(4.5f, 0.015f, -2.3f),
                new Vector3(1.8f, 0.015f, -2.62f)
            };
            for (int index = 0; index < clumpPositions.Length; index++)
            {
                var clump = new GameObject($"Foreground grass clump {index + 1}");
                clump.transform.SetParent(root.transform, false);
                clump.transform.localPosition = clumpPositions[index];
                clump.transform.localRotation = Quaternion.Euler(0f, index * 31f, index % 2 == 0 ? -3f : 4f);
                float scale = 0.3f + (index % 3) * 0.045f;
                clump.transform.localScale = new Vector3(scale, scale, scale);
                clump.AddComponent<MeshFilter>().sharedMesh = grassMesh;
                MeshRenderer renderer = clump.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = grassMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            Vector3[] rockPositions =
            {
                new Vector3(-4.25f, 0.07f, 2.32f),
                new Vector3(4.18f, 0.06f, 2.5f),
                new Vector3(4.38f, 0.065f, -2.12f)
            };
            for (int index = 0; index < rockPositions.Length; index++)
            {
                GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Grassland stone {index + 1}";
                rock.transform.SetParent(root.transform, false);
                rock.transform.localPosition = rockPositions[index];
                rock.transform.localScale = new Vector3(
                    0.34f + index * 0.04f,
                    0.13f + index * 0.015f,
                    0.24f + index * 0.025f);
                Collider collider = rock.GetComponent<Collider>();
                if (collider != null)
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(collider);
                    }
                    else
                    {
                        Object.DestroyImmediate(collider);
                    }
                }
                Renderer renderer = rock.GetComponent<Renderer>();
                renderer.sharedMaterial = stoneMaterial;
            }

            return root;
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

        private static Mesh CreateCrossedGrassMesh()
        {
            var mesh = new Mesh { name = "Crossed grass blade mesh" };
            mesh.vertices = new[]
            {
                new Vector3(-0.3f, 0f, 0f), new Vector3(-0.12f, 0f, 0f), new Vector3(-0.18f, 0.44f, 0f),
                new Vector3(-0.1f, 0f, 0f), new Vector3(0.1f, 0f, 0f), new Vector3(0.02f, 0.72f, 0f),
                new Vector3(0.12f, 0f, 0f), new Vector3(0.31f, 0f, 0f), new Vector3(0.2f, 0.52f, 0f),
                new Vector3(0f, 0f, -0.3f), new Vector3(0f, 0f, -0.12f), new Vector3(0f, 0.44f, -0.18f),
                new Vector3(0f, 0f, -0.1f), new Vector3(0f, 0f, 0.1f), new Vector3(0f, 0.72f, 0.02f),
                new Vector3(0f, 0f, 0.12f), new Vector3(0f, 0f, 0.31f), new Vector3(0f, 0.52f, 0.2f)
            };
            mesh.triangles = new[]
            {
                0, 1, 2, 2, 1, 0,
                3, 4, 5, 5, 4, 3,
                6, 7, 8, 8, 7, 6,
                9, 10, 11, 11, 10, 9,
                12, 13, 14, 14, 13, 12,
                15, 16, 17, 17, 16, 15
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    [DisallowMultipleComponent]
    public sealed class GrasslandDepthAccentOwner : MonoBehaviour
    {
        private Mesh ownedMesh;
        private Material ownedGrassMaterial;
        private Material ownedStoneMaterial;

        public void Initialize(Mesh mesh, Material grassMaterial, Material stoneMaterial)
        {
            ownedMesh = mesh;
            ownedGrassMaterial = grassMaterial;
            ownedStoneMaterial = stoneMaterial;
        }

        private void OnDestroy()
        {
            if (ownedMesh != null)
            {
                DestroyOwned(ownedMesh);
            }
            if (ownedGrassMaterial != null)
            {
                DestroyOwned(ownedGrassMaterial);
            }
            if (ownedStoneMaterial != null)
            {
                DestroyOwned(ownedStoneMaterial);
            }
        }

        private static void DestroyOwned(Object ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(ownedObject);
            }
            else
            {
                DestroyImmediate(ownedObject);
            }
        }
    }
}
