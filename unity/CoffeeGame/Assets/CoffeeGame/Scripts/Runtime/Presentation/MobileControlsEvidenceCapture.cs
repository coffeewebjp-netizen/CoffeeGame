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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Phase = UnityEngine.InputSystem.TouchPhase;

namespace CoffeeGame.Presentation
{
    // Explicit development command only; the bootstrap uses an isolated memory profile.
    public sealed class MobileControlsEvidenceCapture : MonoBehaviour
    {
        private CombatRunController run;
        private GameInputReader input;
        private OnScreenTouchControls overlay;
        private Touchscreen screen;
        private string output;
        private float deadline;
        private readonly List<string> checks = new List<string>();
        [Serializable] private sealed class Result { public string[] passed; }
        public static void Begin(GameObject host, CombatRunController run, string path)
        {
            var c = host.AddComponent<MobileControlsEvidenceCapture>(); c.run = run; c.output = path;
            Application.runInBackground = true; Directory.CreateDirectory(path);
            c.deadline = Time.realtimeSinceStartup + 100;
            c.StartCoroutine(c.Guard(c.Run()));
        }
        private void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks.Add(label); }
        private void Report() => File.WriteAllText(Path.Combine(output, "checks.json"), JsonUtility.ToJson(new Result { passed = checks.ToArray() }, true));
        private void Fail(Exception e) { File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); Report(); Debug.LogException(e); Application.Quit(2); enabled = false; }
        private void Update() { if (Time.realtimeSinceStartup > deadline) { StopAllCoroutines(); Fail(new TimeoutException("Mobile evidence timeout")); } }
        private IEnumerator Guard(IEnumerator steps)
        {
            while (true)
            {
                object next;
                try { if (!steps.MoveNext()) yield break; next = steps.Current; }
                catch (Exception e) { Fail(e); yield break; }
                yield return next;
            }
        }
        private Vector2 Point(GameInputSemantic action) => overlay.Layout.Buttons.Single(b => b.Action == action).Bounds.center;
        private void Touch(int id, Vector2 point, Phase phase) => InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, position = point, phase = phase });
        private void Capture(string name)
        {
            // A hidden Windows window has no drawable back buffer. Render the real
            // scene and real uGUI controls into a texture without showing a window.
            var camera = Camera.main;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude).Where(c => c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var target = new RenderTexture(Screen.width, Screen.height, 24); target.Create();
            var previous = RenderTexture.active; var oldRect = camera.rect; float aspect = camera.aspect;
            var pixels = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .1f; }
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = (float)Screen.width / Screen.height; Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.rect = oldRect; camera.aspect = aspect;
                foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
                Destroy(target); Destroy(pixels); Canvas.ForceUpdateCanvases();
            }
        }
        private IEnumerator Run()
        {
            yield return null;
            input = FindAnyObjectByType<GameInputReader>(); overlay = FindAnyObjectByType<OnScreenTouchControls>();
            screen = InputSystem.AddDevice<Touchscreen>();
            Check(run.TrySelectInputMode(InputMode.TouchOnScreen, out _), "touch selected in actual player");
            run.Party.ToggleParticipation(PartyMemberIds.Hero); run.Party.ToggleParticipation(PartyMemberIds.CatMage);
            run.StartNewRun(); yield return new WaitForSecondsRealtime(.5f);
            // The automated player is deliberately hidden; simulate its focused state.
            overlay.SendMessage("OnApplicationFocus", true); overlay.SendMessage("OnApplicationPause", false);
            var hero = run.Party.Active;
            foreach (var enemy in FindObjectsByType<Health>(FindObjectsInactive.Exclude).Where(h => h.Team == DamageTeam.Enemy))
            {
                if (enemy.TryGetComponent<GoblinController>(out var goblin)) goblin.enabled = false;
                if (enemy.TryGetComponent<SlimeController>(out var slime)) slime.enabled = false;
                enemy.Initialize(10000);
                enemy.transform.position = hero.transform.position + new Vector3(5, 0, 5);
            }
            yield return new WaitForSecondsRealtime(.2f);
            Check(overlay.IsVisible && overlay.Layout.Buttons.Length == 8, "eight reachable touch actions");
            yield return new WaitForEndOfFrame(); Capture("01-phone-hero");
            Vector2 cameraPoint = new Vector2(Screen.width * .65f, Screen.height * .65f);
            float yawBefore = Camera.main.transform.eulerAngles.y;
            Touch(13, cameraPoint, Phase.Began); yield return null; yield return null;
            Touch(13, cameraPoint + Vector2.right * 160 * overlay.Layout.Scale, Phase.Moved);
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(Mathf.DeltaAngle(yawBefore, Camera.main.transform.eulerAngles.y)) > 10f, "camera swipe reaches actual orbit driver");
            Touch(13, cameraPoint, Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Vector2 stick = overlay.Layout.StickCenter;
            Touch(1, stick, Phase.Began); yield return null; yield return null;
            Touch(1, stick + Vector2.right * overlay.Layout.StickRadius, Phase.Moved);
            Touch(2, Point(GameInputSemantic.Sword), Phase.Began);
            yield return new WaitForSecondsRealtime(.1f);
            Check(input.Move.x > .9f, "movement continues with a separate attack finger");
            Check(!hero.Combat.CanBeginGuard, "touch attack reaches the actual combat controller");
            Touch(1, stick, Phase.Ended); Touch(2, Point(GameInputSemantic.Sword), Phase.Ended);
            yield return new WaitForSecondsRealtime(1f);
            Touch(3, Point(GameInputSemantic.Guard), Phase.Began); yield return new WaitForSecondsRealtime(.15f);
            Check(input.GuardHeld, "guard can be held");
            Vector3 back = -hero.Motor.Facing;
            Vector3 cameraRight = Vector3.ProjectOnPlane(Camera.main.transform.right, Vector3.up).normalized;
            Vector3 cameraForward = Vector3.ProjectOnPlane(Camera.main.transform.forward, Vector3.up).normalized;
            Vector2 backInput = new Vector2(Vector3.Dot(back, cameraRight), Vector3.Dot(back, cameraForward)).normalized;
            Touch(12, stick, Phase.Began); yield return null; yield return null;
            Touch(12, stick + backInput * overlay.Layout.StickRadius, Phase.Moved); yield return new WaitForSecondsRealtime(.1f);
            Touch(4, Point(GameInputSemantic.Jump), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            Check(hero.Motor.IsBackflipping, "three fingers trigger movement plus guard backflip");
            Touch(3, Point(GameInputSemantic.Guard), Phase.Ended); Touch(4, Point(GameInputSemantic.Jump), Phase.Ended);
            Touch(12, stick, Phase.Ended);
            yield return new WaitForSecondsRealtime(1.5f);
            Touch(5, Point(GameInputSemantic.LockOn), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            var target = run.GetComponent<TargetLockController>();
            Check(target.IsLocked, "touch lock acquires a target");
            Touch(5, Point(GameInputSemantic.LockOn), Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Touch(6, Point(GameInputSemantic.LockOn), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            Check(!target.IsLocked, "second touch unlocks");
            Touch(6, Point(GameInputSemantic.LockOn), Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Touch(7, Point(GameInputSemantic.SwitchCharacter), Phase.Began); yield return new WaitForSecondsRealtime(.2f);
            Check(run.Party.Active.IsCat, "touch switch controls the cat");
            Check(OnScreenTouchControls.Label(GameInputSemantic.Sword, true) == "連弾" && OnScreenTouchControls.Label(GameInputSemantic.Special, true) == "時止め", "cat-specific touch labels");
            Touch(7, Point(GameInputSemantic.SwitchCharacter), Phase.Ended);
            yield return new WaitForEndOfFrame(); Capture("02-phone-cat");
            Touch(8, stick, Phase.Began); yield return null; yield return null;
            Touch(8, stick + Vector2.up * overlay.Layout.StickRadius, Phase.Moved); yield return new WaitForSecondsRealtime(.1f);
            Check(input.Move.y > .9f, "second actor accepts touch movement");
            // Real uGUI touch click on the pause button, while another finger holds movement.
            var pause = FindObjectsByType<Button>(FindObjectsInactive.Exclude).Single(b => b.name == "Pause");
            var pauseRect = pause.GetComponent<RectTransform>();
            Vector2 pausePoint = RectTransformUtility.WorldToScreenPoint(null, pauseRect.TransformPoint(pauseRect.rect.center));
            Touch(9, pausePoint, Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            Touch(9, pausePoint, Phase.Ended); yield return new WaitForSecondsRealtime(.2f);
            Check(run.Mode == CombatRunMode.Paused && input.Move == Vector2.zero && !overlay.IsVisible, "uGUI pause clears held gameplay touches");
            yield return new WaitForEndOfFrame(); Capture("03-phone-pause");
            run.Resume(); yield return new WaitForSecondsRealtime(.1f);
            Touch(8, stick + Vector2.right * overlay.Layout.StickRadius, Phase.Moved); yield return new WaitForSecondsRealtime(.1f);
            Check(input.Move == Vector2.zero, "resuming requires lifting an old finger");
            Touch(8, stick, Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Touch(10, Point(GameInputSemantic.Guard), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            overlay.SendMessage("OnApplicationPause", true); overlay.SendMessage("OnApplicationFocus", false);
            overlay.SendMessage("OnApplicationFocus", true); yield return new WaitForSecondsRealtime(.1f);
            Check(!input.GuardHeld, "focus gain alone cannot cancel application pause");
            overlay.SendMessage("OnApplicationPause", false); yield return new WaitForSecondsRealtime(.1f);
            Check(!input.GuardHeld, "resume does not restore an old guard touch");
            Touch(10, Point(GameInputSemantic.Guard), Phase.Canceled); yield return new WaitForSecondsRealtime(.1f);
            Touch(11, Point(GameInputSemantic.Guard), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            Check(input.GuardHeld, "fresh guard touch works after resume");
            Touch(11, Point(GameInputSemantic.Guard), Phase.Canceled); yield return new WaitForSecondsRealtime(.1f);
            Check(!input.GuardHeld, "canceled touch releases guard");
            Touch(21, stick, Phase.Began); Touch(22, Point(GameInputSemantic.Guard), Phase.Began); yield return new WaitForSecondsRealtime(.1f);
            Touch(21, stick, Phase.Ended); Touch(22, Point(GameInputSemantic.Guard), Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Touch(22, Point(GameInputSemantic.Guard), Phase.Began); yield return new WaitForSecondsRealtime(.2f);
            Check(screen.touches.Any(t => !t.press.isPressed && t.touchId.ReadValue() == 22), "fixture retains an ended slot with the recycled ID");
            Check(input.GuardHeld, "recycled touch ID stays held despite an old ended slot");
            Touch(22, Point(GameInputSemantic.Guard), Phase.Ended); yield return new WaitForSecondsRealtime(.1f);
            Screen.SetResolution(1920, 864, FullScreenMode.Windowed); yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame(); Capture("04-wide-phone-cat");
            Check(overlay.Layout.SafeArea.width == Screen.safeArea.width, "overlay follows viewport resize");
            while (Directory.GetFiles(output, "*.png").Length < 4) yield return null;
            Check(true, "four full UI frames captured at phone aspect ratios");
            InputSystem.RemoveDevice(screen); Report(); Application.Quit(0);
        }
    }
}
#endif
