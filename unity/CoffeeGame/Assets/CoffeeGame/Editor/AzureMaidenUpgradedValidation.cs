using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Presentation;
using UnityEditor;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public static class AzureMaidenUpgradedValidation
    {
        private const string ModelPath = "Assets/CoffeeGame/Resources/Models/Hero/AzureMaidenUpgraded/azure-maiden-upgraded.fbx";
        private const string ControllerPath = "Assets/CoffeeGame/Resources/Animations/Hero/AzureMaidenUpgradedRuntime.controller";

        public static void Validate()
        {
            // Validation never runs setup or saves/rebuilds controller assets.
            // Unity's normal import handles the explicitly supplied trial FBX.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || controller == null)
            {
                throw new InvalidOperationException("Upgraded Azure Maiden model or controller did not import.");
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(clip => LeafName(clip.name), StringComparer.OrdinalIgnoreCase);
            string[] required =
            {
                "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
                "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
                "Hurt", "Defeated", "Dodge"
            };
            string[] missing = required.Where(name => !clips.ContainsKey(name)).ToArray();

            GameObject holder = new GameObject("Azure Maiden Unity Validation");
            GameObject instance = UnityEngine.Object.Instantiate(prefab, holder.transform, false);
            SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Where(renderer => renderer.name != "AzureMaidenKatana").ToArray();
            var report = new ValidationReport
            {
                taskId = "ORC-20260905-001",
                workPackage = "WP13",
                inputs = "IN08,IN09,IN10,IN11,IN12",
                output = "OUT20",
                unityVersion = Application.unityVersion,
                importedClipCount = clips.Count,
                controllerClipCount = controller.animationClips.Where(clip => clip != null).Distinct().Count(),
                sourceTakes = ((ModelImporter)AssetImporter.GetAtPath(ModelPath)).defaultClipAnimations
                    .Select(clip => new SourceTake { name = clip.name, takeName = clip.takeName,
                        firstFrame = clip.firstFrame, lastFrame = clip.lastFrame }).ToArray(),
                missingActions = missing,
                meshCount = renderers.Length,
                vertexCount = renderers.Sum(renderer => renderer.sharedMesh != null ? renderer.sharedMesh.vertexCount : 0),
                missingNormalMeshCount = renderers.Count(renderer => renderer.sharedMesh == null || renderer.sharedMesh.normals.Length != renderer.sharedMesh.vertexCount),
                missingMaterialSlotCount = renderers.Sum(renderer => renderer.sharedMaterials.Count(material => material == null)),
                actionSamples = new List<ActionSample>()
            };

            string[] sampled = { "Run", "Jump", "Sword", "MagicCharge", "MagicRelease", "Dodge" };
            foreach (string action in sampled)
            {
                if (clips.TryGetValue(action, out AnimationClip clip))
                {
                    report.actionSamples.Add(Sample(instance, clip, action));
                }
            }

            Camera camera = new GameObject("Azure Maiden Validation Camera").AddComponent<Camera>();
            ModelCharacterVisual visual = holder.AddComponent<ModelCharacterVisual>();
            visual.Initialize(instance.transform, controller, CharacterModelStyle.AzureMaidenUpgraded, camera, 1f, 0f, 180f);
            visual.Animator.Update(0f);
            Renderer[] allRenderers = instance.GetComponentsInChildren<Renderer>(true);
            Material[] materials = allRenderers.SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Distinct().ToArray();
            Material[] bodyMaterials = renderers.SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null).Distinct().ToArray();
            Renderer weapon = allRenderers.FirstOrDefault(renderer => renderer.name == "AzureMaidenKatana");
            Material[] weaponMaterials = weapon != null ? weapon.sharedMaterials : Array.Empty<Material>();
            Texture bodyAtlas = Resources.Load<Texture2D>("Models/Hero/AzureMaidenUpgraded/azure-maiden-base");
            report.runtimeMaterialCount = materials.Length;
            report.urpMaterialCount = materials.Count(material => material.shader != null && material.shader.name.Contains("Universal Render Pipeline"));
            report.atlasMaterialCount = materials.Count(material => material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null);
            report.emissionVariantCount = materials.Count(material => material.IsKeywordEnabled("_EMISSION"));
            report.doubleSidedMaterialCount = materials.Count(material => material.HasProperty("_Cull") && Mathf.Approximately(material.GetFloat("_Cull"), 0f));
            report.bodyMaterialCount = bodyMaterials.Length;
            report.bodyAtlasBindingCount = bodyMaterials.Count(material => material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == bodyAtlas);
            report.weaponMaterialCount = weaponMaterials.Length;
            report.weaponBodyAtlasBindingCount = weaponMaterials.Count(material => material != null &&
                material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") == bodyAtlas);
            report.weaponAttachedToRightHand = IsRigidRightHandSkin(weapon);
            Bounds weaponBounds = EvaluatedWeaponBounds(weapon);
            report.weaponLengthMeters = weapon != null ? weaponBounds.size.magnitude : 0f;
            Transform rightHand = Find(instance.transform, "RightHand");
            report.weaponHandDistanceMeters = weapon != null && rightHand != null
                ? Vector3.Distance(weaponBounds.center, rightHand.position) : float.PositiveInfinity;
            report.weaponWorldPosition = weapon != null ? weapon.transform.position : Vector3.zero;
            report.weaponLocalPosition = weapon != null ? weapon.transform.localPosition : Vector3.zero;
            report.handWorldPosition = rightHand != null ? rightHand.position : Vector3.zero;
            report.weaponHasSteelMaterial = weaponMaterials.Any(material => material != null &&
                material.name.Contains("Steel") && material.HasProperty("_Metallic") && material.GetFloat("_Metallic") > 0.5f);

            report.passed = missing.Length == 0 && report.controllerClipCount == required.Length &&
                report.meshCount > 0 && report.vertexCount > 50000 &&
                report.missingNormalMeshCount == 0 && report.missingMaterialSlotCount == 0 &&
                report.runtimeMaterialCount > 0 && report.urpMaterialCount == report.runtimeMaterialCount &&
                bodyAtlas != null && report.bodyMaterialCount > 0 &&
                report.bodyAtlasBindingCount == report.bodyMaterialCount &&
                bodyMaterials.All(material => material.IsKeywordEnabled("_EMISSION") &&
                    material.HasProperty("_Cull") && Mathf.Approximately(material.GetFloat("_Cull"), 0f)) &&
                report.weaponMaterialCount >= 3 && report.weaponBodyAtlasBindingCount == 0 &&
                report.weaponAttachedToRightHand && report.weaponHasSteelMaterial &&
                report.weaponLengthMeters > 0.75f && report.weaponLengthMeters < 1.5f &&
                report.weaponHandDistanceMeters < 0.85f &&
                report.actionSamples.Count == sampled.Length &&
                report.actionSamples.All(sample => sample.motionMagnitude > 0.015f && sample.rootHorizontalDisplacement < 0.002f &&
                    sample.weaponHandDistanceMeters < 0.85f);

            string reportPath = ReportPath();
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true) + Environment.NewLine);
            Debug.Log("AZURE_MAIDEN_UNITY_VALIDATION=" + JsonUtility.ToJson(report));
            UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(camera.gameObject);
            if (!report.passed)
            {
                throw new InvalidOperationException("Upgraded Azure Maiden validation failed. See " + reportPath);
            }
        }

        private static ActionSample Sample(GameObject instance, AnimationClip clip, string action)
        {
            string[] names = { "Hips", "LeftHand", "RightHand", "LeftFoot", "RightFoot", "Head" };
            clip.SampleAnimation(instance, 0f);
            Vector3[] start = names.Select(name => Find(instance.transform, name)?.position ?? Vector3.zero).ToArray();
            Vector3 rootStart = Find(instance.transform, "Hips")?.position ?? Vector3.zero;
            clip.SampleAnimation(instance, Mathf.Max(0.01f, clip.length * 0.5f));
            Vector3[] middle = names.Select(name => Find(instance.transform, name)?.position ?? Vector3.zero).ToArray();
            Renderer weapon = instance.GetComponentsInChildren<Renderer>(true).FirstOrDefault(renderer => renderer.name == "AzureMaidenKatana");
            Transform hand = Find(instance.transform, "RightHand");
            float weaponDistance = weapon != null && hand != null ? Vector3.Distance(EvaluatedWeaponBounds(weapon).center, hand.position) : float.PositiveInfinity;
            clip.SampleAnimation(instance, Mathf.Max(0.01f, clip.length - 0.001f));
            Vector3 rootEnd = Find(instance.transform, "Hips")?.position ?? Vector3.zero;
            float motionMagnitude = 0f;
            for (int i = 0; i < start.Length; i++)
            {
                motionMagnitude += Vector3.Distance(start[i], middle[i]);
            }
            return new ActionSample
            {
                action = action,
                clipLengthSeconds = clip.length,
                motionMagnitude = motionMagnitude,
                weaponHandDistanceMeters = weaponDistance,
                rootHorizontalDisplacement = Vector2.Distance(
                    new Vector2(rootStart.x, rootStart.z),
                    new Vector2(rootEnd.x, rootEnd.z))
            };
        }

        private static bool IsRigidRightHandSkin(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skin) || skin.sharedMesh == null) return false;
            BoneWeight[] weights = skin.sharedMesh.boneWeights;
            return weights.Length == skin.sharedMesh.vertexCount && weights.Length > 0 &&
                weights.All(weight => Mathf.Approximately(weight.weight0, 1f) &&
                    weight.weight1 == 0f && weight.weight2 == 0f && weight.weight3 == 0f &&
                    weight.boneIndex0 < skin.bones.Length && skin.bones[weight.boneIndex0].name == "RightHand");
        }

        // Evaluate the small rigid prop directly, avoiding a renderer bounds
        // envelope accumulated across animation clips or FBX scale compensation.
        private static Bounds EvaluatedWeaponBounds(Renderer renderer)
        {
            if (!(renderer is SkinnedMeshRenderer skin) || !IsRigidRightHandSkin(renderer))
                return renderer != null ? renderer.bounds : default;
            Mesh mesh = skin.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            BoneWeight[] weights = mesh.boneWeights;
            Matrix4x4[] bindPoses = mesh.bindposes;
            Bounds bounds = default;
            for (int index = 0; index < vertices.Length; index++)
            {
                int bone = weights[index].boneIndex0;
                Vector3 point = (skin.bones[bone].localToWorldMatrix * bindPoses[bone]).MultiplyPoint3x4(vertices[index]);
                if (index == 0) bounds = new Bounds(point, Vector3.zero); else bounds.Encapsulate(point);
            }
            return bounds;
        }

        private static Transform Find(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string LeafName(string name)
        {
            int separator = Math.Max(name.LastIndexOf('|'), name.LastIndexOf('/'));
            return separator >= 0 && separator + 1 < name.Length ? name.Substring(separator + 1) : name;
        }

        private static string ReportPath()
        {
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            return Path.Combine(repository, "art", "3d", "trials", "azure-maiden-upgraded", "manifests", "unity-validation.json");
        }

        [Serializable]
        private sealed class ValidationReport
        {
            public string taskId;
            public string workPackage;
            public string inputs;
            public string output;
            public string unityVersion;
            public int importedClipCount;
            public int controllerClipCount;
            public SourceTake[] sourceTakes;
            public string[] missingActions;
            public int meshCount;
            public int vertexCount;
            public int missingNormalMeshCount;
            public int missingMaterialSlotCount;
            public int runtimeMaterialCount;
            public int urpMaterialCount;
            public int atlasMaterialCount;
            public int emissionVariantCount;
            public int doubleSidedMaterialCount;
            public int bodyMaterialCount;
            public int bodyAtlasBindingCount;
            public int weaponMaterialCount;
            public int weaponBodyAtlasBindingCount;
            public bool weaponAttachedToRightHand;
            public bool weaponHasSteelMaterial;
            public float weaponLengthMeters;
            public float weaponHandDistanceMeters;
            public Vector3 weaponWorldPosition;
            public Vector3 weaponLocalPosition;
            public Vector3 handWorldPosition;
            public List<ActionSample> actionSamples;
            public bool passed;
        }

        [Serializable]
        private sealed class SourceTake
        {
            public string name;
            public string takeName;
            public float firstFrame;
            public float lastFrame;
        }

        [Serializable]
        private sealed class ActionSample
        {
            public string action;
            public float clipLengthSeconds;
            public float motionMagnitude;
            public float weaponHandDistanceMeters;
            public float rootHorizontalDisplacement;
        }
    }
}
