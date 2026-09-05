using UnityEditor;

namespace CoffeeGame.Editor
{
    public static class BuildSnowKimonoTrialWindows
    {
        [MenuItem("CoffeeGAME/Build/Windows snow-kimono trial", priority = 22)]
        public static void Build()
        {
            BuildCoffeeGame.BuildSnowKimonoTrialWindows();
        }
    }
}
