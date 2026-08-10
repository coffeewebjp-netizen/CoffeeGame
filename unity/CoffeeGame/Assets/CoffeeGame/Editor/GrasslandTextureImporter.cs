using UnityEditor;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public sealed class GrasslandTextureImporter : AssetPostprocessor
    {
        private const string GroundAssetPath =
            "Assets/CoffeeGame/Resources/Art/Environment/Grassland/grass-ground.png";
        private const string BackdropAssetPath =
            "Assets/CoffeeGame/Resources/Art/Environment/Grassland/grassland-backdrop.png";

        private void OnPreprocessTexture()
        {
            bool isGround = assetPath == GroundAssetPath;
            bool isBackdrop = assetPath == BackdropAssetPath;
            if (!isGround && !isBackdrop)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = isGround ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled = isGround;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
