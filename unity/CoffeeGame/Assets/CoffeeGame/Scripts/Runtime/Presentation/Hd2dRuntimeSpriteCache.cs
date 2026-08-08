using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    /// <summary>
    /// Runtime-created Sprite objects are shared by every actor using the same
    /// atlas slice. Leases prevent five slimes from allocating five identical
    /// sprite libraries, while still releasing the generated objects safely when
    /// the last visual disappears.
    /// </summary>
    internal static class Hd2dRuntimeSpriteCache
    {
        private sealed class SpriteEntry
        {
            public Sprite Sprite;
            public int LeaseCount;
            public string TextureResourcePath;
        }

        private static readonly Dictionary<string, Texture2D> Textures =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private static readonly Dictionary<string, SpriteEntry> Sprites =
            new Dictionary<string, SpriteEntry>(StringComparer.Ordinal);

        public static Texture2D LoadTexture(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                return null;
            }

            if (!Textures.TryGetValue(resourcePath, out Texture2D texture) || texture == null)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
                // A missing resource must not leave a permanent negative-cache
                // entry. It may be imported later during the same Editor session.
                if (texture != null)
                {
                    Textures[resourcePath] = texture;
                }
                else
                {
                    Textures.Remove(resourcePath);
                }
            }
            return texture;
        }

        public static Sprite Acquire(
            string sliceKey,
            string textureResourcePath,
            Func<Sprite> factory)
        {
            if (string.IsNullOrWhiteSpace(sliceKey) ||
                string.IsNullOrWhiteSpace(textureResourcePath))
            {
                return null;
            }

            if (Sprites.TryGetValue(sliceKey, out SpriteEntry existing))
            {
                existing.LeaseCount++;
                return existing.Sprite;
            }

            Sprite sprite = factory?.Invoke();
            if (sprite == null)
            {
                return null;
            }

            Sprites[sliceKey] = new SpriteEntry
            {
                Sprite = sprite,
                LeaseCount = 1,
                TextureResourcePath = textureResourcePath
            };
            return sprite;
        }

        public static void Release(string sliceKey)
        {
            if (string.IsNullOrWhiteSpace(sliceKey) ||
                !Sprites.TryGetValue(sliceKey, out SpriteEntry entry))
            {
                return;
            }

            entry.LeaseCount--;
            if (entry.LeaseCount > 0)
            {
                return;
            }

            Sprites.Remove(sliceKey);
            if (entry.Sprite != null)
            {
                UnityEngine.Object.Destroy(entry.Sprite);
            }

            ReleaseTextureWhenUnreferenced(entry.TextureResourcePath);
        }

        /// <summary>
        /// Removes textures loaded by a failed, pre-commit visual build. A
        /// texture remains resident only while at least one cached Sprite entry
        /// refers to it.
        /// </summary>
        public static void ReleaseUnleasedTextures()
        {
            if (Textures.Count == 0)
            {
                return;
            }

            var referencedPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpriteEntry entry in Sprites.Values)
            {
                if (!string.IsNullOrWhiteSpace(entry.TextureResourcePath))
                {
                    referencedPaths.Add(entry.TextureResourcePath);
                }
            }

            var unreferencedPaths = new List<string>();
            foreach (string resourcePath in Textures.Keys)
            {
                if (!referencedPaths.Contains(resourcePath))
                {
                    unreferencedPaths.Add(resourcePath);
                }
            }

            for (int i = 0; i < unreferencedPaths.Count; i++)
            {
                UnloadTexture(unreferencedPaths[i]);
            }
        }

        private static void ReleaseTextureWhenUnreferenced(string resourcePath)
        {
            if (Sprites.Count == 0)
            {
                UnloadAllTextures();
                return;
            }

            foreach (SpriteEntry remaining in Sprites.Values)
            {
                if (string.Equals(
                    remaining.TextureResourcePath,
                    resourcePath,
                    StringComparison.Ordinal))
                {
                    return;
                }
            }

            UnloadTexture(resourcePath);
        }

        private static void UnloadTexture(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) ||
                !Textures.TryGetValue(resourcePath, out Texture2D texture))
            {
                return;
            }

            Textures.Remove(resourcePath);
            if (texture != null)
            {
                Resources.UnloadAsset(texture);
            }
        }

        private static void UnloadAllTextures()
        {
            foreach (Texture2D texture in Textures.Values)
            {
                if (texture != null)
                {
                    Resources.UnloadAsset(texture);
                }
            }
            Textures.Clear();
        }

        // This also runs when Enter Play Mode has domain reload disabled.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            foreach (SpriteEntry entry in Sprites.Values)
            {
                if (entry.Sprite != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(entry.Sprite);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(entry.Sprite);
                    }
                }
            }
            Sprites.Clear();
            UnloadAllTextures();
        }
    }
}
