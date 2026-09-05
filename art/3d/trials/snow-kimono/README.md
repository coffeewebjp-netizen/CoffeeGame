# Snow Kimono 3D trial

This additive trial is a simplified, reference-guided reconstruction of an owner-local image. The original image is not included. Front identity cues are blue chin-length bob hair, restrained amber eyes, a long closed black wrap kimono with narrow red piping, dark obi and bow, white tabi, sandals, a rigid drawn katana, and a separate rigid saya. Side and back details are conservative first-pass interpretations.

The deliverable is editable and reproducible:

- `source/snow-kimono.blend` is the Blender 4.5 source.
- `export/snow-kimono.fbx` is the archived FBX; the same generated model is exported into Unity Resources.
- `manifests/snow-kimono.json` records scale, axes, mesh, rig, action, material, and preview facts.
- `previews/` contains neutral front/side/back, beauty, Walk, Run, Sword, Dodge, a Unity unlit palette-slot diagnostic, and an actual runtime scene capture.
- `../../../../tools/blender/generate_snow_kimono.py` regenerates the model, source, FBXs, manifest, and Blender previews.

Run Blender from the repository root:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' -b --python tools\blender\generate_snow_kimono.py
```

The normal Windows development player at `unity/CoffeeGame/Builds/Windows/CoffeeGAME.exe` now uses SnowKimono after the temporary replacement update. Run `tools/launch-previous-character.cmd` from the repository root to restore the remembered character for this and subsequent launches, or `tools/launch-snow-kimono-default.cmd` to select SnowKimono again. Both original character assets and the separate trial build remain available.

In Unity, use `CoffeeGAME > Trial > Use snow-kimono 3D` or `Use HD-2D heroine` to choose a character. The original isolated trial can still be launched with:

```powershell
.\tools\launch-snow-kimono-trial.cmd
```

The wrapper starts `unity/CoffeeGame/Builds/Windows-SnowKimono/CoffeeGAME-SnowKimono.exe` with `-snowKimono3D` for that launch. The separate Windows build is a local artifact and is not committed. Unity import, motion sampling, Windows build, and actual player scene capture passed. The older anime-girl trial keeps its own selector.

Known prototype limits: the face and hands are simplified, the sleeves and cloth deformation remain chunky, hair locks are procedural rather than hand groomed, there is no UV-painted fabric texture, and the side/back reconstruction has not been approved as final character art. The rigid katana follows `Hand.R`; the rigid saya follows `Pelvis`. Locomotion clips are in-place and gameplay still owns movement and hit detection.
