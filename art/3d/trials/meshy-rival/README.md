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
