using UnityEngine;

namespace CoffeeGame.World
{
    public static class StageLayout
    {
        public const int ChunkColumns = 4;
        public const int ChunkRows = 4;

        public const float MinX = -19.2f;
        public const float MaxX = 19.2f;
        public const float MinZ = -10.8f;
        public const float MaxZ = 10.8f;

        public const float ActorMinX = -18.55f;
        public const float ActorMaxX = 18.55f;
        public const float ActorMinZ = -10.15f;
        public const float ActorMaxZ = 10.15f;

        // Keep the orthographic view over the playable ground at the edges.
        public const float CameraMinX = -14.4f;
        public const float CameraMaxX = 14.4f;
        public const float CameraMinZ = -7.55f;
        public const float CameraMaxZ = 7.55f;

        public const float Width = MaxX - MinX;
        public const float Depth = MaxZ - MinZ;
        public const float ChunkWidth = Width / ChunkColumns;
        public const float ChunkDepth = Depth / ChunkRows;

        public static Vector3 GetChunkCenter(int column, int row)
        {
            column = Mathf.Clamp(column, 0, ChunkColumns - 1);
            row = Mathf.Clamp(row, 0, ChunkRows - 1);
            return new Vector3(
                MinX + ChunkWidth * (column + 0.5f),
                0f,
                MinZ + ChunkDepth * (row + 0.5f));
        }

        public static Vector3 ClampActorPosition(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, ActorMinX, ActorMaxX);
            position.z = Mathf.Clamp(position.z, ActorMinZ, ActorMaxZ);
            return position;
        }

        public static Vector3 ClampCameraTarget(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, CameraMinX, CameraMaxX);
            position.z = Mathf.Clamp(position.z, CameraMinZ, CameraMaxZ);
            return position;
        }
    }
}
