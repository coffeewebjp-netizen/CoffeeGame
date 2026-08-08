using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    public static class RuntimeMaterialFactory
    {
        private const string LitTemplatePath = "Materials/RuntimeLit";
        private const string UnlitTemplatePath = "Materials/RuntimeUnlit";

        public static Material CreateLit(string materialName, Color color)
        {
            return CreateMaterial(LitTemplatePath, materialName, color,
                "Universal Render Pipeline/Lit", "Standard");
        }

        public static Material CreateUnlit(string materialName, Color color)
        {
            return CreateMaterial(UnlitTemplatePath, materialName, color,
                "Universal Render Pipeline/Unlit", "Sprites/Default", "Unlit/Color");
        }

        /// <summary>
        /// Writes an authored sRGB color without depending on the shader property's
        /// Gamma/HDR flags. Unity 6 only performs the conversion automatically for
        /// flagged properties; URP's ordinary _BaseColor is stored in linear space.
        /// </summary>
        public static void SetSrgbColor(Material material, string propertyName, Color srgbColor)
        {
            if (material == null || string.IsNullOrEmpty(propertyName) || !material.HasProperty(propertyName))
            {
                return;
            }

            Color value = srgbColor;
            if (QualitySettings.activeColorSpace == ColorSpace.Linear &&
                !PropertyPerformsGammaConversion(material.shader, propertyName))
            {
                value = srgbColor.linear;
            }
            material.SetColor(propertyName, value);
        }

        public static Color ToShaderColor(Color srgbColor)
        {
            return QualitySettings.activeColorSpace == ColorSpace.Linear
                ? srgbColor.linear
                : srgbColor;
        }

        private static Material CreateMaterial(
            string resourcePath,
            string materialName,
            Color color,
            params string[] fallbackShaderNames)
        {
            Material template = Resources.Load<Material>(resourcePath);
            Material material = template != null ? new Material(template) : null;

            if (material == null)
            {
                Shader shader = null;
                foreach (string shaderName in fallbackShaderNames)
                {
                    shader = Shader.Find(shaderName);
                    if (shader != null)
                    {
                        break;
                    }
                }

                if (shader == null)
                {
                    Debug.LogError($"No runtime material template or fallback shader is available for {materialName}.");
                    return null;
                }
                material = new Material(shader);
            }

            material.name = materialName;
            if (material.HasProperty("_BaseColor"))
            {
                SetSrgbColor(material, "_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                SetSrgbColor(material, "_Color", color);
            }
            return material;
        }

        private static bool PropertyPerformsGammaConversion(Shader shader, string propertyName)
        {
            if (shader == null)
            {
                return false;
            }

            int index = shader.FindPropertyIndex(propertyName);
            if (index < 0)
            {
                return false;
            }

            ShaderPropertyFlags flags = shader.GetPropertyFlags(index);
            return (flags & (ShaderPropertyFlags.Gamma | ShaderPropertyFlags.HDR)) != 0;
        }
    }
}
