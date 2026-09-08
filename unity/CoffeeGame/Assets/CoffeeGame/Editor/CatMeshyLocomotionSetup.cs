using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Editor
{
    public static class CatMeshyLocomotionSetup
    {
        private const string Sources = "Assets/CoffeeGame/Editor/MotionSources/CatV18";
        private const float Fps = 60;
        private static string Leaf(string name) => name.Split('|').Last();
        public static void Configure() => Configure(false);
        public static void ConfigureSprintOnly() => Configure(true);
        private static void Configure(bool sprintOnly)
        {
            var target = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SilverCatAssetSetup.ModelPath));
            foreach (var a in target.GetComponentsInChildren<Animator>()) a.enabled = false;
            var skin = target.GetComponentInChildren<SkinnedMeshRenderer>();
            var bind = skin.sharedMesh.bindposes;
            foreach (var p in skin.bones.Select((b,i)=>new {bone=b,m=skin.transform.localToWorldMatrix*bind[i].inverse})
                .OrderBy(p=>AnimationUtility.CalculateTransformPath(p.bone,target.transform).Count(c=>c=='/')))
                p.bone.SetPositionAndRotation(p.m.GetColumn(3),p.m.rotation);
            try
            {
                Dictionary<string,Quaternion> airArms;
                using (var sprint = new Transfer(target,Sources+"/MeshySprint.fbx","MeshySprint"))
                {
                    Author(target,"Run",.54f,true,p=>sprint.Sample(p*sprint.Length,true));
                    sprint.Sample(.12f,true);
                    airArms=target.GetComponentsInChildren<Transform>().Where(t=>IsArm(t.name)).ToDictionary(t=>t.name,t=>t.localRotation);
                }
                if (sprintOnly) { AssetDatabase.SaveAssets(); return; }
                using (var jump = new Transfer(target,Sources+"/MeshyJump.fbx","MeshyJump",airArms))
                {
                    // Source frames 29-50: push-off to folded apex. The motor owns height.
                    Author(target,"Jump",.34f,false,p=>jump.Sample(Mathf.Lerp(27,48,p)/60f,false));
                    Author(target,"Fall",.32f,false,p=>jump.Sample(Mathf.Lerp(48,65,p)/60f,false));
                    Author(target,"Land",.30f,false,p=>jump.Land(p));
                }
                AssetDatabase.SaveAssets();
                Debug.Log("CAT_V18: Meshy sprint and jump transferred onto the existing 24-bone cat; motor owns displacement.");
            }
            finally { UnityEngine.Object.DestroyImmediate(target); }
        }
        private static bool IsArm(string name)=>name.EndsWith("Shoulder")||name.EndsWith("Arm")||name.EndsWith("Hand");
        private sealed class Transfer : IDisposable
        {
            private readonly GameObject donor;
            private readonly Dictionary<string,Quaternion> airArms;
            private readonly Transform[] targets, sources;
            private readonly Vector3[] positions;
            private readonly Quaternion[] rotations, worldRest, donorRest;
            private readonly AnimationClip clip;
            private readonly Vector3 hipRest, sourceHipRest;
            private readonly float scale, footY;
            private readonly int hipIndex;
            public float Length => clip.length;
            public Transfer(GameObject target,string path,string motion,Dictionary<string,Quaternion> airArms=null)
            {
                this.airArms=airArms;
                var targetSkin=target.GetComponentInChildren<SkinnedMeshRenderer>();
                foreach(var pair in targetSkin.bones.Select((b,i)=>new {bone=b,m=targetSkin.transform.localToWorldMatrix*targetSkin.sharedMesh.bindposes[i].inverse})
                    .OrderBy(v=>AnimationUtility.CalculateTransformPath(v.bone,target.transform).Count(c=>c=='/')))
                    pair.bone.SetPositionAndRotation(pair.m.GetColumn(3),pair.m.rotation);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType=ModelImporterAnimationType.Generic;
                importer.animationCompression=ModelImporterAnimationCompression.Off;
                importer.resampleCurves=true; importer.optimizeGameObjects=false; importer.SaveAndReimport();
                donor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                foreach(var a in donor.GetComponentsInChildren<Animator>()) a.enabled=false;
                var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
                Debug.Log("CAT_SOURCE "+path+" "+string.Join(",",clips.Select(c=>c.name+":"+c.length)));
                clip=clips.Single(c=>Leaf(c.name)==motion);
                clips.Single(c=>Leaf(c.name)=="BindPose").SampleAnimation(donor,0);
                var sourceMap=donor.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
                targets=target.GetComponentsInChildren<Transform>().Where(t=>sourceMap.ContainsKey(t.name)&&t.name!="Armature"&&t!=target.transform).ToArray();
                sources=targets.Select(t=>sourceMap[t.name]).ToArray();
                positions=targets.Select(t=>t.localPosition).ToArray(); rotations=targets.Select(t=>t.localRotation).ToArray();
                worldRest=targets.Select(t=>t.rotation).ToArray(); donorRest=sources.Select(t=>t.rotation).ToArray();
                hipIndex=Array.FindIndex(targets,t=>t.name=="Hips");
                hipRest=targets[hipIndex].position;sourceHipRest=sources[hipIndex].position;
                int head=Array.FindIndex(targets,t=>t.name=="Head");
                scale=Vector3.Distance(targets[head].position,hipRest)/Vector3.Distance(sources[head].position,sourceHipRest);
                footY=targets.Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").Min(t=>t.position.y);
            }
            public void Sample(float time,bool running,bool landing=false)
            {
                clip.SampleAnimation(donor,time);
                for(int i=0;i<targets.Length;i++) { targets[i].localPosition=positions[i];targets[i].localRotation=rotations[i]; }
                for(int i=0;i<targets.Length;i++) targets[i].rotation=sources[i].rotation*Quaternion.Inverse(donorRest[i])*worldRest[i];
                // Keep the native jump leg timing but use the same athletic arm drive as the sprint.
                if(airArms!=null&&!landing) foreach(var bone in targets)
                    if(airArms.TryGetValue(bone.name,out var pose)) bone.localRotation=Quaternion.Slerp(bone.localRotation,pose,.9f);
                Vector3 delta=(sources[hipIndex].position-sourceHipRest)*scale;
                // Remove source root travel. Preserve sprint bob; physics controls the jump arc.
                targets[hipIndex].position=hipRest+new Vector3(0,running?delta.y:0,0);
                var waist=targets.First(t=>t.name=="Spine02");
                if (running)
                {
                    // Pitch the entire native stride about the pelvis: the support and
                    // push-off legs trail the forward chest instead of staying vertical.
                    targets[hipIndex].rotation=Quaternion.AngleAxis(28,Vector3.right)*targets[hipIndex].rotation;
                    targets[hipIndex].position+=Vector3.forward*.09f;
                }
                waist.rotation=Quaternion.AngleAxis(running?10:18,Vector3.right)*waist.rotation;
                if(running)
                {
                    float floor=targets.Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").Min(t=>t.position.y);
                    targets[hipIndex].position+=Vector3.up*(footY-floor+.018f*Mathf.Sin(time/Length*Mathf.PI*4)*Mathf.Sin(time/Length*Mathf.PI*4));
                }
            }
            public void Land(float p)
            {
                Sample(Mathf.Lerp(66,108,p)/60f,false,true);
                // Keep the support foot planted through compression and recovery.
                float floor=targets.Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").Min(t=>t.position.y);
                targets[hipIndex].position+=Vector3.up*(footY-floor);
            }
            public void Dispose()=>UnityEngine.Object.DestroyImmediate(donor);
        }
        internal static void Author(GameObject model,string name,float seconds,bool loop,Action<float> sample)
        {
            var bones=model.GetComponentsInChildren<Transform>().Where(t=>t!=model.transform&&t.GetComponent<Renderer>()==null).ToArray();
            string[] props={"m_LocalPosition.x","m_LocalPosition.y","m_LocalPosition.z","m_LocalRotation.x","m_LocalRotation.y","m_LocalRotation.z","m_LocalRotation.w"};
            var curves=bones.Select(_=>props.Select(p=>new AnimationCurve()).ToArray()).ToArray();
            int count=Mathf.RoundToInt(seconds*Fps);
            Vector3[] firstPos=null;Quaternion[] firstRot=null;
            for(int f=0;f<=count;f++)
            {
                float p=f/(float)count;sample(p);
                if(f==0){firstPos=bones.Select(b=>b.localPosition).ToArray();firstRot=bones.Select(b=>b.localRotation).ToArray();}
                for(int i=0;i<bones.Length;i++)
                {
                    float blend=loop?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,1,p)):0;
                    Vector3 v=Vector3.Lerp(bones[i].localPosition,firstPos[i],blend);
                    Quaternion q=Quaternion.Slerp(bones[i].localRotation,firstRot[i],blend);
                    float[] values={v.x,v.y,v.z,q.x,q.y,q.z,q.w};
                    for(int k=0;k<7;k++) curves[i][k].AddKey(p*seconds,values[k]);
                }
            }
            var clip=new AnimationClip{name=name,frameRate=Fps};
            for(int i=0;i<bones.Length;i++)for(int k=0;k<7;k++)
            {
                for(int f=0;f<curves[i][k].length;f++)
                { AnimationUtility.SetKeyLeftTangentMode(curves[i][k],f,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curves[i][k],f,AnimationUtility.TangentMode.ClampedAuto); }
                AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i],model.transform),typeof(Transform),props[k]),curves[i][k]);
            }
            clip.EnsureQuaternionContinuity();var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=loop;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(SilverCatMotionSetup.ElementFolder+"/"+name+".anim");
            if(existing==null)throw new InvalidOperationException("Generate V17 before replacing locomotion");
            EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);
        }
    }
}
