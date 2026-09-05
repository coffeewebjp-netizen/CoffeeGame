using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Editor
{
    public static class BuildMeshySnowKimonoWindows
    {
        public static void Build()
        {
            // Preserve the owner's established Windows quality values while the
            // normal build path performs its required project setup.
            GraphicsSettings.lightsUseLinearIntensity = false;
            GraphicsSettings.lightsUseColorTemperature = false;
            QualitySettings.antiAliasing = 2;
            BuildCoffeeGame.BuildWindows();
        }
    }
}
