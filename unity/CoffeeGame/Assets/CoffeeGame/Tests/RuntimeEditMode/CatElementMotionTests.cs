using System.Linq;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class CatElementMotionTests
    {
        private GameObject model;
        private AnimationClip[] clips;
        private Transform Bone(string name) => model.GetComponentsInChildren<Transform>().First(t => t.name == name);
        private void Sample(string name, float time) => clips.First(c => c.name == name).SampleAnimation(model, time);
        [SetUp] public void Setup()
        {
            model = Object.Instantiate(Resources.Load<GameObject>("Models/Characters/SilverCat/silver-cat-girl"));
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            var controller = Resources.Load<RuntimeAnimatorController>("Animations/Characters/SilverCatV17/SilverCatElementsV17");
            Assert.That(controller, Is.Not.Null, "Generate the V17 library before running motion tests.");
            clips = controller.animationClips.Distinct().ToArray();
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(model);

        [Test] public void RunLeansForwardAndAlternatesHandsAndFeet()
        {
            Sample("Idle",0); float idleLean=Bone("Head").position.z-Bone("Hips").position.z;
            Sample("Run",.025f); float runLean=Bone("Head").position.z-Bone("Hips").position.z;
            Vector3 foot=Bone("LeftFoot").position, hand=Bone("RightHand").position;
            Sample("Run",.295f);
            Assert.That(runLean-idleLean,Is.GreaterThan(.08f));
            Assert.That(Vector3.Distance(foot,Bone("LeftFoot").position),Is.GreaterThan(.2f));
            Assert.That(Vector3.Distance(hand,Bone("RightHand").position),Is.GreaterThan(.2f));
        }

        [Test] public void EarthLandingCrouchesWithWideFeetAndHandsNearGround()
        {
            Sample("Idle",0); float standing=Bone("Hips").position.y;
            Sample("CatEarthLand",.25f);
            Assert.That(standing-Bone("Hips").position.y,Is.GreaterThan(.20f));
            Assert.That(Mathf.Abs(Bone("LeftFoot").position.x-Bone("RightFoot").position.x),Is.GreaterThan(.3f));
            Assert.That(Bone("RightHand").position.y,Is.LessThan(Bone("Hips").position.y));
            Assert.That(Bone("Head").position.z,Is.GreaterThan(Bone("Hips").position.z+.12f));
            Sample("CatEarthLand",1);
            Assert.That(Bone("Hips").position.y,Is.GreaterThan(standing-.08f));
        }

        [Test] public void SprintCarriesChestAheadOfPelvisAndTrailingFootThroughoutStride()
        {
            for(int i=0;i<12;i++)
            {
                Sample("Run",i*.54f/12);
                Vector3 hip=Bone("Hips").position, head=Bone("Head").position;
                float trailing=Mathf.Min(Bone("LeftFoot").position.z,Bone("RightFoot").position.z);
                Assert.That(head.z-hip.z,Is.GreaterThan(.18f),"forward torso at phase "+i);
                Assert.That(hip.z-trailing,Is.GreaterThan(.12f),"trailing push-off leg at phase "+i);
            }
        }

        [Test] public void EarthDropDrivesRightHandVerticallyBelowShoulder()
        {
            Sample("Plunge",0); float start=Bone("RightHand").position.y;
            foreach(float t in new[]{.18f,.24f,.31f})
            {
                Sample("Plunge",t);
                Vector3 arm=Bone("RightHand").position-Bone("RightArm").position;
                Assert.That(Vector3.Dot(arm.normalized,Vector3.down),Is.GreaterThan(.9f),"vertical strike at "+t);
                Assert.That(start-Bone("RightHand").position.y,Is.GreaterThan(.25f));
            }
        }

        [Test] public void AirWindSweepsTheArmAndKeepsLegsFolded()
        {
            Sample("AirSlash",0); Vector3 hand=Bone("RightHand").position;
            Sample("AirSlash",.35f);
            Assert.That(Vector3.Distance(hand,Bone("RightHand").position),Is.GreaterThan(.15f));
            Assert.That(Bone("LeftFoot").position.y,Is.GreaterThan(.06f));
        }

        [Test] public void ElementLibraryPreservesAnatomicalBoneLengths()
        {
            Assert.That(clips.Length,Is.EqualTo(18));
            Sample("Idle",0);
            string[] names={"LeftLeg","RightLeg","LeftForeArm","RightForeArm"};
            var lengths=names.Select(n=>Vector3.Distance(Bone(n).position,Bone(n).parent.position)).ToArray();
            foreach(var clip in clips) for(int f=0;f<=20;f++)
            {
                clip.SampleAnimation(model,clip.length*f/20);
                for(int i=0;i<names.Length;i++)
                {
                    var bone=Bone(names[i]); float length=Vector3.Distance(bone.position,bone.parent.position);
                    Assert.That(length,Is.EqualTo(lengths[i]).Within(.002f),clip.name+" "+names[i]);
                }
            }
        }

        [Test] public void RunLoopsWithoutRootDriftOrPoseSnap()
        {
            Sample("Run",.0001f); var bones=model.GetComponentsInChildren<Transform>();
            var positions=bones.Select(t=>t.position).ToArray();var rotations=bones.Select(t=>t.rotation).ToArray();
            Sample("Run",.5399f);
            for(int i=0;i<bones.Length;i++)
            {
                Assert.That(Vector3.Distance(positions[i],bones[i].position),Is.LessThan(.01f),bones[i].name);
                Assert.That(Quaternion.Angle(rotations[i],bones[i].rotation),Is.LessThan(2),bones[i].name);
            }
        }
    }

    public sealed class TimeStopActorSeparationTests
    {
        [Test] public void CharactersAndOwnedSpellsStayUprightButEnvironmentDoesNot()
        {
            var actor=new GameObject("actor"); actor.AddComponent<Health>().Initialize(10);
            var skin=GameObject.CreatePrimitive(PrimitiveType.Cube);skin.transform.SetParent(actor.transform);
            var spell=GameObject.CreatePrimitive(PrimitiveType.Cube);CombatOwnership.Assign(spell,actor);
            var environment=GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(TimeStopWorldEffect.IsActorRenderer(skin.GetComponent<Renderer>()),Is.True);
                Assert.That(TimeStopWorldEffect.IsActorRenderer(spell.GetComponent<Renderer>()),Is.True);
                Assert.That(TimeStopWorldEffect.IsActorRenderer(environment.GetComponent<Renderer>()),Is.False);
            }
            finally {Object.DestroyImmediate(actor);Object.DestroyImmediate(spell);Object.DestroyImmediate(environment);}
        }

        [Test] public void FullStopGraysAndFlipsOnlyBackgroundPixels()
        {
            var material=new Material(Shader.Find("CoffeeGame/TimeStopWorldEffect"));
            var world=new Texture2D(2,2,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point};
            var actors=new Texture2D(2,2,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point};
            var output=new RenderTexture(16,16,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var read=new Texture2D(16,16,TextureFormat.RGBA32,false,true);
            var previous=RenderTexture.active;
            try
            {
                world.SetPixels(new[]{Color.red,Color.red,Color.blue,Color.blue});world.Apply();
                actors.SetPixels(new[]{Color.green,Color.clear,Color.clear,Color.clear});actors.Apply();
                material.SetTexture("_ActorTex",actors);material.SetFloat("_Progress",1);
                Graphics.Blit(world,output,material);RenderTexture.active=output;
                read.ReadPixels(new Rect(0,0,16,16),0,0);read.Apply();
                Color actor=read.GetPixel(3,3), lower=read.GetPixel(12,3), upper=read.GetPixel(12,12);
                Assert.That(actor.g,Is.GreaterThan(.95f));Assert.That(actor.r,Is.LessThan(.02f));Assert.That(actor.b,Is.LessThan(.02f));
                Assert.That(lower.r,Is.EqualTo(lower.g).Within(.01f));Assert.That(lower.g,Is.EqualTo(lower.b).Within(.01f));
                Assert.That(upper.r,Is.EqualTo(upper.g).Within(.01f));Assert.That(upper.g,Is.EqualTo(upper.b).Within(.01f));
                Assert.That(upper.r,Is.GreaterThan(lower.r+.1f),"Red background moves from bottom to top; actor remains bottom left");
            }
            finally {RenderTexture.active=previous;Object.DestroyImmediate(material);Object.DestroyImmediate(world);Object.DestroyImmediate(actors);Object.DestroyImmediate(output);Object.DestroyImmediate(read);}
        }

        [Test] public void InterruptDuringTurnRestoresCameraAndAllowsAnotherStop()
        {
            var host=new GameObject("camera"); var camera=host.AddComponent<Camera>();
            camera.enabled=false;
            var effect=host.AddComponent<TimeStopWorldEffect>(); var original=new RenderTexture(32,32,16);
            camera.targetTexture=original;effect.Initialize(camera);
            try
            {
                effect.SetActive(true);effect.Advance(.2f);effect.StopImmediately();
                Assert.That(camera.targetTexture,Is.SameAs(original));
                Assert.That(effect.IsCompositing,Is.False);
                effect.SetActive(true);effect.Advance(.65f);
                Assert.That(effect.BackgroundVerticalScale,Is.EqualTo(-1).Within(.001f));
                effect.StopImmediately();Assert.That(camera.targetTexture,Is.SameAs(original));
            }
            finally {Object.DestroyImmediate(host);Object.DestroyImmediate(original);}
        }
    }
}
