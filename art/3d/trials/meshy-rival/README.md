# Meshy silver rival trial (ORC-20260830-009)

Paid Meshy look-and-walk test of the approved rival portrait. This does **not** replace the playable HD-2D heroine, and it does **not** swap the live rival encounter 2D portrait until a later step.

Source portrait: `art/concepts/rivals/rival-weakness-challenger-v1.png` (in-game: `Art/UI/Rivals/rival_weakness_challenger_v1`).

## What to upload

Empty-hand T-pose, both arms out. The floating question-mark book is omitted on purpose so Meshy does not fuse it into a hand blob. We can add the book later as a prop.

| Meshy slot | File |
| --- | --- |
| Main / Front | `upload/tpose-sym/01-front.jpg` |
| Right | `upload/tpose-sym/02-right.jpg` |
| Back | `upload/tpose-sym/03-back.jpg` |
| Left | `upload/tpose-sym/04-left.jpg` |

Identity backup (do not mix with T-pose in one generation): `upload/identity/01-portrait.png`.

Known input limits:

- Profile hands point rather than lie fully open. Meshy T-Pose control still expects the arm pose.
- Coat emblems are slightly busier on the back view than the front.
- Tail is on her left. Keep that across views.

## Owner: generate (about 20 credits)

1. Meshy workspace → **Image to 3D**.
2. Model Type: **Standard**. Do not use Smart Topology.
3. AI Model: latest shown (**Meshy 6** or **Meshy 7**).
4. Upload `01-front.jpg` as the main image.
5. Turn **Multi-view** on. Right / Back / Left get the other three files.
6. Before Generate, set Pose to **T-Pose**.
7. Leave Auto Split off.
8. Click **Generate**. Failed technical errors refund credits.

Judge face, white hair, cat ears, white coat, tail. If it is not the silver rival, regenerate once or twice before rigging.

## Owner: texture (about 10 credits)

1. Keep image input and Multi-view on. Reuse the same four T-pose images.
2. Keep **PBR maps** on. Texture **4K**.
3. Click texture. Do not turn Multi-view off.

## Owner: remesh, rig, animate

1. **Remesh is off.** If a remesh punches holes in the coat, discard it and rig the original textured mesh.
2. **Animate** / Auto-Rig. Character type: **Humanoid**. Center, face forward, feet on the ground.
3. Auto-Rig.
4. Add library clips: **Idle**, **Walk**, **Run**. Jump if easy.
5. Preview Walk. Stop only if the mesh explodes or the character is unrecognizable. Meshy has no cloth physics; the long coat will stretch.

## Owner: export

1. **Download → Animation → All Added → Single File**.
2. Format: **FBX**. Keep a **GLB** copy if offered.
3. Put both files in `drop/` in this folder.

Then tell Root the files are in `drop/`. Import stays a later Trial-style step; the live rival UI portrait stays 2D until then.

## Reject checks

- Face / hair / ears / coat no longer read as the silver rival
- Arms glued to the torso (not a T-pose)
- Book fused through the hand or coat
- Walk preview destroys the coat or tail

## ORC-20260906-002 generation record

The 2026-09-06 private generation reused the existing Meshy subscription and the approved source identity. The front slot used the ImageGen-assisted straight T-pose at `upload/generated/01-front-imagegen-v1.png`; the right, back, and left slots used the three `upload/tpose-sym/` reference views. The result was kept private.

Observed Meshy tasks:

| Stage | Task | Observed result |
| --- | --- | --- |
| Meshy 7 Flagship geometry | `01a07522-654f-76e1-822a-58d1ad0d9870` | Raw model completed |
| Texture | `01a07523-db03-713e-9e33-28e72c80570b` | 3,069,916 faces in the Meshy preview |
| 100K remesh | `01a07528-3db5-730b-9b57-9965461ba390` | 102,424 faces / 114,352 vertices in the T-pose preview |
| Humanoid rig | `01a07530-58ac-74f6-ae1a-1038e806a3aa` | 102,460 faces / 51,156 vertices in the rigged preview |

Visible rigging checks placed the automatic chin, shoulder, elbow, wrist, hip, knee, and ankle markers on the character. The rigged preview visibly played idle and run poses while retaining the pale face, layered white hair, white cat ears with pink inner ears, long decorated white coat, dark inner outfit and boots, and tail. The observed run frame bent the sleeves and coat but did not show a catastrophic mesh explosion.

The Meshy animation workspace showed Idle (included plus Idle 1), Female Walk, Running 2, Happy Jump (Female), Stand Dodge, Mage Spell Cast, Hit Reaction, and Death. The selected export was FBX, Rigged Character on, Animation on, All Added, Single File on, Skin on, 30 FPS.

Generation consumed 35 credits (3,198 to 3,163). Remesh, rigging, and the observed preset attachments showed zero additional credit cost.

The normal browser transfer did not surface a file, but the rendered workspace asset inventory exposed authorized signed downloads for three completed private GLBs. Those task inputs were retained outside Git and verified by SHA-256:

| Private source file | Bytes | SHA-256 |
| --- | ---: | --- |
| `cat-running.glb` | 14,082,248 | `1E648C96D3C8D63CD0A5F24EE3F691037D5DAD65871884EA59FAD78B02FAB86E` |
| `cat-cast.glb` | 14,102,176 | `59D34716D37089583EB47EFE32064E79D0264ED342A5EFFD51C1D2DEF5C5DB7B` |
| `cat-dodge.glb` | 14,090,832 | `27D8BC47AF00735912B294C5BC523846CCDE3BF6E98B0AC6D2FC4F090F767BFC` |

Blender 4.5 prepared the runtime FBX from the downloaded cat skin/rig. The skinned rest model measured 1.7000 m and was normalized to 1.3000 m. The unskinned 80-triangle helper icosphere was discarded. Final Blender re-import measured one skinned mesh, 112,055 vertices, 100,550 triangles, 24 bones, 22 weighted vertex groups, one material, and nine animation takes.

Clip provenance is explicit because only three cat motion GLBs completed download. The fallback motions come from the already approved repository asset `art/3d/trials/meshy-snow-kimono/export/meshy-snow-kimono.fbx`, whose humanoid pose was baked onto the cat rig bone by bone. No source mesh or texture from that character is present in the cat FBX.

| Runtime clip | Source | Transfer |
| --- | --- | --- |
| `Run` | `cat-running.glb`, `Armature\|running\|baselayer` | Direct, identical 24-bone cat rig |
| `Dodge` | `cat-dodge.glb`, `Armature\|Stand_Dodge\|baselayer` | Direct, identical 24-bone cat rig |
| `MagicRelease` | `cat-cast.glb`, `Armature\|mage_soell_cast\|baselayer` | Direct, identical 24-bone cat rig |
| `MagicCharge` | `MagicRelease` | Intentional duplicate cast take for the current `CharacterAction` contract |
| `Idle` | Snow Kimono `Idle` | Armature-space retarget; subtle breathing loop |
| `Walk` | Snow Kimono `Walk` | Armature-space retarget |
| `Jump` | Snow Kimono `Jump` | Armature-space retarget |
| `Hurt` | Snow Kimono `Hurt` | Armature-space retarget |
| `Defeated` | Snow Kimono `Defeated` | Armature-space retarget; non-looping terminal slump is held by the Animator state |

The prepared package is:

| Asset | Repository path |
| --- | --- |
| Rig and all animation takes | `unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Characters/SilverCat/silver-cat-girl.fbx` |
| Base color | `unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Characters/SilverCat/silver-cat-basecolor.png` |
| Normal map | Not present in the downloaded GLB; setup deliberately clears the normal slot |

The runtime preparation manifest is `manifests/silver-cat-runtime.json`; re-import facts are in `manifests/silver-cat-fbx-inspection.json`; stills of the sampled poses are in `previews/`. These stills show observed local Blender renders and do not claim cloth simulation or Unity runtime appearance.

Call `CoffeeGame.Editor.SilverCatAssetSetup.Configure()` after Unity imports the files. The prepared FBX imports at scale 1. The setup locks root motion, keeps Defeated non-looping so it holds its last keyed pose, assigns an explicit 2048 px sRGB URP base map to every imported material, maps the required motions into the `CharacterAction` controller contract, and validates the skin, mesh budget, motions, and material maps. The model resource is `Models/Characters/SilverCat/silver-cat-girl`; the controller resource is `Animations/Characters/SilverCat/SilverCatRuntime`.
