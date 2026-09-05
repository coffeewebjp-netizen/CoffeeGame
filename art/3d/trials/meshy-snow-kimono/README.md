# Meshy Snow Kimono finish

This isolated work area is for the approved colored Meshy Snow Kimono body from
`ORC-20260905-001`. It does not replace the earlier procedural `snow-kimono`
source or runtime asset.

## Source drop gate

Use the approved high-resolution textured Meshy body as the source of truth. The
automatic 60,000-triangle derivative was rejected because it introduced visible
neck and tabi display or surface artifacts whose cause has not been diagnosed;
do not use it for finishing. Optimize a game
derivative only after the high-resolution source passes the visual gate.

Place the approved GLB at `drop/approved-highres.glb`. The whole Meshy export
archive may be placed under `drop/` and extracted there if that is the only
available download. An FBX is acceptable when every
referenced base-color, normal, metallic, roughness, opacity, and emissive texture
is delivered beside it. OBJ is not sufficient because it does not preserve the
rig, animation, or complete PBR material relationships required by this task.

Keep the private Meshy download and the four generated identity references local.
Do not commit them. Before changing geometry, run the inspection from the repository
root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' -b `
  --python tools/blender/inspect_meshy_snow_kimono.py -- `
  art/3d/trials/meshy-snow-kimono/drop/approved-highres.glb `
  --report art/3d/trials/meshy-snow-kimono/work/source-inspection.json `
  --preview-dir art/3d/trials/meshy-snow-kimono/work/source-previews
```

The source gate requires a readable mesh, the approved face/hair/kimono materials,
all texture files, usable topology near the body and sleeves, and front/right/back/left
views that still match the approved direction. The report also records whether the
download already contains an armature, skin weights, or actions; none are assumed.

## Additive outputs

- Editable source: `source/meshy-snow-kimono.blend`
- Game export: `export/meshy-snow-kimono.fbx`
- Runtime model: `Resources/Models/Hero/MeshySnowKimono/meshy-snow-kimono.fbx`
- Runtime controller: `Resources/Animations/Hero/MeshySnowKimonoRuntime.controller`
- Evidence: `previews/` and `manifests/`

The body remains one skinned model for every action. A rear obi bow is added to the
approved body; katana and saya are separate rigid objects parented to bones. Their
visibility may change for combat, but the character body must not be swapped.

The final export must provide these sixteen leaf action names:

`Idle`, `Walk`, `Run`, `Jump`, `Fall`, `Land`, `Sword`, `AirSlash`, `Plunge`,
`SpinCharge`, `SpinRelease`, `MagicCharge`, `MagicRelease`, `Hurt`, `Defeated`,
and `Dodge`.

Blender uses metres, Z up internally, and the finished FBX imports into Unity as
one unit per metre with Y up and +Z character-forward. `Walk` and `Run` remain
in-place because `PlayerMotor3D` owns world movement. Blender 4.5 actions must be
bound through their action slots before baking. The final FBX is reimported into a
clean Blender scene and sampled at start/middle/end for `Walk`, `Run`, `Sword`, and
`Dodge`; action names alone are not acceptance evidence.

Generate the runtime derivative from the approved source with:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' -b `
  --python tools/blender/prepare_meshy_snow_kimono.py
```

The current 240,000-triangle output is a Windows prototype/runtime budget. It
does not establish Android or performance readiness. Automatic bone heat does
not solve this Meshy surface, so the script records and applies its smooth
coordinate fallback. The approved face, blue hair, black kimono and atlas are
retained, with modest sleeve-edge and moving-hem irregularity as known prototype
limits. The katana and saya are rigid; the grip crosses the open-finger hand,
without a separate hand-grip deformation pass.

Unity integration stays additive and reversible. Do not overwrite `snow-kimono.fbx`,
`SnowKimonoRuntime.controller`, the HD-2D assets, or prior trial assets. Before any
Unity setup or Windows build, make a byte backup of every dirty or untracked file
that setup can rewrite, then restore and hash-compare those files after the build.
The normal Windows executable selects this asset with
`tools/launch-meshy-snow-kimono-default.cmd`. The saved choice then applies to
ordinary and Steam launches. `tools/launch-previous-character.cmd` restores the
remembered original HD-2D selection, while `tools/launch-snow-kimono-default.cmd`
keeps the intermediate procedural model available.
