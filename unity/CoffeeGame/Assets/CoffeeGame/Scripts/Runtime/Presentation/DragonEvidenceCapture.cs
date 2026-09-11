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
using CoffeeGame.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CoffeeGame.Presentation
{
    public sealed class DragonEvidenceCapture:MonoBehaviour
    {
        private CombatRunController run;
        private string directory;
        private readonly List<string> checks=new List<string>();
        public static void Begin(GameObject host,CombatRunController run,string path)
        {
            var c=host.AddComponent<DragonEvidenceCapture>();c.run=run;c.directory=path==null?null:Path.GetFullPath(path);
            if(c.directory!=null)Directory.CreateDirectory(c.directory);
            Application.runInBackground=true;c.StartCoroutine(c.Guard(c.Run()));
        }
        private IEnumerator Guard(IEnumerator routine)
        {
            while(true)
            {
                object item;try{if(!routine.MoveNext())yield break;item=routine.Current;}
                catch(Exception e){Debug.LogException(e);if(directory!=null){File.WriteAllText(Path.Combine(directory,"failure.txt"),e.ToString());Application.Quit(2);}yield break;}
                yield return item;
            }
        }
        private void Check(bool pass,string name){if(!pass)throw new InvalidOperationException("Dragon evidence: "+name);checks.Add(name);}
        private void Capture(string name)
        {
            var camera=Camera.main;
            var canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c=>c.enabled&&c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var target=new RenderTexture(1600,900,24);target.Create();
            var previous=RenderTexture.active;var rect=camera.rect;float aspect=camera.aspect;
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;}
                camera.rect=new Rect(0,0,1,1);camera.aspect=1600f/900f;Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.rect=rect;camera.aspect=aspect;
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;}
                Destroy(target);Destroy(pixels);Canvas.ForceUpdateCanvases();
            }
        }
        private IEnumerator Run()
        {
            yield return null;
            run.TrySelectInputMode(InputMode.KeyboardMouse,out _);
            var party=run.Party;
            foreach(var member in party.State.Members)if(member.RecoveryState==PartyRecoveryState.Resting)party.ToggleParticipation(member.Id);
            run.StartNewRun();
            var dragon=party.Actors[PartyMemberIds.DragonGirl];
            Check(party.State.Members.Count==3,"three separate party members");
            foreach(var actor in party.Actors.Values)actor.Combat.ResetCombat();
            party.RequestSwitch(dragon.MemberId);
            yield return new WaitForSeconds(.8f);
            Check(party.Active==dragon,"dragon is controllable");
            Check(dragon.GetComponentInChildren<Animator>().runtimeAnimatorController.name=="DragonGirlRuntime","dedicated model and controller");
            if(directory==null)yield break;
            party.enabled=false;
            foreach(var actor in party.Actors.Values){actor.Motor.Commands=default;actor.Combat.Commands=default;actor.Combat.ResetCombat();}
            dragon.Combat.IsManual=true;
            dragon.Combat.CriticalChance=0;
            var enemy=run.CurrentEnemy.GetComponent<Health>();enemy.Initialize(10000);
            foreach(var goblin in FindObjectsByType<GoblinController>(FindObjectsInactive.Exclude)){goblin.Parry(1);goblin.enabled=false;}
            foreach(var slime in FindObjectsByType<SlimeController>(FindObjectsInactive.Exclude)){slime.Parry(1);slime.enabled=false;}
            foreach(var actor in party.Actors.Values)if(actor!=dragon)actor.Motor.ResetMotor(new Vector3(actor.IsCat?3:-3,.05f,-1));
            dragon.Motor.ResetMotor(new Vector3(0,.05f,0));enemy.transform.position=new Vector3(0,.05f,1.5f);
            dragon.Motor.LockedTarget=enemy;
            yield return new WaitForSeconds(.3f);Capture("01-dragon-ready");
            int previous=enemy.Current;
            int firstHit=0;
            for(int strike=0;strike<3;strike++)
            {
                enemy.transform.position=dragon.transform.position+dragon.Motor.Facing*.65f;
                Physics.SyncTransforms();
                int beforeStrike=enemy.Current;
                dragon.Combat.Commands=new ActorCommandFrame{Sword=true};yield return null;dragon.Combat.Commands=default;
                yield return new WaitForSeconds(strike==2?.9f:.46f);
                int dealt=beforeStrike-enemy.Current;
                Debug.Log("Dragon claw damage stage "+(strike+1)+": "+dealt);
                Check(dealt>0,"claw strike "+(strike+1)+" hits");
                if(strike==0)firstHit=dealt;
                if(strike==2)Check(dealt>=firstHit*2,"third claw is stronger");
            }
            Check(enemy.Current<previous,"claw combo deals damage");Capture("02-claw-combo");
            dragon.Combat.Commands=new ActorCommandFrame{Magic=true};yield return null;dragon.Combat.Commands=default;
            yield return new WaitForSeconds(1f);
            Check(dragon.Combat.DragonGateStage==1,"front gate stage");Check(DragonGateCaptivity.IsCaptured(enemy.gameObject),"enemy captured");Capture("03-front-gate");
            var frozenPosition=enemy.transform.position;yield return new WaitForSeconds(.3f);Check(Vector3.Distance(frozenPosition,enemy.transform.position)<.01f,"captured enemy cannot move");
            dragon.Combat.Commands=new ActorCommandFrame{Magic=true};yield return null;dragon.Combat.Commands=default;
            yield return new WaitForSeconds(1.1f);Check(dragon.Combat.DragonGateStage==2,"rear gate summons dragon");Capture("04-allied-dragon");
            previous=enemy.Current;yield return new WaitForSeconds(1.7f);Check(enemy.Current<previous,"summoned dragon attacks");
            dragon.Combat.Commands=new ActorCommandFrame{Magic=true};yield return null;dragon.Combat.Commands=default;
            yield return new WaitForSeconds(.7f);Capture("05-dragon-chop");
            yield return new WaitForSeconds(1f);Check(dragon.Combat.DragonGateStage==0,"third gate resets sequence");Check(!DragonGateCaptivity.IsCaptured(enemy.gameObject),"finisher releases capture");
            dragon.Resources.GainStamina(dragon.Resources.MaxStamina);
            dragon.Combat.Commands=new ActorCommandFrame{Special=true};yield return null;dragon.Combat.Commands=default;yield return new WaitForSeconds(.1f);
            Check(dragon.Combat.DragonBreathRemaining>59,"breath begins at sixty seconds");Check(dragon.Motor.AbilitySpeedMultiplier==2&&dragon.Health.AbilityDefenseMultiplier==2,"movement and defense doubled");Capture("06-dragon-breath");
            run.Pause();float remaining=dragon.Combat.DragonBreathRemaining;yield return new WaitForSecondsRealtime(.2f);Check(Mathf.Abs(remaining-dragon.Combat.DragonBreathRemaining)<.02f,"pause preserves breath");run.Resume();
            yield return new WaitForSeconds(.8f);
            dragon.Motor.Commands=new ActorCommandFrame{Jump=true};yield return null;dragon.Motor.Commands=default;yield return new WaitForSeconds(.15f);
            Check(!dragon.Motor.IsGrounded,"dragon jumps");
            enemy.transform.position=dragon.transform.position+dragon.Motor.Facing*.6f;Physics.SyncTransforms();previous=enemy.Current;
            dragon.Combat.Commands=new ActorCommandFrame{Sword=true};yield return null;dragon.Combat.Commands=default;yield return new WaitForSeconds(.18f);Capture("07-air-claw");
            Check(enemy.Current<previous,"air claw damages enemy");
            bool stompStarted=false,stompLanded=false;
            dragon.Motor.PlungeStarted+=()=>stompStarted=true;
            dragon.Motor.Landed+=_=>stompLanded=true;
            dragon.Motor.Commands=new ActorCommandFrame{Move=Vector2.down};yield return new WaitForSeconds(.15f);dragon.Motor.Commands=default;
            previous=enemy.Current;
            for(float wait=0;wait<2f&&!stompLanded;wait+=Time.deltaTime)yield return null;
            Check(stompStarted&&stompLanded,"down input triggers stomp landing");
            yield return new WaitForSeconds(.12f);Capture("08-wind-stomp");yield return new WaitForSeconds(.38f);
            Check(enemy.Current<previous,"expanding stomp damages enemy");
            dragon.Combat.ResetCombat();Check(dragon.Combat.DragonBreathRemaining==0&&dragon.Motor.AbilitySpeedMultiplier==1&&dragon.Health.AbilityDefenseMultiplier==1,"reset clears breath multipliers");
            var hud=FindAnyObjectByType<CombatGameHudView>();
            hud.Refresh(run,false);yield return null;
            var dragonPanel=hud.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="Party "+PartyMemberIds.DragonGirl);
            var heroPanel=hud.GetComponentsInChildren<RectTransform>(true).First(t=>t.name=="Party "+PartyMemberIds.Hero);
            Check(dragonPanel.anchoredPosition.y>heroPanel.anchoredPosition.y,"controlled member is first in party HUD");
            Check(dragonPanel.GetComponentsInChildren<Button>(true).First(b=>b.name=="Switch").GetComponentInChildren<Text>().text=="操作中","active switch button is explicit");
            Capture("09-active-party-order");
            run.Pause();hud.SetSelectedTab(CharacterMenuTab.Status);hud.RebuildMenuContent(run);hud.Refresh(run,true);yield return null;
            Check(hud.GetComponentsInChildren<Text>(true).First(t=>t.name=="Content").text.Contains("龍少女のステータス"),"status follows controlled member");
            Check(hud.GetComponentsInChildren<RawImage>(true).First(i=>i.name=="Active Companion Full Body").gameObject.activeSelf,"status uses dragon artwork");
            Capture("10-dragon-status");
            hud.SetSelectedTab(CharacterMenuTab.System);hud.RebuildMenuContent(run);hud.Refresh(run,true);yield return null;
            Check(hud.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Rival Interval Increase"),"rival interval settings available");
            Check(hud.GetComponentsInChildren<Button>(true).Any(b=>b.name=="Debug Affinity 4"&&b.GetComponentInChildren<Text>().text.Contains("龍少女")),"dragon affinity debug controls available");
            Canvas.ForceUpdateCanvases();
            var scroll=hud.GetComponentsInChildren<ScrollRect>(true).First();scroll.verticalNormalizedPosition=0f;Canvas.ForceUpdateCanvases();
            Capture("11-progression-settings");
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonUtility.ToJson(new Report{passed=checks.ToArray(),device=SystemInfo.graphicsDeviceName},true));
            yield return new WaitForSecondsRealtime(.5f);Application.Quit(0);
        }
        [Serializable]private sealed class Report{public string[] passed;public string device;}
    }
}
#endif
