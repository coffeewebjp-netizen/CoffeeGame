using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CoffeeGame.Editor
{
    public static class DragonGirlAssetSetup
    {
        public const string ModelPath="Assets/CoffeeGame/Resources/Models/Characters/DragonGirl/dragon-girl.fbx";
        public const string Folder="Assets/CoffeeGame/Resources/Animations/Characters/DragonGirl";
        public static void Configure()
        {
            AssetDatabase.Refresh();Directory.CreateDirectory(Folder);
            var importer=(ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if(importer==null)throw new InvalidOperationException("Dragon FBX missing");
            importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;
            importer.importCameras=false;importer.importLights=false;importer.optimizeGameObjects=false;
            importer.globalScale=1;importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
            var takes=importer.defaultClipAnimations;
            foreach(var take in takes)
            {
                take.name=take.name.Split('|').Last();
                take.loopTime=take.name=="Idle"||take.name=="Walk"||take.name=="Run"||take.name=="Fall";
                take.loopPose=take.loopTime;take.lockRootRotation=true;take.lockRootHeightY=true;take.lockRootPositionXZ=true;
                take.keepOriginalOrientation=true;take.keepOriginalPositionY=true;take.keepOriginalPositionXZ=true;
            }
            importer.clipAnimations=takes;importer.SaveAndReimport();
            string modelFolder=Path.GetDirectoryName(ModelPath).Replace('\\','/');
            foreach(string name in new[]{"dragon-basecolor"})
            {
                var texture=(TextureImporter)AssetImporter.GetAtPath(modelFolder+"/"+name+".png");
                texture.textureType=name.EndsWith("normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
                texture.sRGBTexture=!name.EndsWith("normal");texture.maxTextureSize=2048;texture.mipmapEnabled=true;texture.SaveAndReimport();
            }
            string materialPath=modelFolder+"/DragonGirl.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(modelFolder+"/dragon-basecolor.png"));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.3f);material.SetFloat("_Metallic",.08f);
            // The triangle remesh has a different UV layout and no normal map.
            material.SetTexture("_BumpMap",null);material.DisableKeyword("_NORMALMAP");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            foreach(var m in prefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct())
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),m.name),material);
            importer.SaveAndReimport();
            string path=Folder+"/DragonGirlRuntime.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null)controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine=controller.layers[0].stateMachine;
            foreach(var state in machine.states)machine.RemoveState(state.state);
            if(!controller.parameters.Any(p=>p.name=="LocomotionSpeed"))controller.AddParameter("LocomotionSpeed",AnimatorControllerParameterType.Float);
            foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))
            {var state=machine.AddState(clip.name);state.motion=clip;if(clip.name=="Idle")machine.defaultState=state;}
            foreach(string expected in new[]{"Idle","Walk","Run","ClawRight","ClawLeft","ClawHeavy","DragonGate","DragonChop","DragonBreath","AirClaw","Plunge","StompLand"})
                if(!machine.states.Any(s=>s.state.name==expected))throw new InvalidOperationException("Missing dragon motion "+expected);
            AssetDatabase.SaveAssets();Debug.Log("DRAGON_ASSET_SETUP_PASS");
        }
        public static void Build()
        {
            Configure();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/CoffeeGame/Scenes/CombatSandbox.unity"},locationPathName="Builds/Windows-DragonGirl/CoffeeGAME-DragonGirl.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Dragon build failed");
        }
    }
}
