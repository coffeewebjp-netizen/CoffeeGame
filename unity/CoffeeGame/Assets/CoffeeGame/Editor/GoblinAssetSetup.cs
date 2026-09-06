using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Editor
{
    // This setup owns only the new Goblin paths; it never regenerates shared controllers.
    public static class GoblinAssetSetup
    {
        private const string Root = "Assets/CoffeeGame/Resources/";
        public const string ModelPath = Root + "Models/Goblin/forest-goblin.fbx";
        private const string ControllerPath = Root + "Animations/Goblin/GoblinRuntime.controller";
        private static readonly string[] Required = { "Idle", "Walk", "AttackWindup", "Attack", "Hurt", "Defeated" };

        public static void Configure()
        {
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Goblin FBX missing.");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importBlendShapes = false;
            importer.optimizeGameObjects = false;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.SaveAndReimport();
            var takes = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation take in takes)
            {
                string leaf = take.name.Split('|').Last();
                if (!Required.Contains(leaf)) throw new InvalidOperationException("Unexpected goblin take: " + take.name);
                take.name = leaf;
                take.loopTime = leaf == "Idle" || leaf == "Walk";
                take.loopPose = false;
                take.lockRootRotation = true;
                take.lockRootPositionXZ = true;
            }
            importer.clipAnimations = takes;
            importer.SaveAndReimport();
            var texturePath = Root + "Models/Goblin/forest-goblin-base.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.maxTextureSize = 2048;
            textureImporter.mipmapEnabled = true;
            textureImporter.sRGBTexture = true;
            textureImporter.textureCompression = TextureImporterCompression.CompressedHQ;
            textureImporter.SaveAndReimport();
            Material body = MaterialAt("ForestGoblinLit", "Universal Render Pipeline/Lit");
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            body.SetColor("_BaseColor", Color.white);
            body.SetFloat("_Smoothness", 0.18f);
            body.SetFloat("_Metallic", 0f);
            body.SetFloat("_SpecularHighlights", 0f);
            EditorUtility.SetDirty(body);
            Material club = MaterialAt("GoblinClubLit", "Universal Render Pipeline/Lit");
            club.SetColor("_BaseColor", Color.white);
            club.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Models/Goblin/goblin-club-base.png"));
            club.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(club);
            Material grip = MaterialAt("GoblinGripLit", "Universal Render Pipeline/Lit");
            grip.SetColor("_BaseColor", new Color(0.18f, 0.065f, 0.023f));
            grip.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(grip);
            Material warning = MaterialAt("GoblinWarning", "Universal Render Pipeline/Unlit");
            warning.SetColor("_BaseColor", new Color(1f, 0.36f, 0.04f, 0.72f));
            warning.SetFloat("_Surface", 1f);
            warning.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            warning.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            warning.SetFloat("_ZWrite", 0f);
            warning.SetFloat("_Cull", (float)CullMode.Off);
            warning.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            warning.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(warning);
            Directory.CreateDirectory(Root + "Animations/Goblin");
            AssetDatabase.Refresh();
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            foreach (string name in Required)
            {
                if (!clips.TryGetValue(name, out AnimationClip clip)) throw new InvalidOperationException("Missing goblin clip " + name);
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
                if (state == null) state = machine.AddState(name);
                state.motion = clip;
                state.writeDefaultValues = true;
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Validate();
        }

        private static Material MaterialAt(string name, string shaderName)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("Missing shader " + shaderName);
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || controller == null) throw new InvalidOperationException("Missing goblin runtime resources.");
            var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int triangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3);
            if (triangles < 20000 || triangles > 35000) throw new InvalidOperationException("Unexpected goblin mesh budget: " + triangles);
            if (renderers.Length != 3 || renderers.Any(r => r.bones.Length == 0)) throw new InvalidOperationException("Goblin body/club/glove skin missing.");
            var clips = controller.animationClips.Distinct().ToArray();
            if (Required.Any(n => !clips.Any(c => c.name == n && c.length > 0f))) throw new InvalidOperationException("Goblin animation set incomplete.");
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Models/Goblin/forest-goblin-base.png") == null)
                throw new InvalidOperationException("Goblin base texture missing.");
            Debug.Log($"CoffeeGAME goblin-v8 validation passed: {triangles} triangles, {renderers.Length} skinned meshes, {clips.Length} clips.");
        }
    }
}
