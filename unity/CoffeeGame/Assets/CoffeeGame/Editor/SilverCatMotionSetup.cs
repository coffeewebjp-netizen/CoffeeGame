using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoffeeGame.Presentation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoffeeGame.Editor
{
    // Authors a separate animation library on the existing 24-bone cat skin.
    // Original Meshy FBX, controller, textures and vertex weights remain untouched.
    public static class SilverCatMotionSetup
    {
        public const string Folder = "Assets/CoffeeGame/Resources/Animations/Characters/SilverCatV14";
        public const string ControllerPath = Folder + "/SilverCatMotionV14.controller";
        public const string ElementFolder = "Assets/CoffeeGame/Resources/Animations/Characters/SilverCatV17";
        public const string ElementControllerPath = ElementFolder + "/SilverCatElementsV17.controller";
        private const float Fps = 60f;

        [MenuItem("CoffeeGAME/Assets/Author Cat Motion V14", priority = 71)]
        public static void Configure() => Configure(false);
        public static void ConfigureElements() => Configure(true);
        private static void Configure(bool elements)
        {
            string folder = elements ? ElementFolder : Folder;
            string controllerPath = elements ? ElementControllerPath : ControllerPath;
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SilverCatAssetSetup.ModelPath);
            if (prefab == null) throw new InvalidOperationException("Missing existing cat model");
            var source = AssetDatabase.LoadAllAssetsAtPath(SilverCatAssetSetup.ModelPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var model = UnityEngine.Object.Instantiate(prefab);
            model.name = "Cat motion authoring";
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.enabled = false;
            // FBX scene transforms were saved during an animation. Recover the
            // true skin bind pose before solving feet/arms and authoring clips.
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var bind = skin.sharedMesh.bindposes;
            var bindWorld = skin.bones.Select((b, i) => new { bone = b, matrix = skin.transform.localToWorldMatrix * bind[i].inverse })
                .OrderBy(pair => AnimationUtility.CalculateTransformPath(pair.bone, model.transform).Count(c => c == '/')).ToArray();
            foreach (var pair in bindWorld) pair.bone.SetPositionAndRotation(pair.matrix.GetColumn(3), pair.matrix.rotation);
            try
            {
                var rig = new Rig(model);
                var clips = new Dictionary<string, AnimationClip>();
                var report = new List<ClipReport>();
                AnimationClip nativeRun = source.First(c => Leaf(c.name) == "Run");
                void Author(string name, float seconds, bool loop, Action<float> pose)
                {
                    var clip = new AnimationClip { name = name, frameRate = Fps };
                    var curves = rig.Bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
                    int frames = Mathf.RoundToInt(seconds * Fps);
                    float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
                    Vector3 firstHand = Vector3.zero; float handTravel = 0f;
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float p = frame / (float)frames;
                        rig.Reset(); pose(p);
                        for (int b = 0; b < rig.Bones.Length; b++)
                        {
                            Transform bone = rig.Bones[b]; Vector3 position = bone.localPosition; Quaternion rotation = bone.localRotation;
                            float[] values = { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w };
                            for (int v = 0; v < values.Length; v++) curves[b][v].AddKey(frame * seconds / frames, values[v]);
                        }
                        minY = Mathf.Min(minY, rig["LeftFoot"].position.y, rig["RightFoot"].position.y);
                        maxY = Mathf.Max(maxY, rig["Head"].position.y);
                        if (frame == 0) firstHand = rig["RightHand"].position;
                        handTravel = Mathf.Max(handTravel, Vector3.Distance(firstHand, rig["RightHand"].position));
                    }
                    string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                    for (int b = 0; b < rig.Bones.Length; b++)
                    {
                        string path = AnimationUtility.CalculateTransformPath(rig.Bones[b], model.transform);
                        for (int v = 0; v < properties.Length; v++)
                        {
                            var curve = curves[b][v];
                            for (int key = 0; key < curve.length; key++)
                            {
                                AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                                AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.ClampedAuto);
                            }
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[v]), curve);
                        }
                    }
                    clip.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = loop; settings.loopBlend = loop;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    string asset = folder + "/" + name + ".anim";
                    var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                    if (existing == null) AssetDatabase.CreateAsset(clip, asset);
                    else { EditorUtility.CopySerialized(clip, existing); UnityEngine.Object.DestroyImmediate(clip); clip = existing; }
                    clips.Add(name, clip);
                    report.Add(new ClipReport { name = name, seconds = seconds, loop = loop, minimumFootY = minY, headY = maxY, handTravel = handTravel });
                }

                Author("Idle", 3.2f, true, p => rig.Idle(p));
                Author("Walk", .80f, true, p => rig.Locomotion(p, false, null));
                Author("Run", elements ? .54f : .60f, true, p => { if(elements) rig.ElementRun(p); else rig.Locomotion(p, true, nativeRun); });
                Author("Jump", .34f, false, p => rig.Airborne(p, false));
                Author("Fall", .32f, false, p => rig.Airborne(p, true));
                Author("Land", .30f, false, p => rig.Landing(p));
                Author("MagicCharge", 1.20f, false, p => rig.Charge(p));
                Author("MagicRelease", .48f, false, p => { if(elements) rig.ThunderRelease(p); else rig.MajorRelease(p); });
                Author("CatVolley1", .38f, false, p => rig.Volley(p, 1));
                Author("CatVolley2", .38f, false, p => rig.Volley(p, 2));
                Author("CatVolley3", .62f, false, p => rig.Volley(p, 3));
                Author("CatTimeStop", .65f, false, p => rig.TimeStop(p));
                Author("Dodge", .814f, false, p => rig.Dodge(p));
                Author("Hurt", .34f, false, p => rig.Hurt(p));
                Author("Defeated", 1f, false, p => rig.Defeated(p));
                if(elements) {
                    Author("AirSlash", .70f, false, p => rig.WindAir(p));
                    Author("Plunge", .32f, false, p => rig.EarthDrop(p));
                    Author("CatEarthLand", 1f, false, p => rig.EarthLand(p));
                }

                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)
                    ?? AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.name = elements ? "SilverCatElementsV17" : "SilverCatMotionV14";
                var machine = controller.layers[0].stateMachine;
                foreach (var old in machine.states) machine.RemoveState(old.state);
                foreach (CharacterAction action in Enum.GetValues(typeof(CharacterAction)))
                {
                    string name = action.ToString();
                    string fallback = action == CharacterAction.SpinRelease ? "CatTimeStop" :
                        action == CharacterAction.SpinCharge ? "MagicCharge" :
                        action == CharacterAction.Sword || action == CharacterAction.AirSlash ? "CatVolley1" : "Idle";
                    var state = machine.AddState(name); state.motion = clips.TryGetValue(name, out var clip) ? clip : clips[fallback];
                    state.writeDefaultValues = true;
                    if (action == CharacterAction.Idle) machine.defaultState = state;
                }
                foreach (string name in new[] { "CatVolley1", "CatVolley2", "CatVolley3", "CatTimeStop" })
                { var state = machine.AddState(name); state.motion = clips[name]; state.writeDefaultValues = true; }
                if(elements) { var state=machine.AddState("CatEarthLand"); state.motion=clips["CatEarthLand"]; state.writeDefaultValues=true; }
                EditorUtility.SetDirty(controller); EditorUtility.SetDirty(machine); AssetDatabase.SaveAssets();
                string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../art/3d/trials/meshy-rival/" + (elements ? "motion-v17" : "motion-v14") + "/audit.json"));
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(new Audit {
                    source = SilverCatAssetSetup.ModelPath,
                    sourceClips = source.Select(c => Leaf(c.name) + ": " + c.length.ToString("F3") + "s").ToArray(),
                    boneNames = rig.Bones.Select(b => b.name).ToArray(), authored = report.ToArray()
                }, true));
                Validate(controllerPath); Debug.Log(elements ? "Cat Elements V17 authored: 18 clips; original V14 preserved." : "Cat Motion V14 authored: 15 clips / 22 states; originals preserved.");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }

        public static void Validate() => Validate(ControllerPath);
        public static void ValidateElements() => Validate(ElementControllerPath);
        private static void Validate(string path)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) throw new InvalidOperationException("Cat Motion V14 controller missing");
            var required = new[] { "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Dodge", "MagicCharge", "MagicRelease", "CatVolley1", "CatVolley2", "CatVolley3", "CatTimeStop", "Hurt", "Defeated" };
            if (path == ElementControllerPath) required = required.Concat(new[] { "AirSlash", "Plunge", "CatEarthLand" }).ToArray();
            foreach (string stateName in required)
            {
                var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == stateName).state;
                if (!(state.motion is AnimationClip clip) || clip.length < .1f || AnimationUtility.GetCurveBindings(clip).Length < 100)
                    throw new InvalidOperationException("Missing or empty cat motion " + stateName);
            }
        }
        public static void RenderGallery()
        {
            Validate();
            var host = new GameObject("Cat V14 preview");
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SilverCatAssetSetup.ModelPath), host.transform);
            var camera = new GameObject("Cat preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .20f);
            camera.orthographic = true; camera.orthographicSize = .82f;
            var light = new GameObject("Cat preview light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(35, -35, 0);
            var visual = host.AddComponent<ModelCharacterVisual>();
            visual.Initialize(model.transform, AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath), CharacterModelStyle.SilverCat, camera);
            visual.Animator.enabled = false;
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            var proxy = new GameObject("Baked preview"); proxy.transform.SetParent(skin.transform, false);
            var mesh = new Mesh(); proxy.AddComponent<MeshFilter>().sharedMesh = mesh;
            proxy.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials; skin.enabled = false;
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../art/3d/trials/meshy-rival/motion-v14/gallery"));
            Directory.CreateDirectory(output);
            var target = new RenderTexture(384, 512, 24); target.Create(); var previous = RenderTexture.active;
            try
            {
                foreach (var clip in AssetDatabase.FindAssets("t:AnimationClip", new[] { Folder }).Select(g => AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g))))
                {
                    var sheet = new Texture2D(1536, 512, TextureFormat.RGB24, false);
                    for (int frame = 0; frame < 4; frame++)
                    {
                        clip.SampleAnimation(model, clip.length * (frame == 0 ? .05f : frame == 2 ? .92f : .50f));
                        skin.BakeMesh(mesh, true);
                        Debug.Log($"CATPOSE {clip.name} {frame} hand={model.GetComponentsInChildren<Transform>().First(t => t.name == "RightHand").position} mesh={mesh.bounds} scale={skin.transform.lossyScale}");
                        camera.transform.position = frame == 3 ? new Vector3(2.5f, .73f, -.4f) : new Vector3(.3f, .73f, -2.5f);
                        camera.transform.LookAt(new Vector3(0, .65f, 0));
                        RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                        RenderTexture.active = target;
                        sheet.ReadPixels(new Rect(0, 0, 384, 512), frame * 384, 0);
                    }
                    sheet.Apply(); File.WriteAllBytes(Path.Combine(output, clip.name + ".png"), sheet.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(sheet);
                }
            }
            finally
            {
                RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject);
            }
        }
        public static void ConfigureAndRender() { Configure(); RenderGallery(); }
        private static string Leaf(string name) => name.Split('|').Last();
        [Serializable] private sealed class Audit { public string source; public string[] sourceClips; public string[] boneNames; public ClipReport[] authored; }
        [Serializable] private sealed class ClipReport { public string name; public float seconds; public bool loop; public float minimumFootY; public float headY; public float handTravel; }

        private sealed class Rig
        {
            private readonly GameObject model;
            public readonly Transform[] Bones;
            private readonly Vector3[] positions;
            private readonly Quaternion[] rotations;
            private readonly Vector3[] scales;
            private readonly Dictionary<string, Transform> bones;
            private readonly Vector3 hipRest;
            private readonly float footY;
            private readonly Quaternion leftFootRotation, rightFootRotation;
            private const float H = 1.3f;
            public Transform this[string name] => bones[name];
            public Rig(GameObject root)
            {
                model = root;
                Bones = root.GetComponentsInChildren<Transform>().Where(t => t != root.transform && t.GetComponent<Renderer>() == null).ToArray();
                bones = Bones.ToDictionary(t => t.name);
                positions = Bones.Select(t => t.localPosition).ToArray(); rotations = Bones.Select(t => t.localRotation).ToArray(); scales = Bones.Select(t => t.localScale).ToArray();
                hipRest = this["Hips"].position;
                footY = Mathf.Min(this["LeftFoot"].position.y, this["RightFoot"].position.y);
                leftFootRotation = this["LeftFoot"].rotation; rightFootRotation = this["RightFoot"].rotation;
            }
            public void Reset() { for (int i = 0; i < Bones.Length; i++) { Bones[i].localPosition = positions[i]; Bones[i].localRotation = rotations[i]; Bones[i].localScale = scales[i]; } }
            private static float S(float p) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(p));
            private static float Pulse(float p) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(p));
            private void Body(float crouch, float lean = 0, float twist = 0)
            {
                this["Hips"].position = hipRest + Vector3.down * crouch * H;
                this["Spine02"].rotation = Quaternion.Euler(lean, twist * .45f, 0) * this["Spine02"].rotation;
                this["Spine"].rotation = Quaternion.AngleAxis(twist * .55f, Vector3.up) * this["Spine"].rotation;
                this["Head"].rotation = Quaternion.Euler(-lean * .65f, -twist * .4f, 0) * this["Head"].rotation;
            }
            private void Legs(float leftZ = 0, float rightZ = 0, float leftLift = 0, float rightLift = 0, float width = .085f)
            {
                Leg("Left", new Vector3(-width * H, footY + leftLift * H, leftZ * H));
                Leg("Right", new Vector3(width * H, footY + rightLift * H, rightZ * H));
            }
            private void Leg(string side, Vector3 target)
            {
                Solve(this[side + "UpLeg"], this[side + "Leg"], this[side + "Foot"], target, Vector3.forward);
                this[side + "Foot"].rotation = side == "Left" ? leftFootRotation : rightFootRotation;
            }
            private void Hand(string side, Vector3 target, float palm = 0)
            {
                float sign = side == "Left" ? -1 : 1;
                Solve(this[side + "Arm"], this[side + "ForeArm"], this[side + "Hand"], target * H,
                    new Vector3(sign, -.35f, -.2f));
                this[side + "Hand"].rotation = Quaternion.AngleAxis(palm, Vector3.forward) * this[side + "Hand"].rotation;
            }
            private void RelaxArms(float breath = 0)
            {
                Hand("Left", new Vector3(-.17f, .42f + breath, .08f), -10);
                Hand("Right", new Vector3(.17f, .43f + breath, .10f), 10);
            }
            public void Idle(float p)
            {
                float breath = Mathf.Sin(p * Mathf.PI * 2);
                Body(.018f - breath * .0025f, 2f, 2f * breath); Legs(.012f, -.012f); RelaxArms(.002f * breath);
            }
            public void Locomotion(float p, bool running, AnimationClip native)
            {
                if (native != null) native.SampleAnimation(model, Mathf.Repeat(p, 1f) * native.length);
                // CharacterController owns travel. Bake in-place hips and stance feet;
                // native Meshy run shoulder/elbow motion remains in the upper body.
                float cycle = p * Mathf.PI * 2;
                Body(running ? .067f + .012f * Mathf.Cos(cycle * 2) : .03f + .005f * Mathf.Cos(cycle * 2), running ? 8f : 3f, Mathf.Sin(cycle) * (running ? 5 : 3));
                FootPhase("Left", Mathf.Repeat(p, 1), running);
                FootPhase("Right", Mathf.Repeat(p + .5f, 1), running);
                if (!running)
                {
                    Hand("Left", new Vector3(-.18f, .41f, .08f - .10f * Mathf.Cos(cycle)));
                    Hand("Right", new Vector3(.18f, .41f, .08f + .10f * Mathf.Cos(cycle)));
                }
            }
            private void FootPhase(string side, float p, bool run)
            {
                float reach = run ? .232f : .195f;
                float z = p < .5f ? Mathf.Lerp(reach, -reach, p * 2) : Mathf.Lerp(-reach, reach, S((p - .5f) * 2));
                float lift = p < .5f ? 0 : Mathf.Sin((p - .5f) * Mathf.PI * 2) * (run ? .09f : .025f);
                float sign = side == "Left" ? -1 : 1;
                Leg(side, new Vector3(sign * .075f * H, footY + lift * H, z * H));
            }
            public void Airborne(float p, bool falling)
            {
                float fold = falling ? Mathf.Lerp(.07f, .015f, S(p)) : Mathf.Lerp(.01f, .14f, S(p));
                Body(.018f, falling ? 9 : 14, falling ? 0 : -5);
                Legs(.11f, -.015f, fold, fold * .4f, .10f);
                Hand("Left", new Vector3(-.27f, falling ? .58f : .60f, .13f));
                Hand("Right", new Vector3(.27f, falling ? .58f : .67f, .12f));
            }
            public void ElementRun(float p)
            {
                float phase=p*Mathf.PI*2;
                Body(.07f+.012f*Mathf.Cos(phase*2),30,9*Mathf.Sin(phase));
                this["Hips"].position += Vector3.forward*.075f*H;
                FootPhase("Left",Mathf.Repeat(p,1),true); FootPhase("Right",Mathf.Repeat(p+.5f,1),true);
                Hand("Left",new Vector3(-.17f,.53f+.06f*Mathf.Cos(phase),.12f-.21f*Mathf.Cos(phase)));
                Hand("Right",new Vector3(.17f,.53f-.06f*Mathf.Cos(phase),.12f+.21f*Mathf.Cos(phase)));
            }
            public void ThunderRelease(float p)
            {
                float strike=S(p/.28f),relax=S((p-.6f)/.4f);
                Body(.04f+.055f*strike*(1-relax),16*strike*(1-relax)); Legs(.08f,-.08f,0,0,.13f);
                Hand("Left",new Vector3(-.2f-.12f*strike,Mathf.Lerp(.88f,.44f,strike),.21f));
                Hand("Right",new Vector3(.2f+.12f*strike,Mathf.Lerp(.88f,.44f,strike),.21f));
            }
            public void WindAir(float p)
            {
                float sweep=S(p/.42f),ease=S((p-.6f)/.4f);
                Body(.015f,12-18*Mathf.Sin(p*Mathf.PI),-32+65*sweep-25*ease);
                Legs(.14f,-.10f,.19f,.06f,.10f);
                Hand("Left",new Vector3(Mathf.Lerp(-.42f,-.12f,sweep),.64f,.20f+.22f*sweep));
                Hand("Right",new Vector3(Mathf.Lerp(.35f,.12f,sweep),.74f,.10f+.36f*sweep));
            }
            public void EarthDrop(float p)
            {
                float fold=S(p); Body(.06f,28,0); Legs(.06f,-.04f,.18f,.13f,.14f);
                Hand("Left",new Vector3(-.18f,.44f-.13f*fold,.27f));
                Hand("Right",new Vector3(.18f,.44f-.13f*fold,.27f));
            }
            public void EarthLand(float p)
            {
                float weight=1-S((p-.65f)/.35f);
                Body(.025f+.25f*weight,48*weight); Legs(.09f,-.04f,0,0,.155f);
                Hand("Left",new Vector3(-.22f,Mathf.Lerp(.43f,.12f,weight),.27f));
                Hand("Right",new Vector3(.22f,Mathf.Lerp(.43f,.12f,weight),.27f));
            }
            public void Landing(float p)
            {
                float weight = p < .24f ? Mathf.Lerp(.45f, 1, S(p / .24f)) : 1 - S((p - .24f) / .76f);
                Body(.02f + .105f * weight, 18 * weight); Legs(.035f, -.035f, 0, 0, .11f);
                Hand("Left", new Vector3(-.22f, .44f - .06f * weight, .17f));
                Hand("Right", new Vector3(.22f, .44f - .06f * weight, .17f));
            }
            public void Charge(float p)
            {
                float rise = S(p); float open = Mathf.Sin(p * Mathf.PI);
                Body(.02f + .035f * open, -6f * rise, -12 * open); Legs(.065f, -.06f, 0, 0, .10f);
                Hand("Left", new Vector3(-.14f - .14f * open, Mathf.Lerp(.46f, .84f, rise), .20f));
                Hand("Right", new Vector3(.14f + .14f * open, Mathf.Lerp(.46f, .84f, rise), .20f));
            }
            public void MajorRelease(float p)
            {
                float impact = p < .20f ? S(p / .20f) : 1 - .7f * S((p - .20f) / .80f);
                Body(.035f + .025f * impact, 12 * impact); Legs(.07f, -.06f, 0, 0, .10f);
                Hand("Left", new Vector3(-.15f, Mathf.Lerp(.84f, .62f, impact), Mathf.Lerp(.20f, .43f, impact)));
                Hand("Right", new Vector3(.15f, Mathf.Lerp(.84f, .62f, impact), Mathf.Lerp(.20f, .43f, impact)));
            }
            public void Volley(float p, int stage)
            {
                float release = 1 - S(p); float recoil = Pulse(p);
                bool left = stage == 2; bool both = stage == 3;
                Body(.026f + recoil * .02f, release * 7, both ? 0 : (left ? 1 : -1) * (release * 18 - recoil * 9));
                Legs(left ? -.035f : .065f, left ? .065f : -.035f, 0, 0, .10f);
                RelaxArms();
                if (left || both) Hand("Left", new Vector3(-.12f, .46f + .17f * release, .12f + .34f * release));
                if (!left || both) Hand("Right", new Vector3(.12f, .46f + .17f * release, .12f + .34f * release));
            }
            public void TimeStop(float p)
            {
                float lift = p < .55f ? S(p / .55f) : 1 - .7f * S((p - .55f) / .45f);
                Body(.02f + .018f * Pulse(p), -8 * lift, 10 * lift); Legs(.02f, -.04f, 0, 0, .10f);
                Hand("Right", new Vector3(.10f + .06f * lift, Mathf.Lerp(.53f, 1.03f, lift), .18f));
                Hand("Left", new Vector3(-.20f - .10f * lift, .52f, .18f));
            }
            public void Dodge(float p)
            {
                float tuck = Mathf.Sin(Mathf.PI * S(p));
                Body(.04f, 18 * tuck); Legs(.10f, -.02f, .22f * tuck, .13f * tuck, .075f);
                Hand("Left", new Vector3(-.12f, .60f, .22f)); Hand("Right", new Vector3(.12f, .60f, .22f));
                var hips = this["Hips"];
                hips.rotation = Quaternion.AngleAxis(360 * p, new Vector3(1, .18f, .35f).normalized) * hips.rotation;
            }
            public void Hurt(float p)
            {
                float recoil = p < .2f ? S(p / .2f) : 1 - S((p - .2f) / .8f);
                Body(.02f + .025f * recoil, -18 * recoil, -12 * recoil); Legs(.025f, -.07f, 0, 0, .10f);
                Hand("Left", new Vector3(-.10f, .53f, .17f)); Hand("Right", new Vector3(.20f, .48f, .13f));
            }
            public void Defeated(float p)
            {
                float sink = S(p);
                Body(.03f + .25f * sink, 48 * sink, -8 * sink);
                Legs(.07f, -.10f, 0, 0, .105f);
                Hand("Left", new Vector3(-.20f, Mathf.Lerp(.42f, .13f, sink), .18f));
                Hand("Right", new Vector3(.15f, Mathf.Lerp(.42f, .12f, sink), .24f));
            }
            private static void Solve(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
            {
                Vector3 start = upper.position;
                float a = Vector3.Distance(start, lower.position), b = Vector3.Distance(lower.position, end.position);
                Vector3 delta = target - start; float length = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .001f, a + b - .001f);
                Vector3 forward = delta.normalized;
                Vector3 bend = Vector3.ProjectOnPlane(pole, forward).normalized;
                if (bend.sqrMagnitude < .01f) bend = Vector3.Cross(forward, Vector3.right).normalized;
                float along = (a * a + length * length - b * b) / (2 * length);
                Vector3 elbow = start + forward * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
                upper.rotation = Quaternion.FromToRotation(lower.position - start, elbow - start) * upper.rotation;
                lower.rotation = Quaternion.FromToRotation(end.position - lower.position, target - lower.position) * lower.rotation;
            }
        }
    }
}
