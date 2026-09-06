#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using CoffeeGame.Enemies;
using CoffeeGame.Input;
using CoffeeGame.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    // Explicit development command. Bootstrap supplies an in-memory profile for this run.
    public sealed class GoblinEvidenceCapture : MonoBehaviour
    {
        private Camera sceneCamera;
        private Transform player;
        private CombatRunController run;
        private string directory;
        private readonly List<string> passed = new List<string>();

        public static void Begin(GameObject host, Camera camera, Transform hero, CombatRunController controller, string output)
        {
            var capture = host.AddComponent<GoblinEvidenceCapture>();
            capture.sceneCamera = camera; capture.player = hero; capture.run = controller;
            capture.directory = Path.GetFullPath(output);
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
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(directory,"failure.txt"),error.ToString());
                    Debug.LogException(error); Application.Quit(2); yield break;
                }
                yield return current;
            }
        }
        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Goblin evidence failed: " + name);
            passed.Add(name);
        }
        private IEnumerator Run()
        {
            yield return null;
            Check(run.TrySelectInputMode(InputMode.KeyboardMouse,out _),"input selected");
            run.StartNewRun();
            player.GetComponent<PlayerMotor3D>().enabled = false;
            player.GetComponent<PlayerCombatController>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            run.PlayerHealth.EvasionChance = 0f;
            yield return new WaitForSeconds(0.2f);
            CombatEnemy first = run.CurrentEnemy;
            Check(first != null && first.Kind == EnemyKind.Goblin,"first encounter is goblin");
            string firstClaim = first.ClaimId;
            var ai = first.GetComponent<GoblinController>();
            var visual = first.GetComponentInChildren<GoblinCharacterVisual>();
            Capture("01-forest-approach");
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!ai.IsWindingUp && Time.realtimeSinceStartup < deadline) yield return null;
            Check(ai.IsWindingUp,"goblin approaches and winds up");
            int beforeHit = run.PlayerHealth.Current;
            yield return new WaitForSeconds(0.6f);
            Capture("02-forest-windup");
            Check(run.PlayerHealth.Current == beforeHit,"windup does no damage");
            yield return new WaitForSeconds(0.29f);
            Capture("03-forest-impact");
            Check(run.PlayerHealth.Current < beforeHit,"committed strike hits target");
            run.Pause();
            yield return null; // Observe the first complete paused frame, after the pending animator evaluation.
            Check(run.Mode == CombatRunMode.Paused && Time.timeScale == 0f,"run enters pause");
            Vector3 pausedPosition = first.transform.position;
            float pausedTime = visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            yield return new WaitForSecondsRealtime(0.25f);
            Check(first.transform.position == pausedPosition &&
                Mathf.Abs(visual.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime-pausedTime)<0.001f,"pause freezes enemy and animation");
            run.Resume();
            ai.enabled = false; visual.enabled = false;
            var cameraRig = sceneCamera.GetComponent<FixedCameraRig>();
            cameraRig.enabled = false;
            Vector3 originalCameraPosition = sceneCamera.transform.position;
            Quaternion originalCameraRotation = sceneCamera.transform.rotation;
            bool originalOrthographic = sceneCamera.orthographic;
            float originalOrthographicSize = sceneCamera.orthographicSize;
            sceneCamera.orthographic = true; sceneCamera.orthographicSize = 1.15f;
            visual.SetFacing(Vector3.back);
            sceneCamera.transform.position = first.transform.position + new Vector3(1.8f,1.25f,-3.5f);
            sceneCamera.transform.LookAt(first.transform.position + Vector3.up*0.65f);
            foreach (string pose in new[] { "Idle","Walk","AttackWindup","Attack","Hurt","Defeated" })
            {
                visual.Animator.speed = 0f;
                visual.Animator.Play(pose,0,pose == "Defeated" ? 0.97f : pose == "AttackWindup" ? 0.92f : 0.45f);
                visual.Animator.Update(0f);
                yield return null;
                Capture("portrait-" + pose);
            }
            sceneCamera.orthographic = originalOrthographic;
            sceneCamera.orthographicSize = originalOrthographicSize;
            sceneCamera.transform.SetPositionAndRotation(originalCameraPosition,originalCameraRotation);
            cameraRig.enabled = true;
            visual.enabled = true; visual.ResetState(player.position-first.transform.position); ai.enabled = true;
            for (int kill = 0; kill < 5; kill++)
            {
                CombatEnemy enemy = run.CurrentEnemy;
                Check(enemy != null && enemy.Kind == EnemyEncounterRoster.At(kill),"encounter roster " + (kill+1));
                var goblin = enemy.GetComponent<GoblinController>();
                if (goblin != null) goblin.enabled = false;
                var slime = enemy.GetComponent<SlimeController>();
                if (slime != null) slime.enabled = false;
                enemy.Health.ApplyDamage(new DamageInfo(999,player.gameObject,enemy.transform.position,Vector3.zero));
                enemy.Health.ApplyDamage(new DamageInfo(999,player.gameObject,enemy.transform.position,Vector3.zero));
                Check(run.Kills == kill+1,"single reward for repeated lethal hit " + (kill+1));
                if (kill == 0)
                {
                    run.Pause();
                    yield return new WaitForSecondsRealtime(1.1f);
                    Check(run.CurrentEnemy == enemy,"pause holds defeat/respawn timer");
                    run.Resume();
                }
                deadline = Time.realtimeSinceStartup+4f;
                while (ReferenceEquals(run.CurrentEnemy,enemy) && Time.realtimeSinceStartup < deadline) yield return null;
                Check(!ReferenceEquals(run.CurrentEnemy,enemy),"defeated enemy removed " + (kill+1));
            }
            Check(run.Mode == CombatRunMode.RivalEncounter && run.Kills == 5,"fifth kill enters rival encounter");
            Check(run.Progression.Gold == 5 && run.Progression.SlimeJelly == 2,"mixed rewards: five gold, two slime jelly");
            run.ContinueAfterRivalEncounter();
            Check(run.CurrentEnemy != null && run.CurrentEnemy.Kind == EnemyKind.Slime,"sixth encounter continues roster after rival");
            run.StartNewRun();
            yield return null;
            Check(run.Kills == 0 && run.CurrentEnemy.Kind == EnemyKind.Goblin && run.CurrentEnemy.ClaimId != firstClaim,
                "retry restarts at goblin with fresh claim");
            Check(run.Progression.Gold == 5 && run.Progression.SlimeJelly == 2,"retry preserves in-memory progression");
            File.WriteAllText(Path.Combine(directory,"integration.json"),JsonUtility.ToJson(new Report {
                passed=passed.ToArray(), device=SystemInfo.graphicsDeviceName,
                note="Actual runtime enemy factory, animator, AI, death/respawn and run controller; in-memory progression, no player-profile writes."
            },true));
            Debug.Log("CoffeeGAME goblin evidence passed " + passed.Count + " checks: " + directory);
            Application.Quit(0);
        }
        private void Capture(string name)
        {
            var target = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active; Texture2D image = null;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(sceneCamera,new RenderPipeline.StandardRequest { destination=target });
                RenderTexture.active=target;
                image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(directory,name+".png"),image.EncodeToPNG());
            }
            finally { RenderTexture.active=previous; if(image!=null)Destroy(image); target.Release();Destroy(target); }
        }
        [Serializable] private sealed class Report { public string[] passed; public string device,note; }
    }
}
#endif
