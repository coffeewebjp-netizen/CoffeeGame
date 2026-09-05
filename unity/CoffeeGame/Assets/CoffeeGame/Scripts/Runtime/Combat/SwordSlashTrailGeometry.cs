using UnityEngine;

namespace CoffeeGame.Combat
{
    /// <summary>
    /// Builds the open billboard-space crescent used by the sword swing VFX.
    /// Damage remains governed by CombatArcPolicy; this geometry is an action
    /// trail that communicates direction and reach without drawing a static
    /// hit-sector outline around the player.
    /// </summary>
    public static class SwordSlashTrailGeometry
    {
        public const int DefaultSegments = 28;

        public static Vector3[] Build(float radius, bool mirrored, int segments = DefaultSegments)
        {
            if (radius <= 0f)
            {
                return System.Array.Empty<Vector3>();
            }

            int clampedSegments = Mathf.Max(2, segments);
            var points = new Vector3[clampedSegments + 1];
            float mirror = mirrored ? -1f : 1f;
            for (int index = 0; index < points.Length; index++)
            {
                float t = index / (float)clampedSegments;
                float angle = Mathf.Lerp(122f, -24f, t) * Mathf.Deg2Rad;
                float x = Mathf.Cos(angle) * 0.96f * radius * mirror;
                float y = Mathf.Sin(angle) * 0.88f * radius;
                points[index] = new Vector3(x, y, 0f);
            }

            return points;
        }

        public static Mesh BuildRibbon(Vector3[] path, float maxWidth)
        {
            if (path == null || path.Length < 2 || maxWidth <= 0f)
            {
                return null;
            }

            int count = path.Length;
            var vertices = new Vector3[count * 2];
            var colors = new Color[count * 2];
            var uv = new Vector2[count * 2];
            var triangles = new int[(count - 1) * 6];
            for (int index = 0; index < count; index++)
            {
                float t = index / (float)(count - 1);
                Vector3 tangent = index < count - 1
                    ? path[index + 1] - path[index]
                    : path[index] - path[index - 1];
                if (tangent.sqrMagnitude < 0.0000001f)
                {
                    tangent = Vector3.right;
                }

                Vector3 side = Vector3.Cross(tangent.normalized, Vector3.forward).normalized;
                float width = maxWidth * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.65f) * (1f - t * 0.62f);
                vertices[index * 2] = path[index] + side * width;
                vertices[index * 2 + 1] = path[index] - side * width * 0.28f;
                colors[index * 2] = new Color(1f, 1f, 1f, 0.12f);
                colors[index * 2 + 1] = Color.white;
                uv[index * 2] = new Vector2(t, 0f);
                uv[index * 2 + 1] = new Vector2(t, 1f);
                if (index >= count - 1)
                {
                    continue;
                }

                int triangle = index * 6;
                int vertex = index * 2;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex + 1;
                triangles[triangle + 4] = vertex + 2;
                triangles[triangle + 5] = vertex + 3;
            }

            var mesh = new Mesh { name = "Sword slash ribbon" };
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
