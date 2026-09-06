#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using CoffeeGame.Actors;
using CoffeeGame.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Presentation
{
    // Explicit development command only. Normal play never enters this path.
    public sealed class ForestEvidenceCapture : MonoBehaviour
    {
        private Camera sceneCamera;
        private Transform player;
        private string directory;

        public static void Begin(GameObject host, Camera camera, Transform hero, string output)
        {
            var capture = host.AddComponent<ForestEvidenceCapture>();
            capture.sceneCamera = camera;
            capture.player = hero;
            capture.directory = Path.GetFullPath(output);
            Directory.CreateDirectory(capture.directory);
            Application.runInBackground = true;
            capture.StartCoroutine(capture.Run());
        }

        private IEnumerator Run()
        {
            if (player == null) { Debug.LogError("Forest capture requires the player."); Application.Quit(2); yield break; }
            player.GetComponent<PlayerMotor3D>().enabled = false;
            player.GetComponent<PlayerCombatController>().enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            // Isolated visual benchmark, no run is started and no rewards awarded.
            Time.timeScale = 1f;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var rig = sceneCamera.GetComponent<FixedCameraRig>();
            Vector3 initial = player.position;
            yield return new WaitForSecondsRealtime(2f);
            var records = new ViewRecord[6];
            var benchmarkTarget = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            benchmarkTarget.Create();
            var fence = new Texture2D(1,1,TextureFormat.RGB24,false);
            for (int view = 0; view < records.Length; view++)
            {
                player.position = view < 4 ? initial : view == 4 ? new Vector3(-10f,initial.y,5f) : new Vector3(12f,initial.y,-4f);
                rig.SetOrbitYaw(view < 4 ? view * 90f : 0f, true);
                yield return new WaitForSecondsRealtime(1f);
                float[] frames = new float[180];
                for (int warmup = 0; warmup < 12; warmup++) { RenderAndWait(benchmarkTarget,fence); yield return null; }
                for (int frame = 0; frame < frames.Length; frame++)
                {
                    yield return null;
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    RenderAndWait(benchmarkTarget,fence);
                    frames[frame] = (float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                }
                Array.Sort(frames);
                float total = 0;
                foreach (float frame in frames) total += frame;
                records[view] = new ViewRecord {
                    name = view < 4 ? "center-" + (view * 90) : view == 4 ? "west" : "east",
                    meanMs = total / frames.Length, medianMs = frames[90], p95Ms = frames[171],
                    position = player.position, yaw = rig.OrbitYawDegrees
                };
                Capture(records[view].name);
            }
            File.WriteAllText(Path.Combine(directory,"visual-benchmark.json"), JsonUtility.ToJson(new Report {
                forest = ForestArenaVisuals.UseForest, device = SystemInfo.graphicsDeviceName,
                width = benchmarkTarget.width, height = benchmarkTarget.height, framesPerView = 180,
                note = "Explicit offscreen URP render plus synchronous one-pixel GPU readback, 12 warmup frames/view. Includes CPU submission/readback overhead; not normal-play FPS or a combat/mobile benchmark.", views = records
            },true));
            benchmarkTarget.Release(); Destroy(benchmarkTarget); Destroy(fence);
            Debug.Log("CoffeeGAME forest evidence completed: " + directory);
            Application.Quit(0);
        }

        private void RenderAndWait(RenderTexture target, Texture2D fence)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(sceneCamera,new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                fence.ReadPixels(new Rect(0,0,1,1),0,0,false);
            }
            finally { RenderTexture.active = previous; }
        }

        private void Capture(string name)
        {
            var target = new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Texture2D image = null;
            try
            {
                target.Create();
                RenderPipeline.SubmitRenderRequest(sceneCamera,new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image = new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(directory,name + ".png"),image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (image != null) Destroy(image);
                target.Release(); Destroy(target);
            }
        }

        [Serializable] private sealed class Report
        {
            public bool forest;
            public string device, note;
            public int width, height, framesPerView;
            public ViewRecord[] views;
        }
        [Serializable] private sealed class ViewRecord
        {
            public string name;
            public float meanMs, medianMs, p95Ms, yaw;
            public Vector3 position;
        }
    }
}
#endif
