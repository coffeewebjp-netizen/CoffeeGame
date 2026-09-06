using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoffeeGame.Editor
{
    /// <summary>
    /// Idempotent import setup for the Meshy silver-cat rival. This class owns only
    /// the SilverCat model, material, texture, and controller paths.
    /// </summary>
    public static class SilverCatAssetSetup
    {
        private const string Root = "Assets/CoffeeGame/Resources/";
        public const string ModelPath = Root + "Models/Characters/SilverCat/silver-cat-girl.fbx";
        public const string BaseMapPath = Root + "Models/Characters/SilverCat/silver-cat-basecolor.png";
        public const string NormalMapPath = Root + "Models/Characters/SilverCat/silver-cat-normal.png";
        public const string ControllerPath = Root + "Animations/Characters/SilverCat/SilverCatRuntime.controller";
        private const string MaterialFolder = Root + "Materials/SilverCat";
        private const float ImportScale = 1f;

        private static readonly CharacterAction[] RequiredMotions =
        {
            CharacterAction.Idle,
            CharacterAction.Walk,
            CharacterAction.Run,
            CharacterAction.Jump,
            CharacterAction.Dodge,
            CharacterAction.MagicRelease,
            CharacterAction.Hurt,
            CharacterAction.Defeated
        };

        [MenuItem("CoffeeGAME/Assets/Configure Silver Cat", priority = 70)]
        public static void Configure()
        {
            AssetDatabase.Refresh();
            EnsureFolders();

            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Silver Cat FBX is missing at " + ModelPath);
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.importBlendShapes = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.isReadable = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Optimal;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.globalScale = ImportScale;
            importer.SaveAndReimport();

            ConfigureImportedClips(importer);
            ConfigureTexture(BaseMapPath, TextureImporterType.Default, true);
            ConfigureTexture(NormalMapPath, TextureImporterType.NormalMap, false);
            EnsureMaterialRemaps(importer);
            BuildController();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Root + "Models/Characters/SilverCat");
            Directory.CreateDirectory(Root + "Animations/Characters/SilverCat");
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }

        private static void ConfigureImportedClips(ModelImporter importer)
        {
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            if (takes == null || takes.Length == 0)
            {
                throw new InvalidOperationException("Silver Cat FBX contains no animation takes.");
            }

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ModelImporterClipAnimation take in takes)
            {
                string leaf = AnimationLeaf(take.name);
                string uniqueName = leaf;
                int suffix = 2;
                while (!usedNames.Add(uniqueName))
                {
                    uniqueName = leaf + " " + suffix++;
                }

                take.name = uniqueName;
                bool loop = Matches(uniqueName, "idle", "walk", "run", "running");
                take.loopTime = loop;
                take.loopPose = loop;
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;
                take.keepOriginalOrientation = true;
                take.keepOriginalPositionY = true;
                take.keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = takes;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, TextureImporterType type, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                if (path == BaseMapPath)
                {
                    throw new InvalidOperationException("Silver Cat base map is missing at " + BaseMapPath);
                }
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void EnsureMaterialRemaps(ModelImporter importer)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (prefab == null)
            {
                throw new InvalidOperationException("Silver Cat prefab could not be loaded after import.");
            }

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            Texture2D normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalMapPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (baseMap == null || shader == null)
            {
                throw new InvalidOperationException("Silver Cat URP material inputs are unavailable.");
            }

            Material[] sourceMaterials = prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .GroupBy(material => material.name, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();
            if (sourceMaterials.Length == 0)
            {
                throw new InvalidOperationException("Silver Cat model has no source materials.");
            }

            foreach (Material source in sourceMaterials)
            {
                string safeName = SanitizeFileName(source.name);
                string materialPath = MaterialFolder + "/" + safeName + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = safeName };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else
                {
                    material.shader = shader;
                }

                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", baseMap);
                material.SetFloat("_Metallic", 0.05f);
                material.SetFloat("_Smoothness", 0.36f);
                material.SetFloat("_Cull", 0f);
                if (normalMap != null)
                {
                    material.SetTexture("_BumpMap", normalMap);
                    material.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    material.SetTexture("_BumpMap", null);
                    material.DisableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(material);

                var sourceId = new AssetImporter.SourceAssetIdentifier(typeof(Material), source.name);
                importer.AddRemap(sourceId, material);
            }

            importer.SaveAndReimport();
        }

        private static void BuildController()
        {
            List<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (clips.Count == 0)
            {
                throw new InvalidOperationException("Silver Cat model has no imported animation clips.");
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            controller.name = "SilverCatRuntime";

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in machine.states)
            {
                machine.RemoveState(state.state);
            }

            AnimationClip idle = FindClip(clips, CharacterAction.Idle) ?? clips[0];
            foreach (CharacterAction action in Enum.GetValues(typeof(CharacterAction)))
            {
                AnimationClip clip = FindClip(clips, action) ?? FallbackClip(clips, action, idle);
                AnimatorState state = machine.AddState(ModelCharacterVisual.GetDefaultStateName(action));
                state.motion = clip;
                state.writeDefaultValues = true;
                state.speed = 1f;
                if (action == CharacterAction.Idle)
                {
                    machine.defaultState = state;
                }
            }

            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
        }

        private static AnimationClip FindClip(IEnumerable<AnimationClip> clips, CharacterAction action)
        {
            string[] aliases;
            switch (action)
            {
                case CharacterAction.Idle: aliases = new[] { "idle", "stand" }; break;
                case CharacterAction.Walk: aliases = new[] { "female walk", "walking", "walk" }; break;
                case CharacterAction.Run: aliases = new[] { "running 2", "running", "run", "sprint" }; break;
                case CharacterAction.Jump: aliases = new[] { "happy jump", "jump", "leap" }; break;
                case CharacterAction.Dodge: aliases = new[] { "stand dodge", "dodge", "evade" }; break;
                case CharacterAction.MagicCharge:
                case CharacterAction.MagicRelease: aliases = new[] { "mage spell cast", "spell cast", "cast", "magic" }; break;
                case CharacterAction.Hurt: aliases = new[] { "hit reaction", "hurt", "hit" }; break;
                case CharacterAction.Defeated: aliases = new[] { "death", "defeated", "die" }; break;
                default: return null;
            }

            return clips.FirstOrDefault(clip => Matches(clip.name, aliases));
        }

        private static AnimationClip FallbackClip(List<AnimationClip> clips, CharacterAction action, AnimationClip idle)
        {
            if (action == CharacterAction.Fall || action == CharacterAction.Land || action == CharacterAction.Plunge)
            {
                return FindClip(clips, CharacterAction.Jump) ?? idle;
            }
            if (action == CharacterAction.Sword || action == CharacterAction.AirSlash ||
                action == CharacterAction.SpinCharge || action == CharacterAction.SpinRelease)
            {
                return FindClip(clips, CharacterAction.MagicRelease) ?? idle;
            }
            return idle;
        }

        private static bool Matches(string name, params string[] aliases)
        {
            string normalized = Normalize(name);
            return aliases.Any(alias => normalized.Contains(Normalize(alias)));
        }

        private static string AnimationLeaf(string name)
        {
            string leaf = name.Split('|').Last();
            return string.IsNullOrWhiteSpace(leaf) ? "Take" : leaf.Trim();
        }

        private static string Normalize(string value)
        {
            return new string((value ?? string.Empty).ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
            return string.IsNullOrEmpty(sanitized) ? "SilverCatLit" : sanitized;
        }

        [MenuItem("CoffeeGAME/Assets/Validate Silver Cat", priority = 71)]
        public static void Validate()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            if (prefab == null || controller == null || baseMap == null)
            {
                throw new InvalidOperationException("Silver Cat runtime resources are incomplete.");
            }

            SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int triangles = renderers.Where(r => r.sharedMesh != null).Sum(r => r.sharedMesh.triangles.Length / 3);
            if (renderers.Length == 0 || renderers.Any(renderer => renderer.bones == null || renderer.bones.Length == 0))
            {
                throw new InvalidOperationException("Silver Cat skin or skeleton is missing.");
            }
            if (triangles < 60000 || triangles > 130000)
            {
                throw new InvalidOperationException("Unexpected Silver Cat mesh budget: " + triangles);
            }
            if (renderers.SelectMany(renderer => renderer.sharedMaterials).Any(material =>
                    material == null || material.GetTexture("_BaseMap") == null))
            {
                throw new InvalidOperationException("Every Silver Cat material must have a URP Base Map; fallback textures are forbidden.");
            }

            AnimationClip[] controllerClips = controller.animationClips.Distinct().ToArray();
            foreach (CharacterAction action in RequiredMotions)
            {
                if (FindClip(controllerClips, action) == null)
                {
                    throw new InvalidOperationException("Silver Cat required motion is missing: " + action);
                }
            }

            Debug.Log($"CoffeeGAME Silver Cat validation passed: {triangles} triangles, " +
                $"{renderers.Length} skinned renderers, {controllerClips.Length} source clips, " +
                "all runtime materials have explicit base maps.");
        }
    }
}
