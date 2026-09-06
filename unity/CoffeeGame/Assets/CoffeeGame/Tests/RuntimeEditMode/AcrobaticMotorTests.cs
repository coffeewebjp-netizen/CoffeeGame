using CoffeeGame.Actors;
using CoffeeGame.Domain;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Combat.Tests
{
    public sealed class AcrobaticMotorTests
    {
        private GameObject actor, floor;
        private PlayerMotor3D motor;
        private CombatTuning tuning;

        [SetUp] public void Setup()
        {
            Time.timeScale = 1f;
            tuning = CombatTuning.CreateDefault();
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0,-.5f,0); floor.transform.localScale = new Vector3(30,1,30);
            actor = new GameObject("acrobatic-motor-test");
            var controller = actor.AddComponent<CharacterController>();
            controller.height=1.7f; controller.center=Vector3.up*.85f; controller.radius=.22f;
            motor=actor.AddComponent<PlayerMotor3D>(); motor.UseCommands=true;
            motor.Initialize(null,tuning,null,null); motor.ResetMotor(Vector3.up*.03f);
            Physics.SyncTransforms(); Step(10);
            Assert.That(motor.IsGrounded,Is.True);
        }
        [TearDown] public void Cleanup()
        { Object.DestroyImmediate(actor);Object.DestroyImmediate(floor);Object.DestroyImmediate(tuning);Time.timeScale=1f; }
        private void Step(int frames) { for(int i=0;i<frames;i++) motor.Tick(.01f); }

        [Test] public void LockedRunningFacesTravelButActionStopAndGuardReacquireEnemy()
        {
            var enemy = new GameObject("running lock target");
            try
            {
                var health = enemy.AddComponent<Health>(); health.Initialize(10);
                enemy.transform.position = new Vector3(0, 0, 8); motor.LockedTarget = health;
                motor.Commands = new ActorCommandFrame { Move = Vector2.right, WorldSpace = true };
                Step(80);
                Assert.That(motor.IsRunning, Is.True);
                Assert.That(motor.Facing.x, Is.GreaterThan(.99f));
                Assert.That(motor.HasLockedTarget, Is.True);
                motor.FaceLockedTargetForAction(.34f); Step(1);
                Vector3 aim = Vector3.ProjectOnPlane(enemy.transform.position - actor.transform.position, Vector3.up).normalized;
                Assert.That(Vector3.Dot(motor.Facing, aim), Is.GreaterThan(.999f));
                motor.Commands = default; Step(1);
                Assert.That(motor.IsRunning, Is.False);
                Assert.That(motor.Facing.z, Is.GreaterThan(.9f));
                motor.IsGuarding = true; Step(1);
                Assert.That(motor.Facing.z, Is.GreaterThan(.9f));
            }
            finally { Object.DestroyImmediate(enemy); }
        }

        [TestCase(0f)] [TestCase(35f)] [TestCase(130f)]
        public void EveryRearHalfAngleHasExactlyOneGuardJumpSector(float yaw)
        {
            Vector3 facing = Quaternion.Euler(0,yaw,0) * Vector3.forward;
            for (int degrees=90; degrees<=270; degrees++)
            {
                Vector3 direction = Quaternion.Euler(0,degrees,0) * facing;
                bool side = PlayerMotor3D.IsSidewaysInput(facing,direction);
                bool back = PlayerMotor3D.IsBackwardInput(facing,direction);
                Assert.That(side ^ back, Is.True, "rear sector angle="+degrees+" yaw="+yaw);
                if (degrees==135 || degrees==225) Assert.That(side,Is.True,"45 degrees belongs to cartwheel");
            }
        }

        [TestCase(-1f, 1f, true)] [TestCase(1f, 1f, true)]
        [TestCase(-.98f, 1f, false)] [TestCase(.98f, 1f, false)]
        public void RearDiagonalExecutesRealGuardJump(float x,float y,bool cartwheel)
        {
            motor.IsGuarding=true;
            motor.Commands=new ActorCommandFrame{Jump=true,Move=new Vector2(x,y).normalized,WorldSpace=true};
            motor.Tick(.01f);
            Assert.That(motor.IsGuardJumping,Is.True);
            Assert.That(motor.IsCartwheeling,Is.EqualTo(cartwheel));
            Assert.That(motor.IsBackflipping,Is.EqualTo(!cartwheel));
        }

        [Test] public void LockedFacingSurvivesStrafingAndGuardThenReturnsToMovementAfterRelease()
        {
            var enemy=new GameObject("facing target");
            try
            {
                var health=enemy.AddComponent<Health>();health.Initialize(10);enemy.transform.position=new Vector3(0,0,8);
                motor.LockedTarget=health;
                motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};Step(10);
                Vector3 toTarget=(enemy.transform.position-actor.transform.position);toTarget.y=0;
                Assert.That(Vector3.Dot(motor.Facing,toTarget.normalized),Is.GreaterThan(.999f));
                motor.IsGuarding=true;enemy.transform.position=new Vector3(-8,0,0);Step(5);
                Assert.That(motor.Facing.x,Is.LessThan(-.99f));
                motor.LockedTarget=null;motor.IsGuarding=false;Step(5);
                Assert.That(motor.Facing.x,Is.GreaterThan(.99f));
            }
            finally {Object.DestroyImmediate(enemy);}
        }

        [Test] public void RollStaysLowAndDoesNotEndOnEarlyFloorContact()
        {
            int landings=0; motor.Landed+=_=>landings++;
            motor.Commands=new ActorCommandFrame { Dodge=true,Move=Vector2.right,WorldSpace=true };
            motor.Tick(.01f);motor.Commands=default;
            float peak=actor.transform.position.y;
            for(int i=0;i<30;i++){motor.Tick(.01f);peak=Mathf.Max(peak,actor.transform.position.y);}
            Assert.That(peak,Is.LessThan(.16f));
            Assert.That(motor.IsDodging,Is.True);
            Assert.That(landings,Is.Zero,"physical toe contact must not end the rolling action or its invulnerability early");
            Step(38);
            Assert.That(motor.IsDodging,Is.False);Assert.That(landings,Is.EqualTo(1));
            Assert.That(actor.transform.position.x,Is.GreaterThan(2f));
        }
        [Test] public void GuardBackwardJumpBackflipsWithoutDodgeRewardSignal()
        {
            int jumps=0,dodges=0;motor.Jumped+=()=>jumps++;motor.Dodged+=()=>dodges++;
            Vector3 facing=motor.Facing,start=actor.transform.position;
            motor.IsGuarding=true;motor.Commands=new ActorCommandFrame { Jump=true,Move=Vector2.up,WorldSpace=true };
            motor.Tick(.01f);motor.Commands=default;
            Assert.That(motor.IsBackflipping,Is.True);Assert.That(motor.IsGuarding,Is.False);
            Assert.That(motor.CanAct,Is.False);Assert.That(jumps,Is.EqualTo(1));Assert.That(dodges,Is.Zero);
            Step(30);Assert.That(actor.transform.position.y,Is.GreaterThan(.45f));
            Assert.That(Vector3.Dot(motor.Facing,facing),Is.GreaterThan(.99f));
            Step(55);Assert.That(motor.IsBackflipping,Is.False);
            Assert.That(actor.transform.position.z-start.z,Is.GreaterThan(1.7f));
        }
        [Test] public void GuardForwardJumpDoesNotBecomeBackflip()
        {
            motor.IsGuarding=true; motor.Commands=new ActorCommandFrame{ Jump=true,Move=Vector2.down,WorldSpace=true };
            Step(2);Assert.That(motor.IsBackflipping,Is.False);Assert.That(motor.IsGrounded,Is.True);
        }
        [Test] public void PlungeLocksMovementAndActionsForOneSecond()
        {
            motor.Commands=new ActorCommandFrame { Jump=true };motor.Tick(.01f);motor.Commands=default;Step(20);
            motor.Commands=new ActorCommandFrame { Move=Vector2.down };motor.Tick(.01f);motor.Commands=default;
            Assert.That(motor.IsPlunging,Is.True);
            for(int i=0;i<200&&!motor.IsGrounded;i++)motor.Tick(.01f);
            Assert.That(motor.PlungeRecoveryRemaining,Is.EqualTo(1f).Within(.011f));
            Vector3 contact=actor.transform.position;
            motor.Commands=new ActorCommandFrame{Move=Vector2.right,Jump=true,Dodge=true,WorldSpace=true};Step(89);
            Assert.That(motor.CanAct,Is.False);Assert.That(Vector3.Distance(contact,actor.transform.position),Is.LessThan(.02f));
            motor.Commands=default;Step(13);Assert.That(motor.CanAct,Is.True);
        }
        [TestCase(-1f)] [TestCase(1f)]
        public void GuardSideJumpCartwheelsWithoutDodgeRewardAndKeepsFacing(float side)
        {
            int jumps=0,dodges=0; motor.Jumped+=()=>jumps++;motor.Dodged+=()=>dodges++;
            var facing=motor.Facing;var start=actor.transform.position;
            motor.IsGuarding=true;motor.Commands=new ActorCommandFrame{Jump=true,Move=Vector2.right*side,WorldSpace=true};
            motor.Tick(.01f);motor.Commands=default;
            Assert.That(motor.IsCartwheeling,Is.True);Assert.That(motor.IsBackflipping,Is.False);
            Assert.That(motor.CanAct,Is.False);Assert.That(jumps,Is.EqualTo(1));Assert.That(dodges,Is.Zero);
            var held=actor.transform.position;float progress=motor.AcrobaticProgress;
            Time.timeScale=0f;Step(15);Assert.That(actor.transform.position,Is.EqualTo(held));
            Assert.That(motor.AcrobaticProgress,Is.EqualTo(progress));Time.timeScale=1f;
            Step(90);Assert.That(motor.IsGuardJumping,Is.False);Assert.That(motor.CanAct,Is.True);
            Assert.That((actor.transform.position.x-start.x)*side,Is.GreaterThan(1.7f));
            Assert.That(Vector3.Dot(motor.Facing,facing),Is.GreaterThan(.99f));
        }
        [Test] public void RunningDodgeUsesHighLeapAndKeepsItsSelectedStyle()
        {
            motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};Step(80);
            Assert.That(motor.IsRunning,Is.True);
            motor.Commands=new ActorCommandFrame{Dodge=true,Move=Vector2.up,WorldSpace=true};motor.Tick(.01f);
            Assert.That(motor.IsRunningDodge,Is.True);
            motor.Commands=default;Step(30);
            Assert.That(motor.IsRunningDodge,Is.True);Assert.That(actor.transform.position.y,Is.GreaterThan(.65f));
            Step(65);Assert.That(motor.IsDodging,Is.False);Assert.That(motor.IsRunningDodge,Is.False);
        }
        [Test] public void WalkingAndReleasedRunUseLowRoll()
        {
            motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};Step(30);
            motor.Commands=new ActorCommandFrame{Dodge=true,Move=Vector2.right,WorldSpace=true};motor.Tick(.01f);
            Assert.That(motor.IsRunningDodge,Is.False);motor.Commands=default;Step(70);
            motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};Step(80);
            Assert.That(motor.IsRunning,Is.True);motor.Commands=new ActorCommandFrame{Dodge=true};motor.Tick(.01f);
            Assert.That(motor.IsRunningDodge,Is.False,"releasing movement selects the stationary roll");
        }
        [Test] public void GuardSideInputIsRelativeToFacing()
        {
            Assert.That(PlayerMotor3D.IsSidewaysInput(Vector3.right,Vector3.forward),Is.True);
            Assert.That(PlayerMotor3D.IsSidewaysInput(Vector3.right,Vector3.left),Is.False);
            Assert.That(PlayerMotor3D.IsSidewaysInput(Vector3.right,Vector3.zero),Is.False);
        }
        [Test] public void PauseFreezesAcrobaticProgressAndPhysicalPosition()
        {
            motor.Commands=new ActorCommandFrame{Dodge=true};motor.Tick(.01f);motor.Commands=default;
            Vector3 p=actor.transform.position;float progress=motor.AcrobaticProgress;
            motor.CanMove=false;Time.timeScale=0f;Step(40);
            Assert.That(actor.transform.position,Is.EqualTo(p));Assert.That(motor.AcrobaticProgress,Is.EqualTo(progress));
            Assert.That(motor.IsDodging,Is.True,"opening the pause menu must preserve the active roll");
            Time.timeScale=1f;motor.CanMove=true;Step(70);Assert.That(motor.IsDodging,Is.False);
        }
    }
}
