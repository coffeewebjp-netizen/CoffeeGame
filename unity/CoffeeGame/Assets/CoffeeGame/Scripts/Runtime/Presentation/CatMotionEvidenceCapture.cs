#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Domain;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    public sealed class CatMotionEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run; private string output; private float deadline;
        private PartyActor cat; private ModelCharacterVisual visual;
        private readonly List<string> checks = new List<string>();
        public static void Begin(GameObject host, CombatRunController run, string output)
        {
            var capture = host.AddComponent<CatMotionEvidenceCapture>(); capture.run = run; capture.output = output;
            Application.runInBackground = true; Directory.CreateDirectory(output);
            capture.deadline = Time.realtimeSinceStartup + 160;
            capture.StartCoroutine(capture.Guard(capture.Run()));
        }
        private void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        private void Report() => File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(new Result { passed = checks.ToArray() }, true));
        private void Fail(Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Report(); Debug.LogException(error); Application.Quit(2); enabled = false; }
        private void Update() { if (Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Fail(new TimeoutException("Cat motion evidence timeout")); } }
        private IEnumerator Guard(IEnumerator steps)
        {
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) yield break; next = steps.Current; }
                catch (Exception error) { Fail(error); yield break; }
                yield return next;
            }
        }
        private bool State(string name)
        {
            var a = visual.Animator;
            return a.GetCurrentAnimatorStateInfo(0).IsName(name) || a.IsInTransition(0) && a.GetNextAnimatorStateInfo(0).IsName(name);
        }
        private void Capture(string name, bool side = false)
        {
            var camera = Camera.main;
            Vector3 center = cat.transform.position + Vector3.up * .65f;
            Vector3 view = side ? Vector3.Cross(Vector3.up, cat.Motor.Facing) : cat.Motor.Facing;
            if(name.Contains("strike-detail")||name=="20-earth-crouch-detail") view=-view;
            camera.transform.position = center + view * 2.7f + Vector3.up * .28f;
            camera.transform.LookAt(center); camera.fieldOfView = 34; camera.orthographicSize = .92f;
            if (name.Contains("thunder") || name == "20-earth-wave")
            {
                camera.transform.position = center + view * 7 + Vector3.up * 5;
                camera.transform.LookAt(center); camera.orthographicSize = 4;
            }
            else if (name.StartsWith("15-time"))
            {
                camera.transform.position = center + view * 6 + Vector3.up * .5f;
                camera.transform.LookAt(center); camera.orthographicSize = 2.2f;
            }
            camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 1;
            var target = new RenderTexture(900, 900, 24); target.Create(); var previous = RenderTexture.active;
            var pixels = new Texture2D(900, 900, TextureFormat.RGB24, false);
            try
            {
                var effect = camera.GetComponent<TimeStopWorldEffect>();
                if (effect != null && effect.IsCompositing) effect.CaptureComposite(target);
                else RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Destroy(target); Destroy(pixels); }
        }
        // Supplement the unmodified battle view with pose details. Hide only
        // enemy renderers for this single render; simulation and damage stay live.
        private void CapturePoseDetail(string name)
        {
            var renderers=FindObjectsByType<Health>().Where(h=>h.Team==DamageTeam.Enemy)
                .SelectMany(h=>h.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled).Distinct().ToArray();
            try { foreach(var r in renderers) r.enabled=false; Capture(name,true); }
            finally { foreach(var r in renderers) if(r!=null) r.enabled=true; }
        }
        private IEnumerator Run()
        {
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse, out _), "memory-only input/profile");
            var party = run.Party;
            party.ToggleParticipation(PartyMemberIds.Hero); party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun(); yield return null;
            Check(party.RequestSwitch(PartyMemberIds.CatMage), "cat can become controlled actor");
            cat = party.Actors[PartyMemberIds.CatMage]; var hero = party.Actors[PartyMemberIds.Hero];
            while (party.Active != cat) yield return null;
            visual = cat.GetComponentInChildren<ModelCharacterVisual>();
            Check(visual.Animator.runtimeAnimatorController.name == "SilverCatElementsV17", "live factory uses V17 cat controller");
            Check(cat.Motor.CanPlunge && cat.Combat.IsCatMage, "cat identity and earth plunge enabled");
            foreach (var enemy in FindObjectsByType<Health>(FindObjectsInactive.Exclude).Where(h => h.Team == DamageTeam.Enemy))
            {
                if (enemy.TryGetComponent<GoblinController>(out var goblin)) goblin.enabled = false;
                if (enemy.TryGetComponent<SlimeController>(out var slime)) slime.enabled = false;
                enemy.transform.position = new Vector3(10, 0, 10);
            }
            party.enabled = false; hero.Motor.Commands = default; hero.Combat.Commands = default;
            cat.Motor.Commands = default; cat.Combat.Commands = default;
            cat.Combat.ResetCombat(); hero.Combat.ResetCombat();
            hero.Motor.ResetMotor(new Vector3(-5, .05f, 3));
            cat.Combat.IsManual = true; cat.Motor.ResetMotor(new Vector3(0, .05f, 0));
            var cameraRig = FindAnyObjectByType<FixedCameraRig>(); cameraRig.enabled = false;
            yield return new WaitForSeconds(1f); Capture("01-idle");
            Check(State("Idle"), "idle returns at rest");
            var bones = visual.ModelRoot.GetComponentsInChildren<Transform>();
            var head = bones.First(t => t.name == "Head"); var hips = bones.First(t => t.name == "Hips");
            // The requested state length must be used even when the previous clip is a run.
            cat.Motor.enabled = false;
            foreach (CharacterAction from in new[] { CharacterAction.Idle, CharacterAction.Run })
            {
                visual.ResetState(Vector3.back); visual.SetLocomotion(from, from == CharacterAction.Run ? 1 : 0);
                yield return new WaitForSeconds(.2f);
                visual.PlayAction(CharacterAction.MagicCharge, 1.2f);
                Check(Mathf.Abs(visual.Animator.speed - 1f) < .015f, "charge timing independent of " + from);
                yield return new WaitForSeconds(.3f);
            }
            visual.ResetState(Vector3.back); cat.Motor.enabled = true;
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.down * .35f, WorldSpace = true };
            yield return new WaitForSeconds(.5f); Capture("02-walk", true); Check(State("Walk"), "walk has its own clip");
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.down, WorldSpace = true };
            while (!cat.Motor.IsRunning) yield return null;
            yield return new WaitForSeconds(.1f); Capture("03-run", true); Check(State("Run"), "run accepted by actual motor");
            for(int phase=1;phase<=3;phase++)
            { yield return new WaitForSeconds(.135f); Capture("03-run-phase-"+phase,true); }
            cat.Motor.Commands = default; cat.Motor.ResetMotor(new Vector3(0, .05f, 0)); yield return new WaitForSeconds(.3f);
            cat.Motor.Commands = new ActorCommandFrame { Jump = true }; yield return null; yield return null; cat.Motor.Commands = default;
            yield return new WaitForSeconds(.16f); Capture("04-jump", true); Check(!cat.Motor.IsGrounded && State("Jump"), "ascending jump pose");
            while (cat.Motor.VerticalSpeed >= 0) yield return null;
            yield return new WaitForSeconds(.06f); Capture("05-fall", true); Check(State("Fall"), "descending fall pose");
            while (!cat.Motor.IsGrounded) yield return null;
            yield return new WaitForSeconds(.04f); Capture("06-land", true); Check(State("Land"), "dedicated grounded landing pose");
            yield return new WaitForSeconds(.45f);
            cat.Resources.SetCurrentAndMaximum(cat.Resources.MaxMagicPoints, cat.Resources.Stamina,
                cat.Resources.MaxMagicPoints, cat.Resources.MaxStamina, cat.Resources.MagicRegenPerSecond);
            float mp = cat.Resources.MagicPoints;
            for (int stage = 1; stage <= 3; stage++)
            {
                cat.Combat.Commands = new ActorCommandFrame { Sword = true }; yield return null; yield return null; cat.Combat.Commands = default;
                yield return new WaitForSeconds(.06f); Capture("07-volley-" + stage);
                Check(State("CatVolley" + stage), "normal magic stage " + stage + " uses distinct hand gesture");
                Check(FindObjectsByType<CatElementProjectile>().Any(p => p.Element == CatProjectileElement.Fire), "volley " + stage + " creates fire projectiles");
                yield return new WaitForSeconds(.72f);
            }
            Check(cat.Resources.MagicPoints == mp, "normal magic still costs no MP");
            cat.Combat.Commands = new ActorCommandFrame { Magic = true }; yield return null; yield return null; cat.Combat.Commands = default;
            yield return new WaitForSeconds(.8f); Capture("08-charge"); Check(State("MagicCharge"), "major charge retains dedicated motion");
            yield return new WaitForSeconds(.42f); Capture("09-major-release"); Check(State("MagicRelease"), "major release differs from volley");
            yield return new WaitForSeconds(.8f);
            cat.Motor.Commands = new ActorCommandFrame { Dodge = true, Move = Vector2.right, WorldSpace = true };
            yield return null; yield return null; cat.Motor.Commands = default;
            while (cat.Motor.IsDodging && cat.Motor.AcrobaticProgress < .45f) yield return null;
            Capture("10-ground-roll", true); Check(cat.Motor.IsDodging && !cat.Motor.IsRunningDodge, "stationary dodge retains grounded roll");
            while (cat.Motor.IsDodging) yield return null;
            cat.Motor.ResetMotor(new Vector3(0, .05f, 0)); yield return new WaitForSeconds(.3f);
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.down, WorldSpace = true }; yield return new WaitForSeconds(.7f);
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.down, Dodge = true, WorldSpace = true }; yield return null; yield return null; cat.Motor.Commands = default;
            while (cat.Motor.IsDodging && cat.Motor.AcrobaticProgress < .47f) yield return null;
            Capture("11-running-spin", true);
            Check(cat.Motor.IsRunningDodge && !cat.Motor.UsesRunningSpinFallback && head.position.y < hips.position.y, "authored running dodge turns upside down without double rotation");
            while (cat.Motor.IsDodging) yield return null;
            foreach (Vector2 direction in new[] { Vector2.right, Vector2.up })
            {
                cat.Motor.ResetMotor(new Vector3(0, .05f, 0)); yield return new WaitForSeconds(.3f);
                cat.Combat.Commands = cat.Motor.Commands = new ActorCommandFrame { GuardHeld = true }; yield return new WaitForSeconds(.2f);
                Capture("12-guard");
                cat.Motor.Commands = new ActorCommandFrame { GuardHeld = true, Jump = true, Move = direction, WorldSpace = true };
                yield return null; yield return null; cat.Combat.Commands = cat.Motor.Commands = default;
                while (cat.Motor.IsGuardJumping && cat.Motor.AcrobaticProgress < .44f) yield return null;
                Capture(direction == Vector2.right ? "13-cartwheel" : "14-backflip", true);
                Check(direction == Vector2.right ? cat.Motor.IsCartwheeling : cat.Motor.IsBackflipping, "guard jump direction " + direction);
                while (cat.Motor.IsGuardJumping) yield return null;
            }
            yield return new WaitForSeconds(.4f);
            yield return Guard(ElementSequence(hero));
            cat.Resources.GainStamina(cat.Resources.MaxStamina);
            cat.Combat.Commands = new ActorCommandFrame { Special = true }; yield return null; yield return null; cat.Combat.Commands = default;
            yield return new WaitForSeconds(.06f); Capture("15-time-turn-start");
            yield return new WaitForSeconds(.24f); Capture("15-time-turn-middle");
            var stop = TimeStopController.Instance;
            Check(stop.IsActive && stop.Remaining > 9 && State("CatTimeStop") && !stop.IsFrozen(cat.gameObject) && stop.IsFrozen(hero.gameObject), "ten second stop uses dedicated gesture and selective freeze");
            yield return new WaitForSeconds(.4f); Capture("15-time-stop");
            var worldEffect = Camera.main.GetComponent<TimeStopWorldEffect>();
            Check(worldEffect.BackgroundVerticalScale < -.99f && head.position.y > hips.position.y, "background completes flip while cat remains upright");
            Vector3 frozenHero = hero.transform.position;
            var reader=FindAnyObjectByType<GameInputReader>();
            var touchOverlays=FindObjectsByType<CoffeeGame.UI.OnScreenTouchControls>().Where(t=>t.enabled).ToArray();
            foreach(var overlay in touchOverlays) overlay.enabled=false;
            reader.BeginInputModeSelection();
            Check(reader.TrySelectInputMode(InputMode.TouchOnScreen,out _),"time stop test enables real touch input route");
            reader.EnableBattle();reader.SetTouchMove(Vector2.zero);reader.RefreshContextSwitchReleaseGate();party.enabled=true;
            yield return null;
            foreach(var direction in new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right})
            {
                reader.SetTouchMove(direction);yield return null;yield return null;
                Check(Vector2.Distance(cat.Motor.Commands.Move,direction)<.001f && Vector2.Distance(cat.Combat.Commands.Move,direction)<.001f,"time stop preserves human direction "+direction);
            }
            reader.SetTouchMove(Vector2.up);yield return null;
            Vector3 beforeInput=cat.transform.position;
            Vector3 expected=Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized;
            yield return new WaitForSeconds(.15f);
            Check(Vector3.Dot(cat.transform.position-beforeInput,expected)>.015f,"up input moves camera-forward during full background flip");
            reader.SetTouchMove(Vector2.zero);party.enabled=false;
            reader.BeginInputModeSelection();reader.TrySelectInputMode(InputMode.KeyboardMouse,out _);reader.EnableBattle();
            foreach(var overlay in touchOverlays) overlay.enabled=true;
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.right, WorldSpace = true };
            yield return new WaitForSeconds(.3f); cat.Motor.Commands = default;
            Check(hero.transform.position == frozenHero && !stop.IsFrozen(cat.gameObject), "only caster moves in stopped world");
            stop.Advance(10);
            yield return new WaitForSeconds(.28f); Capture("15-time-return-middle");
            yield return new WaitForSeconds(.5f);
            Check(!worldEffect.IsCompositing && Camera.main.targetTexture == null, "animated completion restores normal camera target");
            worldEffect.SetActive(true); worldEffect.Advance(.2f); worldEffect.enabled = false;
            Check(!worldEffect.IsCompositing && Camera.main.targetTexture == null, "actual player OnDisable restores interrupted compositor");
            worldEffect.enabled = true;
            cat.Motor.enabled = false; visual.PlayAction(CharacterAction.MagicCharge, 1.2f); yield return new WaitForSeconds(.2f);
            run.Pause(); yield return null; Quaternion pose = head.rotation;
            yield return new WaitForSecondsRealtime(.25f); Check(Quaternion.Angle(pose, head.rotation) < .01f, "pause freezes presentation"); run.Resume();
            visual.ResetState(Vector3.back); cat.Motor.enabled = true; cat.Combat.ResetCombat(); yield return new WaitForSeconds(.4f);
            Check(party.RequestSwitch(PartyMemberIds.Hero), "switch back to heroine after cat actions");
            party.enabled = true; yield return new WaitForSeconds(.8f);
            Check(party.Active == hero && visual.Animator.runtimeAnimatorController.name == "SilverCatElementsV17" && !cat.Combat.IsManual, "AI cat uses same motion controller");
            party.enabled = false; cat.Motor.Commands = cat.Combat.Commands = default;
            cat.Motor.enabled = false; visual.ResetState(Vector3.back); visual.PlayAction(CharacterAction.Defeated, .6f);
            yield return new WaitForSeconds(.95f); Capture("16-defeated", true); Check(State("Defeated"), "defeat settles into separate kneeling pose");
            visual.ResetState(Vector3.back); yield return new WaitForSeconds(.1f); Capture("17-recovered");
            Check(State("Idle") && Mathf.Abs(visual.Animator.speed - 1f) < .01f, "reset restores idle and normal speed");
            Report(); Application.Quit(0);
        }
        [Serializable] private sealed class Result { public string[] passed; }

        private IEnumerator ElementSequence(PartyActor hero)
        {
            cat.Combat.ResetCombat(); cat.Motor.ResetMotor(new Vector3(0, .05f, 0));
            yield return new WaitForSeconds(.4f);
            cat.Motor.Commands = new ActorCommandFrame { Jump = true };
            yield return null; yield return null; cat.Motor.Commands = default;
            yield return new WaitForSeconds(.15f);
            cat.Combat.Commands = new ActorCommandFrame { Sword = true };
            yield return null; yield return null; cat.Combat.Commands = default;
            yield return new WaitForSeconds(.09f); Capture("18-air-wind", true);
            Check(!cat.Motor.IsGrounded && State("AirSlash"), "air attack uses airborne wind casting pose");
            Check(FindObjectsByType<CatElementProjectile>().Any(p => p.Element == CatProjectileElement.Wind), "air attack releases piercing wind instead of fire");
            while (!cat.Motor.IsGrounded) yield return null;
            yield return new WaitForSeconds(.6f);
            cat.Combat.ResetCombat(); cat.Motor.ResetMotor(new Vector3(0, .05f, 0));
            var enemies = FindObjectsByType<Health>(FindObjectsInactive.Exclude).Where(h => h.Team == DamageTeam.Enemy).Take(2).ToArray();
            Check(enemies.Length == 2, "two enemies available for radial element damage");
            for (int i = 0; i < enemies.Length; i++) { enemies[i].Initialize(500); enemies[i].transform.position = new Vector3(i == 0 ? -2 : 2, .05f, 0); }
            Physics.SyncTransforms(); yield return new WaitForSeconds(.3f);
            cat.Motor.Commands = new ActorCommandFrame { Jump = true };
            yield return null; yield return null; cat.Motor.Commands = default;
            yield return new WaitForSeconds(.2f);
            cat.Motor.Commands = new ActorCommandFrame { Move = Vector2.down, WorldSpace = true };
            yield return null; yield return null;
            Check(cat.Motor.IsPlunging, "jump plus down starts cat earth drop");
            // Coroutine Update precedes Animator evaluation. Inspect the pose
            // rendered this frame, not the outgoing jump from the previous frame.
            yield return new WaitForEndOfFrame();
            while(cat.Motor.IsPlunging && visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime<.12f)
                yield return new WaitForEndOfFrame();
            var punchBones=visual.ModelRoot.GetComponentsInChildren<Transform>();
            var punch=punchBones.First(b=>b.name=="RightHand").position-punchBones.First(b=>b.name=="RightArm").position;
            Debug.Log("CAT_IMPACT direction="+punch.normalized+" clipTime="+visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime+" speed="+visual.Animator.speed+" grounded="+cat.Motor.IsGrounded);
            CapturePoseDetail("19-earth-contact-check");
            Check(!cat.Motor.IsGrounded&&Vector3.Dot(punch.normalized,Vector3.down)>.85f,"right arm strikes downward before low-jump ground contact");
            Capture("19-earth-drop", true); cat.Motor.Commands = default;
            for(int frame=0;frame<3;frame++)
            { yield return null; if(cat.Motor.IsPlunging) CapturePoseDetail("19-earth-strike-detail-"+frame); }
            while (!cat.Motor.IsGrounded) yield return null;
            yield return new WaitForSeconds(.14f); Capture("20-earth-crouch", true);
            CapturePoseDetail("20-earth-crouch-detail");
            Check(State("CatEarthLand") && !cat.Motor.CanAct, "earth impact uses deep crouch and recovery lock");
            Check(enemies.All(e => e.Current < 500), "earth ripple damages enemies on both sides");
            Check(FindObjectsByType<CatElementVfx>().Any(f => f.Kind == CatElementEffect.EarthWave && f.Radius == 2.6f), "earth wave contains expanding rock fragments");
            yield return new WaitForSeconds(.15f); Capture("20-earth-wave");
            yield return new WaitForSeconds(1.1f);
            Check(cat.Motor.CanAct, "earth recovery releases after one second");
            cat.Combat.ResetCombat(); cat.Motor.ResetMotor(new Vector3(0, .05f, 0));
            for (int i = 0; i < enemies.Length; i++) { enemies[i].Initialize(500); enemies[i].transform.position = new Vector3(0,.05f,i == 0 ? -2.5f : 2.5f); }
            hero.Motor.ResetMotor(new Vector3(1.5f,.05f,0)); int heroHealth = hero.Health.Current;
            Physics.SyncTransforms(); yield return new WaitForSeconds(.3f);
            cat.Resources.SetCurrentAndMaximum(cat.Resources.MaxMagicPoints, cat.Resources.Stamina,cat.Resources.MaxMagicPoints, cat.Resources.MaxStamina, cat.Resources.MagicRegenPerSecond);
            cat.Combat.Commands = new ActorCommandFrame { Magic = true };
            yield return null; yield return null; cat.Combat.Commands = default;
            yield return new WaitForSeconds(1.31f); Capture("21-thunder-surround");
            yield return new WaitForSeconds(.2f);
            Check(enemies.All(e => e.Current < 500) && hero.Health.Current == heroHealth, "thunder strikes front and rear enemies without friendly fire");
            foreach (var enemy in enemies) enemy.transform.position = new Vector3(10,.05f,10);
            hero.Motor.ResetMotor(new Vector3(-1.2f,.05f,0));
            yield return new WaitForSeconds(.7f);
        }
    }
}
#endif
