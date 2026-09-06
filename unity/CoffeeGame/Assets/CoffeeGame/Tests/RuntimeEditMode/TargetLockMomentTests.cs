using CoffeeGame.Actors;
using CoffeeGame.Enemies;
using CoffeeGame.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace CoffeeGame.Combat.Tests
{
    public sealed class TargetLockMomentTests
    {
        private GameObject root, actor, enemy, other;
        [SetUp] public void Setup()
        {
            Time.timeScale=1f;
            root=new GameObject("lock-moment-test");actor=new GameObject("owner");
            enemy=new GameObject("enemy");other=new GameObject("other actor");
            enemy.transform.position=Vector3.forward*3f;
            enemy.AddComponent<Health>().Initialize(10);enemy.GetComponent<Health>().SetTeam(DamageTeam.Enemy);
            enemy.AddComponent<CombatEnemy>();
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(root);Object.DestroyImmediate(actor);Object.DestroyImmediate(enemy);Object.DestroyImmediate(other);
            Time.timeScale=1f;
        }
        [Test] public void TargetToggleDeathSwitchPauseAndRangeRelease()
        {
            var motor=actor.AddComponent<PlayerMotor3D>();var lockOn=root.AddComponent<TargetLockController>();
            lockOn.Tick(motor,true,true);Assert.That(lockOn.Target,Is.EqualTo(enemy.GetComponent<Health>()));
            lockOn.Tick(motor,true,false);Assert.That(lockOn.IsLocked,Is.True);
            lockOn.Tick(motor,true,true);Assert.That(lockOn.IsLocked,Is.False);Assert.That(motor.LockedTarget,Is.Null);
            lockOn.Tick(motor,true,true);lockOn.Tick(motor,false,false);Assert.That(lockOn.IsLocked,Is.False);
            lockOn.Tick(motor,true,true);enemy.transform.position=Vector3.forward*25f;
            lockOn.Tick(motor,true,false);Assert.That(lockOn.IsLocked,Is.False);
            enemy.transform.position=Vector3.forward*3f;lockOn.Tick(motor,true,true);
            lockOn.Tick(other.AddComponent<PlayerMotor3D>(),true,false);Assert.That(lockOn.IsLocked,Is.False);Assert.That(motor.LockedTarget,Is.Null);
            lockOn.Tick(motor,true,true);enemy.GetComponent<Health>().SetCurrentAndMaximum(0,10);
            lockOn.Tick(motor,true,false);Assert.That(lockOn.IsLocked,Is.False);
        }
        [Test] public void MomentUsesRealTimeAndRestoresScaleAndColor()
        {
            var moment=root.AddComponent<PerfectDefenseMoment>();
            Assert.That(moment.TryBegin(),Is.True);Assert.That(Time.timeScale,Is.EqualTo(.18f).Within(.001f));
            Assert.That(moment.Desaturation,Is.EqualTo(1f));
            moment.Tick(.3f,true);Assert.That(moment.TryBegin(),Is.False,"duplicate cannot extend duration");
            moment.Tick(.18f,true);Assert.That(Time.timeScale,Is.InRange(.18f,1f));
            moment.Tick(.18f,true);Assert.That(Time.timeScale,Is.EqualTo(1f));Assert.That(moment.Desaturation,Is.Zero);
        }
        [Test] public void PausingDuringMomentNeverUnpausesOrRestartsIt()
        {
            var moment=root.AddComponent<PerfectDefenseMoment>();moment.TryBegin();Time.timeScale=0f;
            moment.Tick(.2f,false);Assert.That(Time.timeScale,Is.Zero);Assert.That(moment.IsActive,Is.False);
            Assert.That(moment.TryBegin(),Is.False);Time.timeScale=1f;moment.Tick(1f,true);Assert.That(Time.timeScale,Is.EqualTo(1f));
        }
        [Test] public void MomentDoesNotCancelSelectiveTimeStop()
        {
            var stop=other.AddComponent<TimeStopController>();
            Assert.That(stop.TryBegin(actor,10f),Is.True);
            var moment=root.AddComponent<PerfectDefenseMoment>();Assert.That(moment.TryBegin(),Is.True);
            moment.Tick(.7f,true);
            Assert.That(stop.IsActive,Is.True);Assert.That(stop.IsFrozen(enemy),Is.True);Assert.That(stop.IsFrozen(actor),Is.False);
        }
    }
}
