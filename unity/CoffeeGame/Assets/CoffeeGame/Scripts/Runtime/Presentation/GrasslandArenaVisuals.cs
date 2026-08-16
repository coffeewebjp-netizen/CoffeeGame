using UnityEngine;
using UnityEngine.Rendering;
using CoffeeGame.World;

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
                material.mainTextureScale = new Vector2(StageLayout.Width / 4f, StageLayout.Depth / 4f);
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
            backdrop.transform.localPosition = new Vector3(0f, 4.2f, StageLayout.MaxZ + 2.4f);
            backdrop.transform.localRotation = Quaternion.identity;
            backdrop.transform.localScale = new Vector3(StageLayout.Width + 8f, 14f, 1f);

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

            Material trunkMaterial = RuntimeMaterialFactory.CreateLit(
                "Grassland tree trunk material",
                new Color(0.34f, 0.19f, 0.1f));
            Material leafMaterial = RuntimeMaterialFactory.CreateLit(
                "Grassland tree canopy material",
                new Color(0.19f, 0.48f, 0.2f));
            Material waterMaterial = RuntimeMaterialFactory.CreateUnlit(
                "Grassland river material",
                new Color(0.18f, 0.58f, 0.74f));

            if (waterMaterial != null && waterMaterial.HasProperty("_Smoothness"))
            {
                waterMaterial.SetFloat("_Smoothness", 0.18f);
            }

            Mesh grassMesh = CreateCrossedGrassMesh();
            Mesh riverMesh = CreateRiverMesh();
            var owner = root.AddComponent<GrasslandDepthAccentOwner>();
            owner.Initialize(
                grassMesh,
                grassMaterial,
                stoneMaterial,
                riverMesh,
                waterMaterial,
                trunkMaterial,
                leafMaterial);

            CreateRiver(root.transform, riverMesh, waterMaterial);
            for (int column = 0; column < StageLayout.ChunkColumns; column++)
            {
                for (int row = 0; row < StageLayout.ChunkRows; row++)
                {
                    CreateChunkDecoration(
                        root.transform,
                        column,
                        row,
                        grassMesh,
                        grassMaterial,
                        stoneMaterial,
                        trunkMaterial,
                        leafMaterial);
                }
            }

            return root;
        }

        private static void CreateChunkDecoration(
            Transform parent,
            int column,
            int row,
            Mesh grassMesh,
            Material grassMaterial,
            Material stoneMaterial,
            Material trunkMaterial,
            Material leafMaterial)
        {
            var chunk = new GameObject($"Stage chunk {column + 1},{row + 1}");
            chunk.transform.SetParent(parent, false);
            Vector3 center = StageLayout.GetChunkCenter(column, row);
            bool centralChunk = Mathf.Abs(center.x) < 10f && Mathf.Abs(center.z) < 5f;

            for (int index = 0; index < 2; index++)
            {
                float xOffset = index == 0 ? -3.2f : 3.1f;
                float zOffset = ((column + row + index) & 1) == 0 ? -1.7f : 1.65f;
                CreateGrassClump(
                    chunk.transform,
                    center + new Vector3(xOffset, 0.015f, zOffset),
                    grassMesh,
                    grassMaterial,
                    column * 7 + row * 3 + index);
            }

            bool perimeterChunk = column == 0 || column == StageLayout.ChunkColumns - 1 ||
                row == 0 || row == StageLayout.ChunkRows - 1;
            if (perimeterChunk)
            {
                float treeX = column % 2 == 0 ? -3.25f : 3.2f;
                float treeZ = row % 2 == 0 ? -1.45f : 1.55f;
                CreateTree(
                    chunk.transform,
                    center + new Vector3(treeX, 0f, treeZ),
                    0.72f + ((column + row) % 3) * 0.08f,
                    trunkMaterial,
                    leafMaterial,
                    $"Tree {column + 1},{row + 1} A");
                if ((column + row) % 2 == 0)
                {
                    CreateTree(
                        chunk.transform,
                        center + new Vector3(-treeX * 0.72f, 0f, -treeZ * 0.9f),
                        0.6f,
                        trunkMaterial,
                        leafMaterial,
                        $"Tree {column + 1},{row + 1} B");
                }
            }

            if (!centralChunk)
            {
                CreateRock(
                    chunk.transform,
                    center + new Vector3(((column + row) % 2 == 0 ? 2.1f : -2.2f), 0.07f, 0.25f),
                    0.85f + ((column * 3 + row) % 3) * 0.12f,
                    stoneMaterial,
                    $"Rock {column + 1},{row + 1}");
            }
        }

        private static void CreateGrassClump(
            Transform parent,
            Vector3 position,
            Mesh grassMesh,
            Material grassMaterial,
            int index)
        {
            var clump = new GameObject($"Grass clump {index + 1}");
            clump.transform.SetParent(parent, false);
            clump.transform.localPosition = position;
            clump.transform.localRotation = Quaternion.Euler(0f, index * 31f, index % 2 == 0 ? -3f : 4f);
            float scale = 0.3f + (index % 3) * 0.045f;
            clump.transform.localScale = new Vector3(scale, scale, scale);
            clump.AddComponent<MeshFilter>().sharedMesh = grassMesh;
            MeshRenderer renderer = clump.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = grassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void CreateTree(
            Transform parent,
            Vector3 position,
            float scale,
            Material trunkMaterial,
            Material leafMaterial,
            string objectName)
        {
            var tree = new GameObject(objectName);
            tree.transform.SetParent(parent, false);
            tree.transform.localPosition = position;
            tree.transform.localScale = Vector3.one * scale;

            CreateNoCollisionPrimitive(
                PrimitiveType.Cylinder,
                tree.transform,
                "Trunk",
                new Vector3(0f, 0.48f, 0f),
                new Vector3(0.14f, 0.48f, 0.14f),
                trunkMaterial);
            CreateNoCollisionPrimitive(
                PrimitiveType.Sphere,
                tree.transform,
                "Canopy lower",
                new Vector3(0f, 1.03f, 0f),
                new Vector3(0.72f, 0.55f, 0.72f),
                leafMaterial);
            CreateNoCollisionPrimitive(
                PrimitiveType.Sphere,
                tree.transform,
                "Canopy upper",
                new Vector3(0.12f, 1.38f, 0.02f),
                new Vector3(0.5f, 0.42f, 0.5f),
                leafMaterial);
        }

        private static void CreateRock(
            Transform parent,
            Vector3 position,
            float scale,
            Material material,
            string objectName)
        {
            GameObject rock = CreateNoCollisionPrimitive(
                PrimitiveType.Sphere,
                parent,
                objectName,
                position,
                new Vector3(0.48f, 0.2f, 0.34f) * scale,
                material);
            rock.transform.localRotation = Quaternion.Euler(0f, scale * 37f, scale * 9f);
        }

        private static GameObject CreateNoCollisionPrimitive(
            PrimitiveType primitiveType,
            Transform parent,
            string objectName,
            Vector3 position,
            Vector3 scale,
            Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = objectName;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localScale = scale;
            Collider collider = primitive.GetComponent<Collider>();
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
            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return primitive;
        }

        private static void CreateRiver(Transform parent, Mesh riverMesh, Material waterMaterial)
        {
            var river = new GameObject("Stage river (visual only)");
            river.transform.SetParent(parent, false);
            river.AddComponent<MeshFilter>().sharedMesh = riverMesh;
            MeshRenderer renderer = river.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = waterMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Mesh CreateRiverMesh()
        {
            const int steps = 25;
            const float riverWidth = 1.2f;
            var vertices = new Vector3[steps * 2];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(steps - 1) * 6];
            for (int index = 0; index < steps; index++)
            {
                float progress = index / (float)(steps - 1);
                float z = Mathf.Lerp(StageLayout.MinZ, StageLayout.MaxZ, progress);
                float centerX = -8.4f + Mathf.Sin(progress * Mathf.PI * 2.1f) * 1.45f;
                int vertex = index * 2;
                vertices[vertex] = new Vector3(centerX - riverWidth * 0.5f, 0.008f, z);
                vertices[vertex + 1] = new Vector3(centerX + riverWidth * 0.5f, 0.008f, z);
                uv[vertex] = new Vector2(0f, progress * 4f);
                uv[vertex + 1] = new Vector2(1f, progress * 4f);
                if (index >= steps - 1)
                {
                    continue;
                }

                int triangle = index * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            var mesh = new Mesh { name = "Stage river mesh" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
        private Mesh ownedRiverMesh;
        private Material ownedGrassMaterial;
        private Material ownedStoneMaterial;
        private Material ownedWaterMaterial;
        private Material ownedTrunkMaterial;
        private Material ownedLeafMaterial;

        public void Initialize(
            Mesh mesh,
            Material grassMaterial,
            Material stoneMaterial,
            Mesh riverMesh,
            Material waterMaterial,
            Material trunkMaterial,
            Material leafMaterial)
        {
            ownedMesh = mesh;
            ownedGrassMaterial = grassMaterial;
            ownedStoneMaterial = stoneMaterial;
            ownedRiverMesh = riverMesh;
            ownedWaterMaterial = waterMaterial;
            ownedTrunkMaterial = trunkMaterial;
            ownedLeafMaterial = leafMaterial;
        }

        private void OnDestroy()
        {
            if (ownedMesh != null)
            {
                DestroyOwned(ownedMesh);
            }
            if (ownedRiverMesh != null)
            {
                DestroyOwned(ownedRiverMesh);
            }
            if (ownedGrassMaterial != null)
            {
                DestroyOwned(ownedGrassMaterial);
            }
            if (ownedStoneMaterial != null)
            {
                DestroyOwned(ownedStoneMaterial);
            }
            if (ownedWaterMaterial != null)
            {
                DestroyOwned(ownedWaterMaterial);
            }
            if (ownedTrunkMaterial != null)
            {
                DestroyOwned(ownedTrunkMaterial);
            }
            if (ownedLeafMaterial != null)
            {
                DestroyOwned(ownedLeafMaterial);
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
