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
    public sealed class PartyEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private string directory;
        private readonly List<string> checks = new List<string>();
        public static void Begin(GameObject host, CombatRunController run, string path)
        {
            var capture = host.AddComponent<PartyEvidenceCapture>();
            capture.run = run; capture.directory = Path.GetFullPath(path);
            Directory.CreateDirectory(capture.directory);
            Application.runInBackground = true;
            capture.StartCoroutine(capture.Guard(capture.Run()));
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
                    Debug.LogException(e); Application.Quit(2); yield break;
                }
                yield return current;
            }
        }
        private void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException("Party evidence failed: " + name);
            checks.Add(name);
        }
        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(directory, name + ".png"));

        private IEnumerator Run()
        {
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse, out _), "input profile selected");
            var party = run.Party;
            party.ToggleParticipation(PartyMemberIds.Hero); party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun();
            var hero = party.Actors[PartyMemberIds.Hero]; var cat = party.Actors[PartyMemberIds.CatMage];
            var enemy = run.CurrentEnemy.GetComponent<Health>(); enemy.Initialize(5000);
            yield return new WaitForSeconds(0.6f);
            Check(hero.Targetable && cat.Targetable, "both actors deployed");
            Check(cat.GetComponentInChildren<Animator>() != null, "cat has animated model");
            Capture("01-party");
            var heroPosition = hero.transform.position; var catPosition = cat.transform.position;
            Check(party.RequestSwitch(cat.MemberId), "cat switch accepted");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (party.Active != cat && Time.realtimeSinceStartup < deadline) yield return null;
            Check(party.Active == cat, "cat becomes controlled actor");
            yield return new WaitForSeconds(0.3f);
            party.enabled = false;
            hero.Motor.Commands = default; hero.Combat.Commands = default;
            cat.Motor.Commands = default; cat.Combat.Commands = default;
            cat.Motor.ResetMotor(new Vector3(0.2f, 0.05f, 0));
            hero.Motor.ResetMotor(new Vector3(-1.2f, 0.05f, 0));
            enemy.transform.position = new Vector3(2.5f, 0.05f, 1.5f);
            yield return new WaitForSeconds(1.5f);
            hero.Combat.CancelPendingActions();
            cat.Combat.ResetCombat(); cat.Combat.IsManual = true;
            cat.Resources.GainStamina(cat.Resources.MaxStamina);
            cat.Combat.Commands = new ActorCommandFrame { Special = true };
            yield return null; yield return null;
            cat.Combat.Commands = default;
            var stop = TimeStopController.Instance;
            Check(stop.IsActive && stop.Caster == cat.gameObject, "cat special activates time stop");
            var frozenEnemyPosition = enemy.transform.position; var frozenHeroPosition = hero.transform.position;
            int before = enemy.Current;
            Check(enemy.ApplyDamage(new DamageInfo(2, cat.gameObject, enemy.transform.position, Vector3.zero)), "first frozen hit accepted");
            Check(enemy.ApplyDamage(new DamageInfo(3, cat.gameObject, enemy.transform.position, Vector3.zero)), "second frozen hit accepted");
            Check(enemy.Current == before, "damage deferred while stopped");
            hero.Motor.Commands = new ActorCommandFrame { Move = Vector2.right, WorldSpace = true };
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.left, WorldSpace = true };
            yield return new WaitForSeconds(0.35f);
            cat.Motor.Commands = default;
            Check(enemy.transform.position == frozenEnemyPosition && hero.transform.position == frozenHeroPosition, "enemy and heroine remain frozen");
            Check(cat.transform.position.x < 0.1f, "cat moves during time stop");
            Capture("02-time-stop");
            run.Pause(); yield return null;
            float remaining = stop.Remaining;
            yield return new WaitForSecondsRealtime(0.3f);
            Check(Mathf.Abs(stop.Remaining - remaining) < 0.01f, "pause preserves ten-second timer");
            run.Resume();
            while (stop.IsActive) yield return null;
            Check(enemy.Current == before - 5, "queued damage resolves once on release");
            hero.Motor.Commands = default;
            Capture("03-time-resumed");
            party.enabled = true;
            run.Pause(); party.ToggleParticipation(hero.MemberId);
            Check(!hero.gameObject.activeSelf, "resting actor leaves combat scene");
            party.Snapshot();
            yield return null;
            var hud = FindFirstObjectByType<CoffeeGame.UI.CombatGameHudView>();
            Check(hud != null, "party menu view available");
            hud.SetSelectedTab(CoffeeGame.UI.CharacterMenuTab.Companions);
            hud.RebuildMenuContent(run);
            yield return null;
            Capture("04-party-rest-menu");
            File.WriteAllText(Path.Combine(directory, "checks.json"), JsonUtility.ToJson(new Report { passed = checks.ToArray(), device = SystemInfo.graphicsDeviceName }, true));
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit(0);
        }
        [Serializable] private sealed class Report { public string[] passed; public string device; }
    }
}
#endif
