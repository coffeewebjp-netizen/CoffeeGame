# Upgraded Azure Maiden trial status

Task `ORC-20260905-001`, work package `WP13`, route `ROUTE14`, inputs
`IN08,IN09,IN10,IN11`, expected output `OUT16`.

The intended active design is the saved heroine with a light-blue bob, amber
eyes, red haori, white scoop-neck camisole, short peach skirt, original visual
proportions, and a drawn katana. The accepted game selection remains Meshy Snow
Kimono (`3`); remembered rollback remains HD-2D (`0`). Azure Maiden (`4`) is an
explicit development trial and is not an ordinary or first-launch default.

## Motion sources inspected

The controlled local backup contains byte-verified Meshy GLB exports for the
matching character, RunFast, RegularJump, Katana Power Slash, Magic Power
Release, Charged Spell Cast, and Charged Slash. The useful selected donors were
RunFast, RegularJump, Katana Power Slash, and Charged Spell Cast. The two custom
Motion Prime exports use their substantive `rigify_clip` action; RunFast and
RegularJump use `Armature|RunFast|baselayer` and
`Regular_Jump|baselayer`, respectively. Raw provider files and download URLs
are intentionally not stored in the repository.

The preparation experiments preserved the sixteen runtime action names,
Blender 4.5 action-slot binding, quaternion sampling, one-body action reuse,
in-place horizontal Hips motion, and motor-owned jump height. FBX reimport and
numeric motion checks passed for a diagnostic candidate, but those checks did
not override the failed visual gate.

## Rejected body routes

- Provider weights: expressive Sword, Run, and Magic poses pull peach/red
  garment surfaces into long sheets attached to limbs.
- Clean-phase crop: hides selected failures but leaves major cloth fragments and
  suppresses useful parts of the motion.
- Position/color masks with neutral compensation: coincident UV/normal split
  vertices receive discontinuous assignments and create new streaks.
- Rebuilt local rigs with continuous coordinate or graph-smoothed weights:
  either rigidify most of the body, tear sleeves/skirt, or detach collapsed
  skin/garment faces.

The source inverse-bind matrices are internally consistent. The blocking issue
is the fused/discontinuous surface and its semantic weight compatibility, not a
proven corrupt glTF bind. No rejected FBX, texture, controller, or rendered
preview is an accepted runtime asset.

## Next gate

Create a clean A-pose body from the approved design reference, with arms clear
of the torso and skirt and with the weapon separate. Preserve the original
proportions and outfit. Retarget the verified donors relative to their neutral
pose, then require neutral, Run, Jump, early Sword, Charged Spell release, and
recovery renders before Unity integration. The runtime evidence capture already
supports dense early-Sword samples, actual Animator progress, buffered PNG
encoding, current baked-surface framing, and Sword-to-Run interruption after the
combat-owned hold expires.

The prepared reference package is kept outside Git at
`C:/work/output/imagegen/original-heroine-apose-v1/`: `front-apose.png`
(SHA-256 `24e2920f731380935576274809560e02d508e6c5e01db78c5329cb7e467dcd44`),
`back-apose.png`
(SHA-256 `ad1a0ff49b3db8111405a87e282f8ec758a2bcdfe34612de9b4a6554156f51ca`),
and `README.md` with prompts and provenance. Both views are full body, empty
handed, and separate the sleeves from the torso and skirt. Fresh Meshy import is
pending restoration of the supported browser surface.
