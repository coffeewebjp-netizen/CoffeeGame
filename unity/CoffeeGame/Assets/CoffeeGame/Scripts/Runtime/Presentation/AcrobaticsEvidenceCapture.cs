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

namespace CoffeeGame.Presentation
{
    public sealed class AcrobaticsEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private string directory;
        private float deadline;
        private readonly List<string> checks=new List<string>();
        private readonly List<GeometryFrame> geometry=new List<GeometryFrame>();
        public static void Begin(GameObject host,CombatRunController controller,string output)
        {
            var capture=host.AddComponent<AcrobaticsEvidenceCapture>();capture.run=controller;capture.directory=output;
            Directory.CreateDirectory(output);capture.deadline=Time.realtimeSinceStartup+60f;capture.StartCoroutine(capture.Guard(capture.Run()));
        }
        private void Update()
        {
            if(directory==null || Time.realtimeSinceStartup<deadline)return;
            StopAllCoroutines();File.WriteAllText(Path.Combine(directory,"failure.txt"),"Acrobatics capture timed out.");ReportResults();Application.Quit(2);enabled=false;
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                object current;
                try {if(!routine.MoveNext())yield break;current=routine.Current;}
                catch(Exception e){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());ReportResults();Debug.LogException(e);Application.Quit(2);yield break;}
                yield return current;
            }
        }
        private void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("Acrobatics evidence: "+name);checks.Add(name);}
        private void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
        private void ReportResults()=>File.WriteAllText(Path.Combine(directory,"checks.json"),JsonUtility.ToJson(new Report{passed=checks.ToArray(),geometry=geometry.ToArray()},true));
        private void Measure(GameObject actor,string name)
        {
            foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(!renderer.enabled)continue;
                var mesh=new Mesh();renderer.BakeMesh(mesh,true);
                var points=mesh.vertices.Select(v=>renderer.transform.TransformPoint(v).y-actor.transform.position.y).ToArray();
                if(points.Length>0)geometry.Add(new GeometryFrame{name=name,mesh=renderer.name,minY=points.Min(),maxY=points.Max()});
                Destroy(mesh);
            }
        }
        private IEnumerator Run()
        {
            // ScreenCapture requires a visible rendering window. Keep diagnostics
            // stationary until the review window has been brought to the foreground.
            while(!Application.isFocused)yield return null;
            Time.captureDeltaTime=1f/60f;
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse,out _),"input selection");
            var party=run.Party;party.ToggleParticipation(PartyMemberIds.Hero);party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun();
            var enemies=FindObjectsByType<Health>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).Where(h=>h.Team==DamageTeam.Enemy && h.IsAlive).ToArray();
            Check(enemies.Length==2,"two simultaneous live enemies");
            Check(enemies.Any(h=>h.GetComponent<GoblinController>()!=null)&&enemies.Any(h=>h.GetComponent<SlimeController>()!=null),"goblin and slime pair");
            foreach(var e in enemies){if(e.TryGetComponent<GoblinController>(out var g))g.enabled=false;if(e.TryGetComponent<SlimeController>(out var s))s.enabled=false;e.Initialize(1000);}
            party.enabled=false;
            var hero=party.Actors[PartyMemberIds.Hero];var cat=party.Actors[PartyMemberIds.CatMage];
            foreach(var actor in party.Actors.Values){actor.Motor.Commands=default;actor.Combat.Commands=default;actor.Combat.ResetCombat();}
            hero.Motor.ResetMotor(new Vector3(-.6f,.05f,0));cat.Motor.ResetMotor(new Vector3(1.3f,.05f,.5f));
            enemies[0].transform.position=new Vector3(-1.55f,0,-.45f);enemies[1].transform.position=new Vector3(.45f,0,-.45f);
            yield return new WaitForSeconds(.7f);Capture("01-two-enemies");
            Measure(hero.gameObject,"standing");
            Transform hips=hero.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");
            float standingHips=hips.position.y-hero.transform.position.y;
            Transform heroHead=hero.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
            Vector3 standingLookAxis=Quaternion.Inverse(heroHead.rotation)*hero.Motor.Facing;
            int before=enemies.Sum(e=>e.Current);
            hero.Motor.Commands=new ActorCommandFrame{Jump=true};yield return null;yield return null;hero.Motor.Commands=default;
            yield return new WaitForSeconds(.23f);
            hero.Motor.Commands=new ActorCommandFrame{Move=Vector2.down};while(!hero.Motor.IsPlunging)yield return null;
            hero.Motor.Commands=default;yield return new WaitForEndOfFrame();Capture("02-plunge-down");
            while(!hero.Motor.IsGrounded)yield return null;
            Check(hero.Motor.PlungeRecoveryRemaining>.9f,"one second plunge recovery begins on contact");
            Check(enemies.Sum(e=>e.Current)<before,"shockwave damages nearby enemies");
            yield return new WaitForSeconds(.09f);yield return new WaitForEndOfFrame();Capture("03-deep-crouch-impact");
            Measure(hero.gameObject,"plunge-impact");
            var landingBody=geometry.Last(g=>g.name=="plunge-impact"&&g.mesh=="AzureMaidenCleanBody");
            var landingBlade=geometry.Last(g=>g.name=="plunge-impact"&&g.mesh=="AzureMaidenKatana");
            Check(landingBody.minY>-.08f,"landing body stays above the floor");
            Check(landingBlade.minY>-.2f&&landingBlade.minY<.06f,"only the katana tip enters the ground");
            float standingHeight=geometry.First(g=>g.name=="standing"&&g.mesh=="AzureMaidenCleanBody").maxY;
            Check(landingBody.maxY<standingHeight*.75f && hips.position.y-hero.transform.position.y<standingHips-.1f,"actual body lowers into a deep landing crouch");
            var bones=hero.GetComponentsInChildren<Transform>();
            var thigh=bones.First(t=>t.name=="LeftUpLeg");var knee=bones.First(t=>t.name=="LeftLeg");var foot=bones.First(t=>t.name=="LeftFoot");
            Check(Vector3.Dot((knee.position-thigh.position).normalized,(foot.position-knee.position).normalized)<.5f,"landing knee bends more than sixty degrees");
            Check(Vector3.Dot(hero.GetComponent<DefensePosePresentation>().MeasureBladeWorldDirection(),Vector3.down)>.9f,"planted katana remains downward");
            Check(Vector3.Dot(heroHead.rotation*standingLookAxis,Vector3.up)<-.45f,"plunge face looks down instead of up");
            var spine=bones.First(t=>t.name=="Spine");var chest=bones.First(t=>t.name=="Spine01");
            File.WriteAllText(Path.Combine(directory,"plunge-bones.txt"), "facing="+hero.Motor.Facing+"\n"+string.Join("\n", bones.Where(t=>t.name.Contains("Spine")||t.name=="Head"||t.name=="Hips").Select(t=>t.name+" parent="+t.parent.name+" position="+t.position+" rotation="+t.rotation)));
            Check(Vector3.Dot((heroHead.position-hips.position).normalized,hero.Motor.Facing)>.6f,"plunge torso bends strongly forward");
            var otherFoot=bones.First(t=>t.name=="RightFoot");
            Check(Vector3.ProjectOnPlane(foot.position-otherFoot.position,Vector3.up).magnitude>.4f,"landing feet form a wider squat");
            var landingCamera=FindAnyObjectByType<FixedCameraRig>();landingCamera.SetOrbitYaw(42f,true);
            yield return new WaitForEndOfFrame();Capture("03b-crouch-three-quarter");landingCamera.SetOrbitYaw(0f,true);
            int contactHp=enemies.Sum(e=>e.Current);
            hero.Motor.Commands=new ActorCommandFrame{Move=Vector2.right,Jump=true,Dodge=true};
            yield return new WaitForSeconds(.45f);Capture("04-plunge-hold");
            Check(!hero.Motor.CanAct&&!hero.Motor.IsDodging&&!hero.Motor.IsBackflipping,"plunge lock rejects follow-up jump and dodge");
            Check(enemies.Sum(e=>e.Current)==contactHp,"landing shockwave applies once");
            hero.Motor.Commands=default;yield return new WaitForSeconds(.6f);Check(hero.Motor.CanAct,"plunge recovery releases actions");
            hero.Motor.ResetMotor(new Vector3(-.6f,.05f,.5f));yield return new WaitForSeconds(.2f);
            hero.Resources.TrySpendStamina(hero.Resources.MaxStamina);
            hero.Combat.Commands=new ActorCommandFrame{GuardHeld=true};yield return new WaitForSeconds(.25f);
            Check(hero.GetComponent<PlayerDefense>().IsGuarding,"held guard before backflip");Capture("05-guard-ready");
            hero.Motor.Commands=new ActorCommandFrame{Jump=true,Move=Vector2.up,WorldSpace=true};
            yield return null;yield return null;hero.Motor.Commands=default;hero.Combat.Commands=default;
            Check(hero.Motor.IsBackflipping&&!hero.Motor.IsDodging,"backward guarded jump starts backflip");
            Check(!hero.Health.IsDodgeInvulnerable && hero.Resources.Stamina==0,"backflip does not grant dodge invulnerability or special gauge");
            for(int i=1;i<=3;i++){while(hero.Motor.IsBackflipping&&hero.Motor.AcrobaticProgress<i*.23f)yield return null;yield return new WaitForEndOfFrame();Capture("06-backflip-"+i);}
            while(hero.Motor.IsBackflipping)yield return null;
            Check(hero.Motor.IsGrounded,"backflip returns to ground");
            hero.Motor.ResetMotor(new Vector3(-1.2f,.05f,0));yield return new WaitForSeconds(.25f);
            hero.Motor.Commands=new ActorCommandFrame{Dodge=true,Move=Vector2.right,WorldSpace=true};
            yield return null;yield return null;hero.Motor.Commands=default;Check(hero.Motor.IsDodging,"ground roll begins");
            float peak=hero.transform.position.y;
            for(int i=1;i<=3;i++){while(hero.Motor.IsDodging&&hero.Motor.AcrobaticProgress<i*.23f){peak=Mathf.Max(peak,hero.transform.position.y);yield return null;}yield return new WaitForEndOfFrame();Capture("07-ground-roll-"+i);Measure(hero.gameObject,"roll-"+i);}
            while(hero.Motor.IsDodging)yield return null;
            Check(peak<.18f,"roll physical hop stays close to ground");
            Check(hero.Motor.CanAct&&hero.Motor.IsGrounded,"roll returns control after recovery");
            Check(!hero.GetComponent<CoffeeGame.Audio.SpecialCombatVoice>().HasClip&&!cat.GetComponent<CoffeeGame.Audio.SpecialCombatVoice>().HasClip,"pending Owner special voice files remain optional");
            Capture("08-recovered");
            enemies[0].transform.position=new Vector3(-4f,0,3f);enemies[1].transform.position=new Vector3(4f,0,3f);
            hero.Motor.ResetMotor(new Vector3(2f,.05f,0));
            var cameraRig=FindAnyObjectByType<FixedCameraRig>();cameraRig.Follow(cat.transform);cameraRig.Snap();
            cat.Motor.ResetMotor(new Vector3(-.6f,.05f,.4f));yield return new WaitForSeconds(.25f);
            Check(cat.GetComponent<AcrobaticMotionPresentation>().HasPoseRig,"cat model has a compatible full body motion rig");
            cat.Combat.Commands=new ActorCommandFrame{GuardHeld=true};yield return new WaitForSeconds(.25f);
            cat.Motor.Commands=new ActorCommandFrame{Jump=true,Move=Vector2.up,WorldSpace=true};yield return null;yield return null;cat.Motor.Commands=default;cat.Combat.Commands=default;
            Check(cat.Motor.IsBackflipping,"cat guard also permits backward flip");
            while(cat.Motor.IsBackflipping&&cat.Motor.AcrobaticProgress<.46f)yield return null;
            yield return new WaitForEndOfFrame();Capture("09-cat-backflip");
            while(cat.Motor.IsBackflipping)yield return null;
            cat.Motor.ResetMotor(new Vector3(-.6f,.05f,.4f));yield return new WaitForSeconds(.25f);
            cat.Motor.Commands=new ActorCommandFrame{Dodge=true,Move=Vector2.left,WorldSpace=true};yield return null;yield return null;cat.Motor.Commands=default;
            Check(cat.Motor.IsDodging,"cat ground roll starts");
            while(cat.Motor.IsDodging&&cat.Motor.AcrobaticProgress<.46f)yield return null;
            yield return new WaitForEndOfFrame();Capture("10-cat-ground-roll");
            while(cat.Motor.IsDodging)yield return null;
            Check(cat.Motor.IsGrounded&&cat.Motor.CanAct,"cat acrobatics return control");
            foreach(var actor in new[]{hero,cat})
            {
                cameraRig.Follow(actor.transform);cameraRig.SetOrbitYaw(0f,true);cameraRig.Snap();
                string actorName=actor==hero?"hero":"cat";
                foreach(float side in new[]{-1f,1f})
                {
                    actor.Motor.ResetMotor(new Vector3(0,.05f,0));yield return new WaitForSeconds(.25f);
                    actor.Combat.Commands=new ActorCommandFrame{GuardHeld=true};yield return new WaitForSeconds(.25f);
                    actor.Motor.Commands=new ActorCommandFrame{Jump=true,Move=Vector2.right*side,WorldSpace=true};
                    yield return null;yield return null;actor.Motor.Commands=default;actor.Combat.Commands=default;
                    Check(actor.Motor.IsCartwheeling,actorName+" guarded cartwheel "+side);
                    Check(!actor.Health.IsDodgeInvulnerable,actorName+" cartwheel has no dodge immunity "+side);
                    for(int i=1;i<=3;i++)
                    {while(actor.Motor.IsCartwheeling&&actor.Motor.AcrobaticProgress<i*.23f)yield return null;yield return new WaitForEndOfFrame();Capture("11-"+actorName+"-cartwheel-"+side+"-"+i);}
                    while(actor.Motor.IsGuardJumping)yield return null;
                    Check(actor.Motor.CanAct&&actor.Motor.IsGrounded,actorName+" cartwheel returns control "+side);
                }
                actor.Motor.ResetMotor(new Vector3(-2f,.05f,0));yield return new WaitForSeconds(.25f);
                actor.Motor.Commands=new ActorCommandFrame{Move=Vector2.right,WorldSpace=true};yield return new WaitForSeconds(.8f);
                Check(actor.Motor.IsRunning,actorName+" run established before dodge");
                actor.Motor.Commands=new ActorCommandFrame{Move=Vector2.right,Dodge=true,WorldSpace=true};
                yield return null;yield return null;actor.Motor.Commands=default;
                var spinPresentation=actor.GetComponent<AcrobaticMotionPresentation>();
                Check(actor.Motor.IsRunningDodge && (actor==hero ? !spinPresentation.IsPresenting :
                    spinPresentation.IsPresenting && spinPresentation.CurrentKind==AcrobaticMotionKind.RunningSpin),
                    actorName+" running dodge uses authored clip or cat spin fallback");
                for(int i=1;i<=3;i++)
                {while(actor.Motor.IsDodging&&actor.Motor.AcrobaticProgress<i*.23f)yield return null;yield return new WaitForEndOfFrame();Capture("12-"+actorName+"-running-spin-"+i);
                    if(i==2 && actor==cat)
                    {var spinBones=actor.GetComponentsInChildren<Transform>();
                    var spinHips=spinBones.First(t=>t.name=="Hips");var spinHead=spinBones.First(t=>t.name=="Head");
                    Check(spinHead.position.y<spinHips.position.y,"cat running dodge visibly turns upside down");}}
                while(actor.Motor.IsDodging)yield return null;
                Check(actor.Motor.CanAct&&actor.Motor.IsGrounded,actorName+" running spin lands and releases control");
            }
            ReportResults();yield return new WaitForSeconds(.4f);Application.Quit(0);
        }
        [Serializable] private sealed class GeometryFrame{public string name,mesh;public float minY,maxY;}
        [Serializable] private sealed class Report{public string[] passed;public GeometryFrame[] geometry;}
    }
}
#endif
