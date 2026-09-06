#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    public sealed class TargetLockEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private string output;
        private float deadline;
        private readonly List<string> checks = new List<string>();
        public static void Begin(GameObject host, CombatRunController controller, string directory)
        {
            var capture=host.AddComponent<TargetLockEvidenceCapture>();capture.run=controller;capture.output=directory;
            Directory.CreateDirectory(directory);capture.deadline=Time.realtimeSinceStartup+80f;
            capture.StartCoroutine(capture.Guard(capture.Run()));
        }
        private void Check(bool value,string label) { if(!value)throw new InvalidOperationException(label);checks.Add(label); }
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(output,name+".png"));
        private void Report()=>File.WriteAllText(Path.Combine(output,"checks.json"),JsonUtility.ToJson(new Results{passed=checks.ToArray()},true));
        private void Fail(Exception error) {File.WriteAllText(Path.Combine(output,"failure.txt"),error.ToString());Report();Debug.LogException(error);Application.Quit(2);enabled=false;}
        private void Update() {if(output!=null&&Time.realtimeSinceStartup>deadline){StopAllCoroutines();Fail(new TimeoutException("Target-lock capture timeout"));}}
        private IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                object current;
                try {if(!routine.MoveNext())yield break;current=routine.Current;}
                catch(Exception error){Fail(error);yield break;}
                yield return current;
            }
        }
        private IEnumerator Run()
        {
            while(!Application.isFocused)yield return null;
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse,out _),"select memory-only input");
            var party=run.Party;party.ToggleParticipation(PartyMemberIds.Hero);party.ToggleParticipation(PartyMemberIds.CatMage);run.StartNewRun();
            var hero=party.Actors[PartyMemberIds.Hero];var cat=party.Actors[PartyMemberIds.CatMage];
            var enemies=FindObjectsByType<Health>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(h=>h.Team==DamageTeam.Enemy&&h.IsAlive).ToArray();
            Check(enemies.Length==2,"two live enemies retained");
            foreach(var enemy in enemies)
            {
                if(enemy.TryGetComponent<GoblinController>(out var goblin))goblin.enabled=false;
                if(enemy.TryGetComponent<SlimeController>(out var slime))slime.enabled=false;
                enemy.Initialize(1000);
            }
            party.enabled=false;
            foreach(var actor in party.Actors.Values){actor.Motor.Commands=default;actor.Combat.Commands=default;actor.Combat.ResetCombat();}
            hero.Motor.ResetMotor(new Vector3(-.6f,.05f,0));cat.Motor.ResetMotor(new Vector3(1.8f,.05f,1.1f));
            enemies[0].transform.position=new Vector3(-1f,0,-1.8f);enemies[1].transform.position=new Vector3(2.3f,0,-1.5f);
            var lockOn=GetComponent<TargetLockController>();lockOn.AutomaticInput=false;
            var moment=GetComponent<PerfectDefenseMoment>();
            yield return new WaitForSecondsRealtime(2f);
            lockOn.Tick(hero.Motor,true,true);yield return null;
            Check(lockOn.IsLocked&&hero.Motor.HasLockedTarget,"lock selects a live enemy");
            yield return new WaitForEndOfFrame();Capture("01-target-locked");
            hero.Motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};
            yield return new WaitForSecondsRealtime(.3f);hero.Motor.Commands=default;
            Vector3 aim=Vector3.ProjectOnPlane(lockOn.Target.transform.position-hero.transform.position,Vector3.up).normalized;
            Check(Vector3.Dot(hero.Motor.Facing,aim)>.998f,"strafe keeps facing locked target");
            lockOn.Tick(hero.Motor,true,true);Check(!lockOn.IsLocked&&!hero.Motor.HasLockedTarget,"second edge releases lock");
            hero.Motor.FaceTowards(hero.transform.position+Vector3.back);
            hero.Combat.Commands=hero.Motor.Commands=new ActorCommandFrame{GuardHeld=true};yield return new WaitForSecondsRealtime(.25f);
            hero.Motor.Commands=new ActorCommandFrame{GuardHeld=true,Jump=true,Move=new Vector2(1,1).normalized,WorldSpace=true};
            yield return null;yield return null;hero.Combat.Commands=hero.Motor.Commands=default;
            Check(hero.Motor.IsCartwheeling,"rear 45 degree diagonal executes cartwheel");
            yield return new WaitForSecondsRealtime(.26f);yield return new WaitForEndOfFrame();Capture("02-rear-diagonal-cartwheel");
            while(hero.Motor.IsGuardJumping)yield return null;
            hero.Combat.Commands=hero.Motor.Commands=new ActorCommandFrame{GuardHeld=true};yield return new WaitForSecondsRealtime(.2f);
            hero.Motor.Commands=new ActorCommandFrame{GuardHeld=true,Jump=true,Move=new Vector2(.45f,1).normalized,WorldSpace=true};
            yield return null;yield return null;hero.Combat.Commands=hero.Motor.Commands=default;
            Check(hero.Motor.IsBackflipping,"more backward than 45 degrees executes backflip");
            yield return new WaitForSecondsRealtime(.25f);yield return new WaitForEndOfFrame();Capture("03-rear-backflip");
            while(hero.Motor.IsGuardJumping)yield return null;
            hero.Motor.ResetMotor(new Vector3(-.6f,.05f,0));yield return new WaitForSecondsRealtime(.2f);
            var defense=hero.GetComponent<PlayerDefense>();hero.Health.Initialize(100);hero.Health.EvasionChance=0;
            defense.TickGuard(true,true,CombatClock.Time(hero.gameObject));
            Check(!hero.Health.ApplyDamage(new DamageInfo(10,enemies[0].gameObject,hero.transform.position+Vector3.up*.7f,Vector3.zero)),"perfect guard takes no damage");
            Check(moment.IsActive&&Time.timeScale<.2f&&moment.Desaturation>.99f,"actual perfect guard starts gray slow motion");
            yield return new WaitForEndOfFrame();Capture("04-perfect-guard-gray");
            yield return new WaitForSecondsRealtime(.8f);
            Check(!moment.IsActive&&Mathf.Approximately(Time.timeScale,1f)&&moment.Desaturation==0f,"perfect guard recovers normal color and speed");
            yield return new WaitForEndOfFrame();Capture("05-normal-color-restored");
            defense.BeginDodge();hero.Health.BeginDodgeInvulnerability(.5f);
            Check(!hero.Health.ApplyDamage(new DamageInfo(10,enemies[0].gameObject,hero.transform.position,Vector3.zero)),"perfect dodge avoids incoming hit");
            Check(moment.IsActive&&Time.timeScale<.2f,"actual perfect dodge starts slow motion");
            yield return new WaitForEndOfFrame();Capture("06-perfect-dodge-gray");
            run.Pause();Check(Time.timeScale==0f&&!moment.IsActive,"pause cancels moment without unpausing");
            yield return new WaitForSecondsRealtime(.15f);run.Resume();
            Check(Time.timeScale==1f&&!moment.IsActive,"resume restores normal speed");
            hero.Health.EndDodgeInvulnerability();defense.CancelGuard();hero.Health.Initialize(100);
            hero.Health.ApplyDamage(new DamageInfo(2,enemies[0].gameObject,hero.transform.position,Vector3.zero));
            Check(hero.GetComponent<HeroineCombatVoice>().LastPlayedClip=="hurt_01_u","accepted hit plays heroine hurt line");
            Check(!moment.IsActive,"ordinary damage does not start perfect effect");
            var stop=TimeStopController.Instance;
            Check(stop.TryBegin(cat.gameObject,10f),"cat time stop remains available");
            Check(moment.TryBegin(),"brief moment can coexist with selective time stop");
            yield return new WaitForSecondsRealtime(.8f);
            Check(stop.IsActive&&!moment.IsActive&&Time.timeScale==1f,"moment ending preserves cat time stop");
            yield return new WaitForEndOfFrame();Capture("07-cat-time-stop-retained");
            stop.Cancel();lockOn.Tick(hero.Motor,true,true);lockOn.Tick(cat.Motor,true,false);
            Check(!lockOn.IsLocked&&!hero.Motor.HasLockedTarget,"actor switch clears old target");
            lockOn.Tick(hero.Motor,true,true);var target=lockOn.Target;target.SetCurrentAndMaximum(0,1000);lockOn.Tick(hero.Motor,true,false);
            Check(!lockOn.IsLocked,"dead target releases lock");
            Report();yield return new WaitForSecondsRealtime(.3f);Application.Quit(0);
        }
        [Serializable]private sealed class Results{public string[] passed;}
    }
}
#endif
