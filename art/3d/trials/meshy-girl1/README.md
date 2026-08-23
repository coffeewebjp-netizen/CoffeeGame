# Meshy girl1 trial (ORC-20260823-004)

Paid Meshy 1-month look-and-walk test. This does **not** replace the playable HD-2D heroine.

CoffeeGAME already has a Trial slot (`CoffeeGAME > Trial > Use anime-girl 3D`). After Meshy export, drop the FBX here and the import stays in that trial path.

## What to upload first

Use the T-pose pack. Neutral pose rigs much better than idle.

| Meshy slot | File |
| --- | --- |
| Main / Front | `upload/tpose/01-front.jpg` |
| Right | `upload/tpose/02-right.jpg` |
| Back | `upload/tpose/03-back.jpg` |
| Left | leave empty; let Meshy fill it |

Backup if the T-pose result looks wrong: `upload/identity/` (idle front / right / back). Do not mix T-pose and idle in one generation.

Known input issue: front and right T-pose hold the sword; the back T-pose has empty hands and a sheathed sword. Expect the first mesh to fuse the katana into the right hand. That is acceptable for this trial.

## Owner: subscribe

1. Open https://www.meshy.ai/pricing
2. Choose **Pro, billed monthly**. Do not pick yearly.
3. New Pro is currently 50% off the first month (~$10). Confirm the live price on that page.
4. Free plan cannot download latest models and has no Multi-view. Paid Pro is required.

## Owner: generate (about 20 credits)

1. Open the Meshy workspace → **Image to 3D**.
2. Model Type: **Standard**. Do not use Smart Topology (Multi-view disappears).
3. AI Model: latest shown (**Meshy 6** or **Meshy 7**).
4. Upload `01-front.jpg` as the main image.
5. Turn **Multi-view** on. Put `02-right.jpg` in Right and `03-back.jpg` in Back.
6. Before Generate, set Pose to **T-Pose** (Pro Pose Control, 0 credits).
7. Leave Auto Split off (saves 10 credits).
8. Click **Generate**. Failed technical errors refund credits.

Judge the still in the Meshy viewer: face, hair, haori, skirt, geta, silhouette. If it is not recognizably girl1, regenerate once or twice before rigging.

## Owner: remesh, rig, animate (usually 0 extra credits)

1. If the mesh is very dense, run **Remesh** first (quad topology, target about 20k–40k triangles).
2. Open **Animate** / Auto-Rig. Character type: **Humanoid**. Center, face forward, feet on the ground.
3. Click **Auto-Rig**.
4. Add library clips: **Idle**, **Walk**, **Run**. Optionally one Jump and one sword slash if they are easy to find.
5. Preview Walk. Reject the candidate if the skirt or hair collapses into the legs, or if the face/identity is gone.

Meshy has no cloth physics. A short skirt that stretches with the legs is expected; total collapse is a fail.

## Owner: export

1. **Download → Animation → All Added → Single File**.
2. Format: **FBX**. Also keep a **GLB** copy if offered.
3. Put both files in `drop/` in this folder. Do not replace `unity/.../heroine-v4.fbx`.

Then tell Root the files are in `drop/`. Import into the existing Trial slot is the next step; HD-2D stays the default.

## Reject checks

- Face / hair / outfit no longer read as girl1
- Arms glued to the torso (not a T-pose)
- Sword is a blob fused through the hip or skirt
- Walk preview destroys the skirt or hair
- Polycount cannot be remeshed under ~40k without losing the silhouette
