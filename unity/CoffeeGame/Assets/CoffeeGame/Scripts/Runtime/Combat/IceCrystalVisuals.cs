using CoffeeGame.Presentation;
using UnityEngine;

namespace CoffeeGame.Combat
{
    public static class IceCrystalVisuals
    {
        public static readonly Color Frost = new Color(0.78f, 0.93f, 1f, 1f);
        public static readonly Color Core = new Color(0.93f, 0.98f, 1f, 1f);

        public static Mesh CreateOctahedron()
        {
            var mesh = new Mesh { name = "Ice octahedron" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(0f, -1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(0f, 0f, -1f)
            };
            mesh.triangles = new[]
            {
                0, 2, 4, 0, 4, 3, 0, 3, 5, 0, 5, 2,
                1, 4, 2, 1, 3, 4, 1, 5, 3, 1, 2, 5
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static GameObject CreateCrystal(
            Transform parent,
            string objectName,
            Vector3 localScale,
            Color color,
            out Material ownedMaterial)
        {
            var crystal = new GameObject(objectName);
            crystal.transform.SetParent(parent, false);
            crystal.transform.localScale = localScale;
            MeshFilter filter = crystal.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateOctahedron();
            MeshRenderer renderer = crystal.AddComponent<MeshRenderer>();
            ownedMaterial = RuntimeMaterialFactory.CreateUnlit($"{objectName} material", color);
            if (ownedMaterial != null)
            {
                renderer.sharedMaterial = ownedMaterial;
            }

            return crystal;
        }
    }
}
