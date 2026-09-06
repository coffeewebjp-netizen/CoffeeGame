using System.Linq;
using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Tests
{
    public sealed class SilverCatMotionTests
    {
        private GameObject model;
        private AnimationClip[] clips;
        [SetUp] public void Setup()
        {
            model = Object.Instantiate(Resources.Load<GameObject>("Models/Characters/SilverCat/silver-cat-girl"));
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            clips = Resources.Load<RuntimeAnimatorController>("Animations/Characters/SilverCatV14/SilverCatMotionV14").animationClips;
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(model);
        private AnimationClip Clip(string name) => clips.First(c => c.name == name);
        private Transform Bone(string name) => model.GetComponentsInChildren<Transform>().First(t => t.name == name);
        [TestCase("Idle")] [TestCase("Walk")] [TestCase("Run")]
        public void LocomotionHasNoRootDriftOrLoopSeam(string name)
        {
            var clip = Clip(name); clip.SampleAnimation(model, .0001f);
            var bones = model.GetComponentsInChildren<Transform>(); var points = bones.Select(b => b.position).ToArray();
            var rotations = bones.Select(b => b.rotation).ToArray(); Vector3 root = model.transform.position;
            clip.SampleAnimation(model, clip.length - .0001f);
            Assert.That(Vector3.Distance(root, model.transform.position), Is.LessThan(.001f));
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(Vector3.Distance(points[i], bones[i].position), Is.LessThan(.01f), name + " seam " + bones[i].name);
                Assert.That(Quaternion.Angle(rotations[i], bones[i].rotation), Is.LessThan(2f), name + " rotation seam " + bones[i].name);
            }
        }
        [Test]
        public void AirborneAndLandingHaveDifferentLegAndHipPoses()
        {
            Clip("Jump").SampleAnimation(model, .25f); Vector3 jumpFoot = Bone("LeftFoot").position;
            Clip("Fall").SampleAnimation(model, .28f); Vector3 fallFoot = Bone("LeftFoot").position;
            Clip("Idle").SampleAnimation(model, 0); float standing = Bone("Hips").position.y;
            Clip("Land").SampleAnimation(model, .075f);
            Assert.That(jumpFoot.y - fallFoot.y, Is.GreaterThan(.08f));
            Assert.That(standing - Bone("Hips").position.y, Is.GreaterThan(.09f));
        }
        [Test]
        public void VolleysAlternateArmsAndMajorCastRaisesBothHands()
        {
            Clip("CatVolley1").SampleAnimation(model, 0); float right = Bone("RightHand").position.z - Bone("LeftHand").position.z;
            Clip("CatVolley2").SampleAnimation(model, 0); float left = Bone("LeftHand").position.z - Bone("RightHand").position.z;
            Assert.That(right, Is.GreaterThan(.20f)); Assert.That(left, Is.GreaterThan(.20f));
            Clip("MagicCharge").SampleAnimation(model, 1.18f);
            Assert.That(Bone("LeftHand").position.y, Is.GreaterThan(Bone("Spine").position.y));
            Assert.That(Bone("RightHand").position.y, Is.GreaterThan(Bone("Spine").position.y));
        }
        [Test]
        public void RunningDodgeTurnsWholeBodyAndReturnsUpright()
        {
            var clip = Clip("Dodge"); clip.SampleAnimation(model, clip.length * .5f);
            Assert.That(Bone("Head").position.y, Is.LessThan(Bone("Hips").position.y));
            clip.SampleAnimation(model, clip.length);
            Assert.That(Bone("Head").position.y, Is.GreaterThan(Bone("Hips").position.y + .25f));
        }
        [Test]
        public void AllClipsKeepFiniteBoneTransformsAndAnatomicalBoneLengths()
        {
            Clip("Idle").SampleAnimation(model, 0);
            string[] ends = { "LeftLeg", "RightLeg", "LeftForeArm", "RightForeArm" };
            var lengths = ends.Select(n => Vector3.Distance(Bone(n).position, Bone(n).parent.position)).ToArray();
            foreach (var clip in clips.Distinct()) for (int f = 0; f <= 20; f++)
            {
                clip.SampleAnimation(model, clip.length * f / 20);
                for (int i = 0; i < ends.Length; i++)
                {
                    var bone = Bone(ends[i]); float distance = Vector3.Distance(bone.position, bone.parent.position);
                    Assert.That(float.IsNaN(distance) || float.IsInfinity(distance), Is.False, clip.name);
                    Assert.That(distance, Is.EqualTo(lengths[i]).Within(.002f), clip.name + " bone length " + ends[i]);
                }
            }
        }
    }
}
