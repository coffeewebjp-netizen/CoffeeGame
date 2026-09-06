#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;

namespace CoffeeGame.Presentation
{
    public sealed class DefenseEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private string directory;
        private readonly List<string> checks = new List<string>();
        public static void Begin(GameObject host, CombatRunController controller, string output)
        {
            var capture = host.AddComponent<DefenseEvidenceCapture>();
            capture.run = controller; capture.directory = output;
            Directory.CreateDirectory(output); capture.StartCoroutine(capture.Guard(capture.Run()));
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) yield break; current = routine.Current; }
                catch (Exception e)
                {
                    File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString());
                    File.WriteAllText(Path.Combine(directory, "partial-checks.json"), JsonUtility.ToJson(new Report { passed=checks.ToArray(), device=SystemInfo.graphicsDeviceName }, true));
                    Debug.LogException(e); Application.Quit(2); yield break;
                }
                yield return current;
            }
        }
        private void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("Defense evidence: " + name);
            checks.Add(name);
        }
        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));
        private IEnumerator Run()
        {
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse, out _), "input selection accepted");
            var party = run.Party;
            party.ToggleParticipation(PartyMemberIds.Hero); party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun();
            var hero = party.Actors[PartyMemberIds.Hero]; var cat = party.Actors[PartyMemberIds.CatMage];
            var enemy = run.CurrentEnemy.GetComponent<Health>(); var goblin = enemy.GetComponent<GoblinController>();
            goblin.enabled = false; enemy.Initialize(1000);
            party.enabled = false;
            foreach (var actor in party.Actors.Values) { actor.Motor.Commands = default; actor.Combat.Commands = default; actor.Combat.ResetCombat(); }
            hero.Motor.ResetMotor(new Vector3(-0.65f, 0.05f, 0));
            cat.Motor.ResetMotor(new Vector3(0.8f, 0.05f, 0));
            enemy.transform.position = new Vector3(-0.65f, 0, -1f);
            yield return new WaitForSeconds(0.6f);
            hero.Motor.FaceTowards(enemy.transform.position); cat.Motor.FaceTowards(enemy.transform.position);
            hero.Motor.enabled = false; cat.Motor.enabled = false;
            hero.Combat.enabled = false; cat.Combat.enabled = false;
            hero.Combat.Commands = new ActorCommandFrame { GuardHeld = true };
            cat.Combat.Commands = new ActorCommandFrame { GuardHeld = true };
            var defense = hero.GetComponent<PlayerDefense>(); var catDefense = cat.GetComponent<PlayerDefense>();
            Check(hero.GetComponent<DefensePosePresentation>().HasPoseRig && cat.GetComponent<DefensePosePresentation>().HasPoseRig,
                "actual generic character skeletons resolved for poses");
            Check(hero.GetComponent<DefensePosePresentation>().HasSwordAxis, "actual katana blade axis resolved");
            yield return new WaitForSeconds(0.45f);
            Check(defense.IsGuarding && catDefense.IsGuarding, "both character-specific guards active");
            Capture("01-guard-poses"); yield return new WaitForSeconds(0.15f);
            hero.Health.IncomingDamageMultiplier = 1f; hero.Health.Initialize(100);
            Check(hero.Health.ApplyDamage(new DamageInfo(30, enemy.gameObject, hero.transform.position + Vector3.up * .75f, Vector3.back)), "held guard registers chip");
            Check(hero.Health.Current == 97, "guard reduces 30 damage to 3");
            Capture("02-blade-block"); yield return new WaitForSeconds(0.8f);
            hero.Combat.Commands = default; cat.Combat.Commands = default;
            yield return new WaitForSeconds(0.4f);
            for (int i=0; i<200 && !goblin.IsWindingUp; i++) goblin.Tick(.01f);
            goblin.Tick(GoblinController.WindupSeconds + .01f);
            hero.Combat.Commands = new ActorCommandFrame { GuardHeld = true };
            yield return null; yield return null;
            int hp = hero.Health.Current;
            goblin.Tick(GoblinController.ImpactSeconds + .01f);
            Check(hero.Health.Current == hp && goblin.Phase == GoblinController.CombatPhase.Parried, "parry staggers attacker with no damage");
            Capture("03-parry"); yield return new WaitForSeconds(.25f);
            Vector3 p = goblin.transform.position; goblin.Tick(1.4f);
            Check(goblin.transform.position == p && goblin.Phase == GoblinController.CombatPhase.Parried, "stun persists 1.4 seconds");
            goblin.Tick(.11f); Check(goblin.Phase == GoblinController.CombatPhase.Approach, "stun ends after 1.5 seconds");
            hero.Combat.Commands = default; yield return null;
            for (int i=0; i<200 && !goblin.IsWindingUp; i++) goblin.Tick(.01f);
            goblin.Tick(GoblinController.WindupSeconds + .01f);
            hero.Resources.TrySpendStamina(hero.Resources.MaxStamina);
            hero.Motor.enabled = true;
            hero.Motor.Commands = new ActorCommandFrame { Dodge = true };
            yield return null; yield return null;
            hero.Motor.Commands = default;
            Check(hero.Motor.IsDodging, "physical dodge starts through actor commands");
            goblin.Tick(GoblinController.ImpactSeconds + .01f);
            Check(hero.Resources.Stamina == hero.Resources.MaxStamina, "perfect dodge fills special gauge");
            Capture("04-perfect-dodge"); yield return null; yield return null;
            hero.Resources.TrySpendStamina(hero.Resources.MaxStamina); goblin.Tick(.02f);
            Check(hero.Resources.Stamina == 0, "same strike cannot fill gauge twice");
            while (!hero.Motor.IsGrounded) yield return null;
            hero.Motor.enabled = false;
            yield return new WaitForSeconds(.5f);
            hero.Health.EndDodgeInvulnerability();
            enemy.transform.position = hero.transform.position + Vector3.back * .85f;
            for (int i=0; i<200 && !goblin.IsWindingUp; i++) goblin.Tick(.01f);
            Check(goblin.IsWindingUp, "enemy counter window opens");
            int before = enemy.Current;
            enemy.ApplyDamage(new DamageInfo(7, hero.gameObject, enemy.transform.position + Vector3.up*.6f, Vector3.zero));
            Check(enemy.Current == before - 14, "counter doubles 7 damage to 14");
            Capture("05-counter"); yield return new WaitForSeconds(.5f);
            enemy.ApplyDamage(new DamageInfo(7, hero.gameObject, enemy.transform.position, Vector3.zero));
            Check(enemy.Current == before - 21, "interrupted enemy takes normal follow-up");
            hero.Motor.enabled = true; hero.Combat.enabled = true; hero.Combat.ResetCombat();
            hero.Motor.ResetMotor(new Vector3(-.65f, .05f, 0));
            enemy.transform.position = new Vector3(-.65f, .05f, -.9f);
            yield return new WaitForSeconds(.3f);
            hero.Motor.Commands = new ActorCommandFrame { Jump = true };
            yield return null; yield return null; hero.Motor.Commands = default;
            yield return new WaitForSeconds(.22f);
            hero.Motor.Commands = new ActorCommandFrame { Move = Vector2.down };
            float deadline=Time.realtimeSinceStartup+1f;
            while (!hero.Motor.IsPlunging && Time.realtimeSinceStartup<deadline) yield return null;
            Check(hero.Motor.IsPlunging, "airborne down starts heroine plunge");
            yield return new WaitForSeconds(.04f);
            yield return new WaitForEndOfFrame();
            Check(Vector3.Dot(hero.GetComponent<DefensePosePresentation>().MeasureBladeWorldDirection(), Vector3.down) > .94f,
                "rendered katana points down during plunge");
            Capture("06-downward-blade");
            hero.Motor.Commands = default; before = enemy.Current;
            while (!hero.Motor.IsGrounded) yield return null;
            Check(enemy.Current < before, "plunge contact damages nearby enemy");
            int landedHp = enemy.Current;
            Capture("07-plunge-shockwave"); yield return new WaitForSeconds(.18f);
            Check(enemy.Current == landedHp, "shockwave damages once per landing");
            Capture("08-plunge-ring"); yield return new WaitForSeconds(.5f);
            Check(!cat.Motor.CanPlunge, "cat does not use heroine sword plunge");
            File.WriteAllText(Path.Combine(directory,"checks.json"), JsonUtility.ToJson(new Report { passed=checks.ToArray(), device=SystemInfo.graphicsDeviceName },true));
            yield return new WaitForSeconds(.3f); Application.Quit(0);
        }
        [Serializable] private sealed class Report { public string[] passed; public string device; }
    }
}
#endif
