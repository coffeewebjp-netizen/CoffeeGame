# Split-ink dragon girl v11

Private Meshy/Blender trial for the second rival. This candidate does not replace the current live rival portrait or any playable character until its face, rig, and Unity import are validated.

## Identity locks

- Spiral irises in both eyes.
- On the character: left hair and clothing are white; right hair and clothing are black. In the front image this reads viewer-left white / viewer-right black. The colors naturally swap screen sides in the back view.
- A balanced Japanese fantasy outfit with the same construction on both sides. Color and dragon embroidery carry the asymmetry.
- Short bob, pale face, dark crimson obi cord, paired dragon embroidery, divided hakama, and matching split boots.
- No cat ears, horns, tail, wings, fan, sword, sheath, or hand-held prop in the rig source.
- Target stature is close to the earlier CoffeeGAME heroine model, about 6.5 to 7 heads rather than an 8-head fashion figure.

## Source and generated references

| Meshy slot | File | Purpose |
| --- | --- | --- |
| Owner design | `upload/00-owner-reference.png` | Face, spiral eyes, split hair, monochrome kimono language |
| Main / Front | `upload/01-front.png` | Full-body A-pose and primary color boundary |
| Right | `upload/02-right.png` | Character's right side; black side visible |
| Back | `upload/03-back.png` | Back construction and continuous center boundary |
| Left | `upload/04-left.png` | Character's left side; white side visible |

The four model sheets were generated with the built-in ImageGen workflow from the Owner-supplied design. Prompts held the same adult character, practical game proportions, empty hands, clean neutral backdrop, and orthographic front/right/back/left views. The side and back prompts explicitly preserved the anatomical color split and mirrored dragon embroidery so Meshy would not average the outfit to gray.

## Meshy generation

- Date: 2026-09-11 JST
- Account mode: private
- Model: Meshy 7 Flagship, High Detail
- Input: four-view image-to-3D
- Resolution: Ultra 2K
- Texture: on
- Pose: A-pose
- Image enhancement: off to preserve the strict black/white boundary
- Geometry: 3,049,860 triangles / 1,633,889 vertices in the textured high-detail preview
- Generation and texture: 35 credits total (3,163 to 3,128)
- Visual result: accepted as a preparation candidate. The short split bob, Japanese outfit, paired dragon motif, center color boundary, and compact proportions remain readable.

### Saved Meshy task chain

| Stage | Task ID | Result |
| --- | --- | --- |
| Multi-view model | `01a08c80-6652-73a8-8643-7de0b5e982c3` | Textured high-detail source |
| Texture | `01a08c81-92f3-71ca-ad3c-2e1c23418d68` | Ultra 2K black/white material pass |
| Quad remesh | `01a08c88-7912-7059-a645-ab0d86e1c243` | 80,793 faces / 86,368 vertices |
| Humanoid rig | `01a08c8c-fc88-775c-9662-96274d9d93eb` | 80,793 faces / 86,577 vertices; 1.7 m; MeshyRig |

The automatic humanoid markers were checked at the jaw, shoulders, elbows, wrists, pelvis, knees, and ankles before submission. The preset `待機` clip was attached to expose deformation problems. The skeleton drives the body, but the long sleeves and divided skirt need manual Blender weight cleanup before gameplay use. The current private rig preview is preserved in `previews/meshy-rig-preview.webp`.

The Meshy workspace successfully prepares a rigged GLB download, but this Codex in-app browser did not surface the resulting file-transfer event after both the viewer and asset-gallery export routes were tried. Its network policy also blocks `http://127.0.0.1:5324`, so the official DCC Bridge cannot be called from the embedded tab (`ERR_BLOCKED_BY_CLIENT`). The rig remains stored under the task ID above. No live CoffeeGAME asset was overwritten while the source transfer is pending.

## Blender bridge preparation

- Official source package: Meshy for Blender v0.6.1, SHA-256 `74E6B77BCB9359D127CF5DCD5D7FDF216FAFA280C5CACE5090ADBE6C6C34E0EC`.
- Installed and enabled for Blender 4.5.10 LTS.
- Local hardening: bind the bridge to `127.0.0.1` instead of all network interfaces, reject non-Meshy origins and non-HTTPS model URLs, add download timeouts, and answer Chromium Private Network Access preflight.
- `tools/start_meshy_bridge.py` clears a temporary scene, starts the bridge, and saves the imported result as packed BLEND, GLB, FBX, and an inspection report under `exports/`.
- The bridge is ready; the remaining transfer must be initiated from a regular browser that permits the Meshy page to reach localhost.

## Game preparation target

1. Keep the high-detail private Meshy result as the source of truth. (done)
2. Remesh to about 100K faces with quad topology for deformation. (done: 80,793 quads)
3. Auto-rig as humanoid after checking the jaw, shoulders, elbows, wrists, pelvis, knees, and ankles. (done)
4. Transfer the rigged GLB/FBX from Meshy, then inspect it in Blender 4.5.
5. Correct eye materials, face normals, center color seam, sleeve weights, and skirt/leg intersections in Blender.
6. Triangulate only for the final Unity FBX and target 80K to 120K triangles with one 2K base-color material where practical.
7. Add a new SplitInk/DragonGirl visual path. Do not reuse or overwrite the silver cat or sword heroine resources.

The current rival sequence remains silver cat first, then split-ink dragon girl. Affinity, companion, combat, and save contracts are outside this art trial.
