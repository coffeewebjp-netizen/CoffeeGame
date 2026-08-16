using System.IO;
using UnityEditor.Android;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public sealed class PatchAndroidLibraryNamespace : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string gradle = Path.Combine(path, "CoffeeGameAndroid.androidlib", "build.gradle");
            if (!File.Exists(gradle))
            {
                return;
            }

            string text = File.ReadAllText(gradle);
            string patched = text.Replace(
                "namespace \"jp.coffeetools.coffeegame\"",
                "namespace \"jp.coffeetools.coffeegame.androidlib\"");
            if (patched != text)
            {
                File.WriteAllText(gradle, patched);
                Debug.Log("CoffeeGAME: set androidlib namespace to jp.coffeetools.coffeegame.androidlib");
            }

            // Do not rewrite the Unity launcher activity. A custom GameActivity
            // subclass is not packaged into the player DEX and will fail to start.
        }
    }
}
