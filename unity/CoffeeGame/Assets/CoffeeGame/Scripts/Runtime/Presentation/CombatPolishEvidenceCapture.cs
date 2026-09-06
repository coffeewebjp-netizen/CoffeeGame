#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CoffeeGame.Actors;
using CoffeeGame.Audio;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Run;
using CoffeeGame.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CoffeeGame.Presentation
{
    public sealed class CombatPolishEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private string output;
        private float deadline;
        private readonly List<string> checks = new List<string>();
        public static void Begin(GameObject host, CombatRunController controller, string directory)
        {
            var capture = host.AddComponent<CombatPolishEvidenceCapture>(); capture.run = controller; capture.output = directory;
            Directory.CreateDirectory(directory); capture.deadline = Time.realtimeSinceStartup + 100f;
            capture.StartCoroutine(capture.Guard(capture.Run()));
        }
        private void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        private void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
        private void Report() => File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(new Results { passed = checks.ToArray() }, true));
        private void Fail(Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Report(); Debug.LogException(error); Application.Quit(2); enabled = false; }
        private void Update() { if (output != null && Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Fail(new TimeoutException("Combat polish capture timeout")); } }
        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try { if (!routine.MoveNext()) yield break; current = routine.Current; }
                catch (Exception error) { Fail(error); yield break; }
                yield return current;
            }
        }
        private IEnumerator Run()
        {
            while (!Application.isFocused) yield return null;
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse, out _), "memory-only profile and input");
            var party = run.Party;
            Check(!run.Progression.IsRivalRecruited(RivalCharacterIds.WeaknessChallenger), "diagnostic starts before cat recruitment");
            party.ToggleParticipation(PartyMemberIds.Hero); run.StartNewRun(); run.Pause();
            yield return null;
            var hud = FindFirstObjectByType<CombatSliceHud>();
            typeof(CombatSliceHud).GetMethod("HandlePointerTab", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(hud, new object[] { CharacterMenuTab.System });
            yield return null;
            var view = FindFirstObjectByType<CombatGameHudView>();
            // Recruitment rebuilds this list synchronously. Old UI objects wait
            // until end-of-frame for destruction, so query the live row list.
            Button DebugButton(int index) => ((List<Button>)typeof(CombatGameHudView)
                .GetField("controlButtons", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view))[22 + index];
            DebugButton(3).onClick.Invoke();
            Check(run.Progression.GetRivalAffinity(RivalCharacterIds.WeaknessChallenger) == 100 && party.Actors.ContainsKey(PartyMemberIds.CatMage), "settings button recruits cat through live party");
            var catMember = party.State.Find(PartyMemberIds.CatMage);
            DebugButton(0).onClick.Invoke(); Check(run.Progression.GetRivalAffinity(RivalCharacterIds.WeaknessChallenger) == 90, "settings can lower affinity");
            DebugButton(2).onClick.Invoke(); DebugButton(1).onClick.Invoke();
            Check(run.Progression.GetRivalAffinity(RivalCharacterIds.WeaknessChallenger) == 10 && party.State.Find(PartyMemberIds.CatMage) == catMember, "zero and raise preserve recruited member");
            DebugButton(3).onClick.Invoke(); DebugButton(7).onClick.Invoke(); DebugButton(4).onClick.Invoke();
            Check(run.Progression.GetRivalAffinity(RivalCharacterIds.SplitInk) == 90 && run.Progression.Gold == 0 && run.Progression.ClaimedRewardCount == 0, "dragon affinity independent without learning rewards");
            Check(RivalCharacterIds.EncounterCandidates(run.Progression.IsRivalRecruited)[0] == RivalCharacterIds.SplitInk, "cat recruitment advances rival to dragon");
            view.SetSelectedControlRow(29); yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame(); Capture("01-debug-affinity-settings");
            run.Resume(); yield return null;
            var hero = party.Actors[PartyMemberIds.Hero];
            var enemies = FindObjectsByType<Health>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(h => h.Team == DamageTeam.Enemy && h.IsAlive).ToArray();
            Check(enemies.Length == 2, "two monsters retained");
            foreach (var enemy in enemies)
            {
                if (enemy.TryGetComponent<GoblinController>(out var g)) g.enabled = false;
                if (enemy.TryGetComponent<SlimeController>(out var s)) s.enabled = false;
                enemy.Initialize(1000); enemy.transform.position = new Vector3(enemy == enemies[0] ? -2 : 3, 0, -3);
            }
            party.enabled = false;
            hero.Motor.Commands = default; hero.Combat.Commands = default; hero.Combat.ResetCombat();
            hero.Motor.ResetMotor(new Vector3(0, .05f, 0));
            var lockOn = GetComponent<TargetLockController>(); lockOn.AutomaticInput = false;
            var moment = GetComponent<PerfectDefenseMoment>();
            yield return new WaitForSecondsRealtime(.3f);
            lockOn.Tick(hero.Motor, true, true);
            hero.Motor.Commands = new ActorCommandFrame { Move = Vector2.right, WorldSpace = true };
            yield return new WaitForSecondsRealtime(.9f);
            Check(hero.Motor.IsRunning && hero.Motor.Facing.x > .99f && lockOn.IsLocked, "locked run faces travel while keeping target");
            yield return new WaitForEndOfFrame(); Capture("02-locked-running");
            hero.Combat.Commands = new ActorCommandFrame { Sword = true }; yield return null; yield return null; hero.Combat.Commands = default;
            Vector3 aim = Vector3.ProjectOnPlane(lockOn.Target.transform.position - hero.transform.position, Vector3.up).normalized;
            Check(Vector3.Dot(hero.Motor.Facing, aim) > .995f, "attack while running turns to locked enemy");
            hero.Motor.Commands = default; yield return new WaitForSecondsRealtime(.4f);
            aim = Vector3.ProjectOnPlane(lockOn.Target.transform.position - hero.transform.position, Vector3.up).normalized;
            Check(!hero.Motor.IsRunning && Vector3.Dot(hero.Motor.Facing, aim) > .995f, "stopping faces locked enemy");
            lockOn.Tick(hero.Motor, true, true);
            var voice = hero.GetComponent<SpecialCombatVoice>();
            Check(voice.HasClip && voice.ResourcePath.EndsWith("Heroine/special_01"), "accepted finisher loaded for heroine");
            hero.Resources.TrySpendStamina(hero.Resources.Stamina);
            hero.Combat.Commands = new ActorCommandFrame { Special = true }; yield return null; yield return null; hero.Combat.Commands = default;
            Check(!hero.Combat.IsCharging && voice.PlaybackCount == 0, "insufficient gauge does not play finisher");
            hero.Resources.GainStamina(hero.Resources.MaxStamina);
            hero.Combat.Commands = new ActorCommandFrame { Special = true }; yield return null; yield return null; hero.Combat.Commands = default;
            Check(hero.Combat.IsCharging, "real special input begins charge");
            hero.Combat.ResetCombat(); yield return new WaitForSecondsRealtime(.9f);
            Check(voice.PlaybackCount == 0, "cancelled charge does not play finisher");
            hero.Resources.GainStamina(hero.Resources.MaxStamina);
            hero.Combat.Commands = new ActorCommandFrame { Special = true }; yield return null; yield return null; hero.Combat.Commands = default;
            while (voice.PlaybackCount == 0) yield return null;
            Check(voice.PlaybackCount == 1 && voice.IsSpeaking, "successful special release plays finisher exactly once");
            yield return new WaitForEndOfFrame(); Capture("03-finisher-release");
            yield return new WaitForSecondsRealtime(1.7f); hero.Combat.ResetCombat();
            Check(voice.PlaybackCount == 1, "finisher does not repeat on held release");

            var goblin = enemies.Select(h => h.GetComponent<GoblinController>()).FirstOrDefault(g => g != null);
            Check(goblin != null, "actual encounter contains goblin controller");
            goblin.transform.position = Vector3.zero;
            hero.Motor.ResetMotor(new Vector3(0, .05f, 1)); hero.Health.Initialize(100); hero.Health.EvasionChance = 0;
            hero.Resources.TrySpendStamina(hero.Resources.Stamina);
            yield return new WaitForSecondsRealtime(.3f);
            goblin.Initialize(Resources.Load<CombatTuning>("Data/FirstCombatTuning"), hero.transform, hero.Health, goblin.GetComponent<Health>(), goblin.GetComponent<Collider>(), null, 0);
            goblin.Tick(.71f); goblin.Tick(GoblinController.WindupSeconds + .01f);
            Check(goblin.Phase == GoblinController.CombatPhase.Strike, "real goblin commits incoming swing");
            hero.Motor.Commands = new ActorCommandFrame { Dodge = true, Move = Vector2.up, WorldSpace = true };
            yield return null; yield return null; hero.Motor.Commands = default;
            yield return new WaitForSecondsRealtime(.20f);
            Check(hero.Motor.IsDodging && !goblin.Threatens(hero.transform.position), "physical roll escapes strike volume");
            goblin.Tick(GoblinController.ImpactSeconds + .01f);
            Check(hero.Resources.Stamina == hero.Resources.MaxStamina && hero.Health.Current == 100, "real escaped attack fills gauge without damage");
            Check(moment.IsActive && moment.Desaturation > .99f && Time.timeScale < .2f, "real enemy impact starts grayscale slowdown");
            yield return new WaitForEndOfFrame(); Capture("04-real-perfect-dodge-gray");
            yield return new WaitForSecondsRealtime(.8f);
            Check(!moment.IsActive && moment.Desaturation == 0 && Time.timeScale == 1, "real perfect dodge returns to normal color and speed");
            yield return new WaitForEndOfFrame(); Capture("05-dodge-color-restored");
            while (hero.Motor.IsDodging) yield return null;
            var tuning = Resources.Load<CombatTuning>("Data/FirstCombatTuning");
            Check(tuning.JustDodgeSeconds == .30f && tuning.DodgeInvulnerabilitySeconds > .60f, "shipped tuning has wider finite windows");
            Report(); yield return new WaitForSecondsRealtime(.3f); Application.Quit(0);
        }
        [Serializable] private sealed class Results { public string[] passed; }
    }
}
#endif
