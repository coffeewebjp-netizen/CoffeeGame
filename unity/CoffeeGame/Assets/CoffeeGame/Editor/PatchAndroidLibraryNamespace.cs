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

            string[] manifests =
            {
                Path.Combine(path, "src", "main", "AndroidManifest.xml"),
                Path.GetFullPath(Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"))
            };
            foreach (string manifestPath in manifests)
            {
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                string manifest = File.ReadAllText(manifestPath);
                string replaced = manifest.Replace(
                    "com.unity3d.player.UnityPlayerGameActivity",
                    "jp.coffeetools.coffeegame.androidlib.CoffeeGameActivity");
                if (replaced != manifest)
                {
                    File.WriteAllText(manifestPath, replaced);
                    Debug.Log("CoffeeGAME: rewrote activity in " + manifestPath);
                }
            }
        }
    }
}
