#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    public sealed class MeshyMotionEvidenceCapture : MonoBehaviour
    {
        [Serializable]
        private sealed class Sample
        {
            public string sequence;
            public string action;
            public int index;
            public float requestedSeconds;
            public float actualSeconds;
            public float expectedProgress;
            public float animatorProgress;
            public float animatorSpeed;
            public bool inTransition;
            public int fullPathHash;
            public int shortNameHash;
            public string image;
        }

        [Serializable]
        private sealed class Report
        {
            public string taskId = "ORC-20260905-001";
            public string workPackage = "WP13";
            public string input = "IN08,IN09,IN10,IN11,IN12";
            public string output = "OUT20";
            public string renderer = "actual development player / ModelCharacterVisual";
            public bool denseVideoFrames;
            public Vector3 framingCenter;
            public Vector3 framingSize;
            public List<Sample> samples = new List<Sample>();
        }

        private sealed class BufferedFrame
        {
            public string Path;
            public Texture2D Image;
        }

        private static readonly float[] Fractions = { 0f, 0.25f, 0.5f, 0.75f, 0.98f };
        private Camera sceneCamera;
        private ModelCharacterVisual visual;
        private string outputDirectory;
        private Vector3 originalCameraPosition;
        private Quaternion originalCameraRotation;
        private float originalOrthographicSize;
        private readonly Report report = new Report();

        public static void Begin(
            GameObject host,
            Camera camera,
            ModelCharacterVisual modelVisual,
            string outputPath)
        {
            MeshyMotionEvidenceCapture capture = host.AddComponent<MeshyMotionEvidenceCapture>();
            capture.sceneCamera = camera;
            capture.visual = modelVisual;
            capture.outputDirectory = Path.GetFullPath(outputPath);
            capture.report.denseVideoFrames = Array.Exists(Environment.GetCommandLineArgs(),
                argument => string.Equals(argument, "-captureMeshyMotionVideo", StringComparison.OrdinalIgnoreCase));
            capture.StartCoroutine(capture.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(outputDirectory);
            Application.runInBackground = true;
            // Title/setup flows may leave the development player paused. The
            // evidence sequence uses realtime waits, so explicitly advance the
            // Animator while recording and restore the caller's scale on exit.
            float previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(1f);
            FixedCameraRig cameraRig = sceneCamera.GetComponent<FixedCameraRig>();
            bool cameraRigWasEnabled = cameraRig != null && cameraRig.enabled;
            if (cameraRig != null) cameraRig.enabled = false;
            visual.ResetState(Vector3.back);
            visual.Animator.Update(0f);
            FrameCharacterForEvidence();
            // Pay the first offscreen render/shader warmup before timed Run
            // samples; otherwise the first capture can miss half a run cycle.
            Texture2D warmup = CaptureFrameBuffered();
            Destroy(warmup);
            yield return null;
            yield return new WaitForEndOfFrame();

            yield return CaptureLocomotion("run", CharacterAction.Run, 0.8f);
            yield return CaptureAction("jump-ascent", CharacterAction.Jump, float.PositiveInfinity, 0.44f, true);
            yield return CaptureAction("jump-fall", CharacterAction.Fall, float.PositiveInfinity, 0.6f, false);
            yield return CaptureAction("jump-land", CharacterAction.Land, 0.18f, 0.22f, false);
            yield return CaptureActionAtTimes(
                "sword",
                CharacterAction.Sword,
                0.34f,
                new[] { 0f, 0.04f, 0.08f, 0.12f, 0.20f, 0.34f, 0.50f, 0.80f, 1.10f },
                true);
            yield return CaptureSwordToRunTransition();
            yield return CaptureAction("magic-charge", CharacterAction.MagicCharge, 0.65f, 0.72f, true);
            yield return CaptureAction("magic-release", CharacterAction.MagicRelease, 0.36f, 0.9f, false);

            string reportPath = Path.Combine(outputDirectory, "motion-progress.json");
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            Debug.Log("CoffeeGAME Meshy motion evidence captured: " + reportPath);
            sceneCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
            sceneCamera.orthographicSize = originalOrthographicSize;
            if (cameraRig != null) cameraRig.enabled = cameraRigWasEnabled;
            Time.timeScale = previousTimeScale;
            yield return null;
            Application.Quit(0);
        }

        private void FrameCharacterForEvidence()
        {
            originalCameraPosition = sceneCamera.transform.position;
            originalCameraRotation = sceneCamera.transform.rotation;
            originalOrthographicSize = sceneCamera.orthographicSize;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    // Fit the body using named joints in world space. Auxiliary
                    // end bones and imported prop bounds can include outliers.
                    foreach (Transform bone in skinned.bones)
                    {
                        if (bone == null || (bone.name != "Head" && bone.name != "Hips" &&
                            bone.name != "LeftFoot" && bone.name != "RightFoot" &&
                            bone.name != "LeftHand" && bone.name != "RightHand")) continue;
                        Vector3 world = bone.position;
                        if (!hasBounds)
                        {
                            bounds = new Bounds(world, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(world);
                        }
                    }
                }
            }

            if (!hasBounds)
            {
                return;
            }

            // Leave room above the head bone and around the garment silhouette.
            bounds.Expand(0.4f);
            report.framingCenter = bounds.center;
            report.framingSize = bounds.size;
            float height = Mathf.Max(bounds.size.y, 0.5f);
            if (height > 5f) throw new InvalidOperationException("Character framing exceeds the expected humanoid scale.");
            if (sceneCamera.orthographic)
            {
                Vector3 toBounds = bounds.center - sceneCamera.transform.position;
                sceneCamera.transform.position += toBounds -
                    Vector3.Project(toBounds, sceneCamera.transform.forward);
                sceneCamera.orthographicSize = height / (2f * 0.55f);
            }
            else
            {
                float halfFov = Mathf.Max(sceneCamera.fieldOfView * 0.5f * Mathf.Deg2Rad, 0.01f);
                float distance = height / (2f * Mathf.Tan(halfFov) * 0.55f);
                sceneCamera.transform.position = bounds.center - sceneCamera.transform.forward * distance;
            }
        }

        private IEnumerator CaptureLocomotion(string sequence, CharacterAction action, float duration)
        {
            visual.ResetState(Vector3.back);
            visual.SetLocomotion(action, 1f);
            yield return CaptureTimeline(sequence, action, duration);
        }

        private IEnumerator CaptureAction(
            string sequence,
            CharacterAction action,
            float requestedDuration,
            float evidenceDuration,
            bool resetFirst)
        {
            if (resetFirst)
            {
                visual.ResetState(Vector3.back);
            }
            visual.PlayAction(action, requestedDuration);
            yield return CaptureTimeline(sequence, action, evidenceDuration);
        }

        private IEnumerator CaptureActionAtTimes(
            string sequence,
            CharacterAction action,
            float requestedDuration,
            float[] sampleSeconds,
            bool resetFirst)
        {
            if (resetFirst)
            {
                visual.ResetState(Vector3.back);
            }
            visual.PlayAction(action, requestedDuration);
            yield return CaptureTimeline(sequence, action, sampleSeconds);
        }

        private IEnumerator CaptureTimeline(string sequence, CharacterAction action, float duration)
        {
            var sampleSeconds = new float[Fractions.Length];
            for (int index = 0; index < Fractions.Length; index++)
            {
                sampleSeconds[index] = duration * Fractions[index];
            }
            yield return CaptureTimeline(sequence, action, sampleSeconds);
        }

        private IEnumerator CaptureTimeline(string sequence, CharacterAction action, float[] sampleSeconds)
        {
            if (report.denseVideoFrames && sampleSeconds.Length > 0)
            {
                // Keep the requested diagnostic checkpoints and add frames for
                // review video. The report records actual time for every frame;
                // encoding remains deferred until the sequence has finished.
                var times = new List<float>(sampleSeconds);
                float end = sampleSeconds[sampleSeconds.Length - 1];
                for (int frame = 0; frame / 30f < end; frame++)
                {
                    float time = frame / 30f;
                    if (!times.Exists(existing => Mathf.Abs(existing - time) < 0.001f)) times.Add(time);
                }
                times.Sort();
                sampleSeconds = times.ToArray();
            }
            float started = Time.realtimeSinceStartup;
            var buffered = new List<BufferedFrame>(sampleSeconds.Length);
            for (int index = 0; index < sampleSeconds.Length; index++)
            {
                float targetSeconds = sampleSeconds[index];
                float elapsed = Time.realtimeSinceStartup - started;
                if (targetSeconds > elapsed)
                {
                    yield return new WaitForSecondsRealtime(targetSeconds - elapsed);
                }
                yield return new WaitForEndOfFrame();
                float actualSeconds = Time.realtimeSinceStartup - started;

                Animator animator = visual.Animator;
                bool inTransition = animator != null && animator.IsInTransition(0);
                AnimatorStateInfo state = inTransition
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                string fileName = $"{sequence}-{index:D2}.png";
                buffered.Add(new BufferedFrame
                {
                    Path = Path.Combine(outputDirectory, fileName),
                    Image = CaptureFrameBuffered(),
                });
                report.samples.Add(new Sample
                {
                    sequence = sequence,
                    action = action.ToString(),
                    index = index,
                    requestedSeconds = targetSeconds,
                    actualSeconds = actualSeconds,
                    expectedProgress = sampleSeconds.Length > 1
                        ? targetSeconds / sampleSeconds[sampleSeconds.Length - 1]
                        : 0f,
                    animatorProgress = state.normalizedTime,
                    animatorSpeed = animator != null ? animator.speed : 0f,
                    inTransition = inTransition,
                    fullPathHash = state.fullPathHash,
                    shortNameHash = state.shortNameHash,
                    image = fileName,
                });
            }
            yield return WriteBufferedFrames(buffered);
        }

        private IEnumerator CaptureSwordToRunTransition()
        {
            visual.ResetState(Vector3.back);
            visual.PlayAction(CharacterAction.Sword, 0.34f);
            float started = Time.realtimeSinceStartup;
            float[] sampleSeconds = { 0.36f, 0.50f, 0.75f };
            var buffered = new List<BufferedFrame>(sampleSeconds.Length);
            for (int index = 0; index < sampleSeconds.Length; index++)
            {
                float elapsed = Time.realtimeSinceStartup - started;
                if (sampleSeconds[index] > elapsed)
                {
                    yield return new WaitForSecondsRealtime(sampleSeconds[index] - elapsed);
                }
                if (index == 0)
                {
                    visual.SetLocomotion(CharacterAction.Run, 1f);
                }
                yield return new WaitForEndOfFrame();
                float actualSeconds = Time.realtimeSinceStartup - started;
                Animator animator = visual.Animator;
                bool inTransition = animator != null && animator.IsInTransition(0);
                AnimatorStateInfo state = inTransition
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                string fileName = $"sword-to-run-{index:D2}.png";
                buffered.Add(new BufferedFrame
                {
                    Path = Path.Combine(outputDirectory, fileName),
                    Image = CaptureFrameBuffered(),
                });
                report.samples.Add(new Sample
                {
                    sequence = "sword-to-run",
                    action = CharacterAction.Run.ToString(),
                    index = index,
                    requestedSeconds = sampleSeconds[index],
                    actualSeconds = actualSeconds,
                    expectedProgress = sampleSeconds[index] / sampleSeconds[sampleSeconds.Length - 1],
                    animatorProgress = state.normalizedTime,
                    animatorSpeed = animator != null ? animator.speed : 0f,
                    inTransition = inTransition,
                    fullPathHash = state.fullPathHash,
                    shortNameHash = state.shortNameHash,
                    image = fileName,
                });
            }
            yield return WriteBufferedFrames(buffered);
        }

        private Texture2D CaptureFrameBuffered()
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                var request = new RenderPipeline.StandardRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(sceneCamera, request);
                RenderTexture.active = target;
                image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
                image.Apply();
                return image;
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                Destroy(target);
            }
        }

        private static IEnumerator WriteBufferedFrames(List<BufferedFrame> frames)
        {
            foreach (BufferedFrame frame in frames)
            {
                File.WriteAllBytes(frame.Path, frame.Image.EncodeToPNG());
                UnityEngine.Object.Destroy(frame.Image);
                yield return null;
            }
        }
    }
}
#endif
