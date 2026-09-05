using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Editor
{
    public static class MeshySnowKimonoValidation
    {
        private const string ModelPath = "Assets/CoffeeGame/Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono.fbx";
        private const string ControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/MeshySnowKimonoRuntime.controller";

        public static void Validate()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            CoffeeGameProjectSetup.SetupMeshySnowKimono();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || controller == null)
            {
                throw new InvalidOperationException("Meshy Snow Kimono model or controller did not import.");
            }

            var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    clips[LeafName(clip.name)] = clip;
                }
            }

            string[] required =
            {
                "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
                "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
                "Hurt", "Defeated", "Dodge"
            };
            var missing = new List<string>();
            for (int i = 0; i < required.Length; i++)
            {
                if (!clips.ContainsKey(required[i]))
                {
                    missing.Add(required[i]);
                }
            }

            GameObject holder = new GameObject("Meshy Snow Kimono Unity Validation");
            GameObject instance = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
            var report = new ValidationReport
            {
                unityVersion = Application.unityVersion,
                modelPath = ModelPath,
                controllerPath = ControllerPath,
                importedClipCount = clips.Count,
                missingActions = missing.ToArray(),
                meshCount = 0,
                vertexCount = 0,
                missingNormalMeshCount = 0,
                missingMaterialSlotCount = 0,
                activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                actionSamples = new List<ActionSample>()
            };

            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            report.meshCount = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                Mesh mesh = renderers[i].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                report.vertexCount += mesh.vertexCount;
                if (mesh.normals == null || mesh.normals.Length != mesh.vertexCount)
                {
                    report.missingNormalMeshCount++;
                }
                Material[] materials = renderers[i].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    if (materials[materialIndex] == null)
                    {
                        report.missingMaterialSlotCount++;
                    }
                }
            }

            string[] sampledActions = { "Walk", "Run", "Sword", "Dodge" };
            for (int i = 0; i < sampledActions.Length; i++)
            {
                if (clips.TryGetValue(sampledActions[i], out AnimationClip clip))
                {
                    report.actionSamples.Add(Sample(instance, clip, sampledActions[i]));
                }
            }

            Camera camera = CreateCamera();
            var visual = holder.AddComponent<ModelCharacterVisual>();
            visual.Initialize(instance.transform, controller, CharacterModelStyle.MeshySnowKimono, camera, 1f, 0f, 180f);
            report.runtimeMaterialCount = CountRuntimeMaterials(renderers);
            report.visiblePaletteRoleCount = CountVisiblePaletteRoles(renderers);
            report.materialSamples = CollectMaterialSamples(renderers);
            report.atlasMaterialCount = report.materialSamples.Count(sample => sample.baseMap != "<none>");
            report.normalMappedMaterialCount = report.materialSamples.Count(sample => sample.normalMap != "<none>" && sample.normalKeyword);
            report.packedMaterialCount = report.materialSamples.Count(sample =>
                sample.metallicMap != "<none>");
            report.normalSamples = CollectNormalSamples(renderers);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                RenderPreview(camera, instance, UnlitPreviewPath());
                report.previewPath = RelativeToRepository(UnlitPreviewPath());
            }
            else
            {
                report.previewPath = string.Empty;
            }
            report.passed = report.missingActions.Length == 0 &&
                report.meshCount >= 11 && report.vertexCount > 100000 &&
                report.missingNormalMeshCount == 0 && report.missingMaterialSlotCount == 0 &&
                report.runtimeMaterialCount >= 6 && report.atlasMaterialCount >= 1 &&
                report.normalMappedMaterialCount >= 1 && report.packedMaterialCount >= 1 &&
                report.actionSamples.Count == sampledActions.Length;
            for (int i = 0; i < report.actionSamples.Count; i++)
            {
                ActionSample sample = report.actionSamples[i];
                report.passed &= sample.motionMagnitude > 0.015f;
                if (sample.action == "Walk" || sample.action == "Run")
                {
                    report.passed &= sample.rootDisplacement < 0.002f;
                }
            }

            string reportPath = ReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + Environment.NewLine);
            Debug.Log("MESHY_SNOW_KIMONO_UNITY_VALIDATION=" + JsonUtility.ToJson(report));

            UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            if (!report.passed)
            {
                throw new InvalidOperationException("Meshy Snow Kimono Unity validation failed. See " + reportPath);
            }
        }

        private static ActionSample Sample(GameObject instance, AnimationClip clip, string action)
        {
            string[] names = { "Pelvis", "Hand.R", "Foot.L", "Foot.R", "Weapon", "Sheath" };
            clip.SampleAnimation(instance, 0f);
            Vector3[] start = Capture(instance.transform, names);
            Transform root = Find(instance.transform, "Root");
            Vector3 rootStart = root != null ? root.localPosition : Vector3.zero;
            clip.SampleAnimation(instance, Mathf.Max(0.01f, clip.length * 0.5f));
            Vector3[] middle = Capture(instance.transform, names);
            clip.SampleAnimation(instance, Mathf.Max(0.01f, clip.length - 0.001f));
            Vector3 rootEnd = root != null ? root.localPosition : Vector3.zero;
            float magnitude = 0f;
            for (int i = 0; i < start.Length; i++)
            {
                magnitude += Vector3.Distance(start[i], middle[i]);
            }
            return new ActionSample
            {
                action = action,
                clipLengthSeconds = clip.length,
                motionMagnitude = magnitude,
                rootDisplacement = Vector3.Distance(rootStart, rootEnd)
            };
        }

        private static Vector3[] Capture(Transform root, string[] names)
        {
            var positions = new Vector3[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                Transform found = Find(root, names[i]);
                positions[i] = found != null ? found.position : Vector3.zero;
            }
            return positions;
        }

        private static Transform Find(Transform root, string name)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }
            return null;
        }

        private static int CountRuntimeMaterials(Renderer[] renderers)
        {
            var materials = new HashSet<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] slots = renderers[i].sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] != null && slots[slot].shader != null &&
                        slots[slot].shader.name.IndexOf("Universal Render Pipeline", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        materials.Add(slots[slot]);
                    }
                }
            }
            return materials.Count;
        }

        private static int CountVisiblePaletteRoles(Renderer[] renderers)
        {
            bool skin = false;
            bool hair = false;
            bool kimono = false;
            bool trim = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] slots = renderers[i].sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    Material material = slots[slot];
                    if (material == null)
                    {
                        continue;
                    }
                    Color color = material.HasProperty("_BaseColor")
                        ? material.GetColor("_BaseColor")
                        : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.black;
                    string name = material.name.ToLowerInvariant();
                    if (name.Contains("skin") && color.maxColorComponent > 0.45f) skin = true;
                    if (name.Contains("hair") && color.b > color.r * 1.4f) hair = true;
                    if (name.Contains("kimono") && color.maxColorComponent > 0.005f) kimono = true;
                    if (name.Contains("trim") && color.r > color.g * 2f) trim = true;
                }
            }
            return (skin ? 1 : 0) + (hair ? 1 : 0) + (kimono ? 1 : 0) + (trim ? 1 : 0);
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Meshy Snow Kimono Validation Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.11f, 1f);
            camera.fieldOfView = 34f;
            camera.transform.position = new Vector3(1.85f, 1.58f, -3.55f);
            camera.transform.LookAt(new Vector3(0f, 0.84f, 0f));

            CreateLight(cameraObject.transform, "Meshy Snow Kimono Key", new Vector3(-2f, 3.2f, -2.5f), 1.6f, new Color(1f, 0.83f, 0.76f));
            CreateLight(cameraObject.transform, "Meshy Snow Kimono Rim", new Vector3(1.8f, 2.6f, 2.4f), 1.2f, new Color(0.45f, 0.72f, 1f));
            return camera;
        }

        private static void CreateLight(Transform parent, string name, Vector3 position, float intensity, Color color)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, true);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
            light.transform.position = position;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0.9f, 0f) - position, Vector3.up);
        }

        private static List<MaterialSample> CollectMaterialSamples(Renderer[] renderers)
        {
            var result = new List<MaterialSample>();
            var seen = new HashSet<Material>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] slots = renderers[i].sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    Material material = slots[slot];
                    if (material == null || !seen.Add(material)) continue;
                    Color baseColor = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.black;
                    Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
                    Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    Texture normalMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                    Texture metallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
                    Texture occlusionMap = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
                    result.Add(new MaterialSample
                    {
                        name = material.name,
                        shader = material.shader != null ? material.shader.name : string.Empty,
                        baseColorLinear = baseColor,
                        baseColorSrgb = QualitySettings.activeColorSpace == ColorSpace.Linear ? baseColor.gamma : baseColor,
                        emissionLinear = emission,
                        baseMap = baseMap != null ? baseMap.name : "<none>",
                        normalMap = normalMap != null ? normalMap.name : "<none>",
                        metallicMap = metallicMap != null ? metallicMap.name : "<none>",
                        occlusionMap = occlusionMap != null ? occlusionMap.name : "<none>",
                        normalKeyword = material.IsKeywordEnabled("_NORMALMAP")
                    });
                }
            }
            return result;
        }

        private static List<NormalSample> CollectNormalSamples(SkinnedMeshRenderer[] renderers)
        {
            var result = new List<NormalSample>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Mesh mesh = renderers[i].sharedMesh;
                if (mesh == null || mesh.vertexCount == 0 || mesh.normals.Length != mesh.vertexCount) continue;
                Vector3 center = mesh.bounds.center;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                int outward = 0;
                int sampled = 0;
                int stride = Mathf.Max(1, mesh.vertexCount / 2048);
                for (int vertex = 0; vertex < mesh.vertexCount; vertex += stride)
                {
                    Vector3 radial = vertices[vertex] - center;
                    if (radial.sqrMagnitude < 0.000001f) continue;
                    if (Vector3.Dot(radial.normalized, normals[vertex].normalized) > 0f) outward++;
                    sampled++;
                }
                result.Add(new NormalSample
                {
                    renderer = renderers[i].name,
                    sampledVertexCount = sampled,
                    outwardRatio = sampled > 0 ? (float)outward / sampled : 0f
                });
            }
            return result;
        }

        private static void RenderUnlitDiagnostic(Camera camera, Renderer[] renderers, string outputPath)
        {
            var originals = new List<Material[]>();
            var diagnosticMaterials = new List<Material>();
            try
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material[] source = renderers[i].sharedMaterials;
                    originals.Add(source);
                    var replacements = new Material[source.Length];
                    for (int slot = 0; slot < source.Length; slot++)
                    {
                        Color linear = source[slot] != null && source[slot].HasProperty("_BaseColor")
                            ? source[slot].GetColor("_BaseColor") : Color.magenta;
                        Color srgb = QualitySettings.activeColorSpace == ColorSpace.Linear ? linear.gamma : linear;
                        Material diagnostic = RuntimeMaterialFactory.CreateUnlit("Snow unlit palette diagnostic", srgb);
                        replacements[slot] = diagnostic;
                        if (diagnostic != null) diagnosticMaterials.Add(diagnostic);
                    }
                    renderers[i].sharedMaterials = replacements;
                }
                RenderPreview(camera, renderers[0].transform.root.gameObject, outputPath);
            }
            finally
            {
                for (int i = 0; i < renderers.Length && i < originals.Count; i++)
                {
                    renderers[i].sharedMaterials = originals[i];
                }
                for (int i = 0; i < diagnosticMaterials.Count; i++) UnityEngine.Object.DestroyImmediate(diagnosticMaterials[i]);
            }
        }

        private static void RenderPreview(Camera camera, GameObject model, string outputPath)
        {
            AnimationClip idle = null;
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ModelPath);
            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is AnimationClip clip && string.Equals(LeafName(clip.name), "Idle", StringComparison.OrdinalIgnoreCase))
                {
                    idle = clip;
                    break;
                }
            }
            idle?.SampleAnimation(model, 0f);

            var target = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static string LeafName(string name)
        {
            int separator = Math.Max(name.LastIndexOf('|'), name.LastIndexOf('/'));
            return separator >= 0 && separator + 1 < name.Length ? name.Substring(separator + 1) : name;
        }

        private static string RepositoryRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        private static string UnlitPreviewPath()
        {
            return Path.Combine(RepositoryRoot(), "art", "3d", "trials", "meshy-snow-kimono", "previews", "unity-runtime-atlas.png");
        }

        private static string ReportPath()
        {
            return Path.Combine(RepositoryRoot(), "art", "3d", "trials", "meshy-snow-kimono", "manifests", "meshy-snow-kimono-unity-validation.json");
        }

        private static string RelativeToRepository(string path)
        {
            return Path.GetRelativePath(RepositoryRoot(), path).Replace('\\', '/');
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string unityVersion;
            public string modelPath;
            public string controllerPath;
            public int importedClipCount;
            public string[] missingActions;
            public int meshCount;
            public int vertexCount;
            public int missingNormalMeshCount;
            public int missingMaterialSlotCount;
            public string activeColorSpace;
            public int runtimeMaterialCount;
            public int visiblePaletteRoleCount;
            public int atlasMaterialCount;
            public int normalMappedMaterialCount;
            public int packedMaterialCount;
            public List<MaterialSample> materialSamples;
            public List<NormalSample> normalSamples;
            public List<ActionSample> actionSamples;
            public string previewPath;
            public bool passed;
        }

        [Serializable]
        private sealed class MaterialSample
        {
            public string name;
            public string shader;
            public Color baseColorLinear;
            public Color baseColorSrgb;
            public Color emissionLinear;
            public string baseMap;
            public string normalMap;
            public string metallicMap;
            public string occlusionMap;
            public bool normalKeyword;
        }

        [Serializable]
        private sealed class NormalSample
        {
            public string renderer;
            public int sampledVertexCount;
            public float outwardRatio;
        }

        [Serializable]
        private sealed class ActionSample
        {
            public string action;
            public float clipLengthSeconds;
            public float motionMagnitude;
            public float rootDisplacement;
        }
    }
}
