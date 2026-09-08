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
