using UnityEditor;

namespace CoffeeGame.Editor
{
    public static class BuildMeshyTrialWindows
    {
        public static void Build()
        {
            CoffeeGameProjectSetup.SetupTrialAnimeGirl();
            BuildCoffeeGame.BuildWindows();
        }
    }
}
