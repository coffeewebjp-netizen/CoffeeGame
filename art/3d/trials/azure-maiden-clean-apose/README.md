# Original red-haori heroine: clean A-pose source

ORC-20260905-001 / WP13 / ROUTE17, inputs IN08-IN12. IN13 / WP17 / ROUTE19
approves provisional adoption in the ordinary player for Owner testing.
The final derivative uses V3 art plus the V4 FBX socket correction.

Design: light-blue bob, amber eyes, rose-red haori, white scoop-neck camisole,
short peach pleated skirt, black waist ribbon and wooden geta. Preserve the
original reference proportions. Build the katana separately. The generated
matching back is an interpretation, not an Owner-supplied back drawing.

References and raw downloads are local and ignored. The exact image prompts
are in `references/README.md`. Original reference copies remain under
`C:/work/output/imagegen/original-heroine-apose-v1/`.

Private Meshy 7 Ultra 2K source task: `01a071d3-479d-75e8-9265-d0fadd7af10d`.
High-detail source: 3,005,196 triangles. Auto-split grouped the garments, so it
does not establish independent haori/skirt topology. A 30K quad remesh lost the
sleeves/skirt and was rejected. A 100K triangle derivative preserved their
neutral silhouette. Texturing the high-detail model before remeshing produced
broad skin/gray patches on cloth and hair. Vertex seam/weight diagnostics and
flat-material posed renders isolated this to the atlas. Broad recoloring and
local brush repair were rejected. Image-guided 4K retexturing directly on the
final triangle mesh fixes those patches.

The retained rig is task `01a071e8-b1c4-71f1-8d73-f34fb40f7b60`. The cleaner
atlas is from task `01a07210-31f7-73c3-8423-4a2eeb0d9418`. Their primitive
indices and UVs are byte-identical. Only the atlas was copied: the newer rig's
slightly different rest transforms, weights and positions were not adopted.
SHA-256 provenance and numerical checks are in `manifests/blender-v3.json`.

V3 contains a 1.62 m body (97,002 triangles, 24 bones, at most four normalized
weights per vertex), a separate 402-triangle curved/tapered katana, and exactly
16 named actions. Run, Jump, Sword and Magic use fresh matching-rest Meshy
donors. Other required states are derived in Blender. The root does not add
horizontal travel or duplicate the motor's jump trajectory. The right hand
has a static mesh grip; its coarse finger topology is a remaining limitation.

The initial rigid bone-child attachment looked correct in the source Blender
scene but exported roughly 24 m away in both FBX and GLB. V4 keeps the same
separate weapon geometry and six materials, with all 211 vertices weighted
100% to RightHand. Export/reimport grip distance is now about 1.6 cm across
Idle, Run and Sword; body geometry, bones and all take ranges are unchanged.
`manifests/blender-v4-socket.json` records this correction. Unity validation
checks evaluated weapon distance as well as clip bindings and materials.

Sword emits at frame 1, retaining the existing immediate gameplay hit and
0.34 s movement interrupt/cooldown. Its full visual recovery lasts 1.05 s.
MagicRelease likewise starts at emission, with 0.8 s visual recovery. This
keeps the existing combat timing while making the follow-through visible.

Local editable source: `source/azure-maiden-clean-runtime.blend`. The GLB/FBX
exchange copies are under `export/`; posed Blender images are under `previews/`.
These large review/source copies remain local and ignored. Unity's runtime
FBX and 4K PNG are versioned under `Resources/Models/Hero/AzureMaidenUpgraded`.

Reproduce into a **new, empty output directory** with Blender 4.5:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --python tools/blender/prepare_azure_maiden_clean.py -- --source-glb art/3d/trials/azure-maiden-clean-apose/downloads/meshy-clean-rigged-body.glb --texture-source-glb art/3d/trials/azure-maiden-clean-apose/downloads/meshy-direct-texture-rigged-body.glb --motion-donor-dir art/3d/trials/azure-maiden-clean-apose/downloads --out-dir art/3d/trials/azure-maiden-clean-apose/export/new-iteration
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --python tools/blender/repair_azure_maiden_socket.py -- --source-blend art/3d/trials/azure-maiden-clean-apose/export/new-iteration/azure-maiden-clean-runtime.blend --out-dir art/3d/trials/azure-maiden-clean-apose/export/new-iteration-fixed
```

Import the **fixed second-stage export**, retaining the existing Unity FBX
GUID, clip names and internal IDs. The first-stage bone-child export is not
game-ready even when its source render looks correct.

Validation must remain read-only: `AzureMaidenUpgradedValidation.Validate`
does not invoke any setup, rebuild or asset-save method. Retain the existing
controller and the FBX clip internal IDs when updating take references.
The prior exact six-file restoration was completed and byte-verified under
WP15. The subsequent 86-file pre-import snapshot is under the ignored
`.task-local-backup/ORC-20260905-001-WP13-route17-before-clean-import` directory.

The diagnostic Windows build uses `BuildAzureCleanV3DiagnosticNoSetup` and
writes `Builds/Windows-AzureCleanV3`, preserving earlier player folders. Dense
presentation evidence uses `-azureMaidenUpgraded3D -captureMeshyMotion <new-dir>
-captureMeshyMotionVideo`. `tools/build-motion-review.py <new-dir>` encodes
the measured frame times into a review video. This captures the real player
renderer and Animator with motor/combat disabled for repeatability; it is not
an input-driven combat playtest or a performance benchmark.

Final verified local build: 572,917,297 bytes aggregate. Read-only import checks
pass for all sixteen referenced clips and the evaluated weapon attachment.
`manifests/unity-final.json` and `manifests/runtime-final.json` contain the
checks and hashes. The final recording is
`previews/runtime-final-warmed/motion-review.mp4` (172 captured frames across
all sequences, including three additional movement-interrupt checkpoints).
Sword remains in its recovery at 0.8 s and a movement request at 0.36 s enters
Run. Initial offscreen rendering is warmed before timing starts.

On 2026-09-06, IN13 explicitly approved replacing the normal player after the
previous approval-review gate. WP17 installed a verified copy of this build
at `Builds/Windows/CoffeeGAME.exe`, renaming the EXE and its matching Data
directory. Steam's target and launch options are unchanged. The existing
`-useAzureMaidenUpgradedDefault` option saved selection 4; a second launch
without a selector rendered the red-haori model. This is startup evidence,
not a comprehensive input-driven playtest. The Owner noted a jump concern;
no new jump/motion edit or final visual acceptance is implied by adoption.

All 348 prior files (588,225,841 bytes), empty directories and the three
display preferences are hash-verified in the ignored local directory
`.task-local-backup/ORC-20260905-001-WP17-normal-player`. Other top-level logs
and Owner folders were retained in place. The copied runtime tree contains
297 files / 573,125,610 bytes; this filesystem sum is distinct from Unity's
572,917,297-byte BuildReport aggregate above. Every installed file matches
its prepared source. All 77 protected source files remain byte-identical.
`manifests/normal-player-adoption.json` records sanitized deployment evidence;
the earlier `runtime-final.json` remains the historical pre-adoption record.

To restore the pre-update player, close CoffeeGAME and run
`tools/restore-pre-azure-normal-player.cmd`. The helper first verifies the
complete backup and installed hashes, retains the upgrade in a separate
folder, restores only runtime entries and the three backed-up display
preferences, and leaves save progress and unrelated settings alone. Run the
PowerShell helper without `-Restore` for non-mutating verification. This
verification passed; an actual rollback was not performed after adoption.
The backup stays local and is required by this helper.

The ordinary startup capture logged a Unity graphics-ring-buffer warning
but completed with the expected model and no application exception. Capture
evidence does not establish in-play performance. The independent
`tools/launch-azure-maiden-clean.cmd` trial remains available and does not
persist selection changes.

### IN14 correction: verify the actual Steam client launch

WP17's direct-EXE checks were insufficient: the Owner's real Steam run still
entered the first-launch branch and saved MeshySnowKimono3D, while the
automation host's registry view retained selection 4. The installed DLL and
all payload hashes were already correct. The observed selection state
differed between launch environments; the precise OS-level isolation
mechanism has not been established and is not asserted as a proven cause.

WP18 backed up the current shortcut, failed-run log and profile. Through the
Steam properties UI, the existing persistent selection flag and a one-time
scene capture were added, and Steam launched the game itself. The game
saved/rendered AzureMaidenUpgraded3D. Both flags were then removed, restoring
empty launch options. A fresh click on Steam's Play button started the same
normal EXE with no arguments. Its actual Player.log selected the upgraded
model without saving a new override; Root also inspected the native game
window after the input selection and Start buttons, seeing the red haori,
peach skirt and katana in the running combat scene. The game was paused
immediately for Owner testing. This verifies delivery, not jump quality.

All 297 installed files and 348 old backup files still pass the rollback
helper's non-mutating check. All 77 protected source files and the current
profile hash are unchanged. No runtime source or binary was changed in WP18.
`manifests/steam-selection-repair.json` records sanitized evidence; local
logs and the shortcut snapshot are under
`.task-local-backup/ORC-20260905-001-WP18-steam-selection`.

Future adoption checks must use the actual Steam client to set and read the
selection, then remove temporary flags and restart from Play. A direct EXE
capture and a registry read in the automation host are not substitutes. The
existing rollback helper remains valid when launched by the Owner normally;
it restores runtime files and display preferences without reverting progress.
