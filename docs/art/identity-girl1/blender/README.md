# girl1 Blender prototype

Quality trial only. The playable HD-2D heroine is unchanged.

## Open

1. Blender 4.5
2. **Next compare:** `girl1-compare.blend` — 120点ルック絵と、既存の1.6m 3Dヒロイン（Walk再生可）
3. Look flipbook: `girl1-look-preview.blend`
3. Sculpt clay (not for judging): `girl1-sculpt.blend`
4. Older blockout: `girl1-prototype.blend`
3. Timeline 1–24, play. Action name: `WalkInPlace`
4. Empty images `Ref.Front` / `Ref.Right` / `Ref.Back` are the T-pose drawings

## What this is

Judge **look** from `look-previews/` first (3D-style stills of the same girl).

The `.blend` is only a colored mannequin for scale/rig, not the quality sample.

It is **not** a finished game mesh. Judge whether this Blender path is worth sculpting/retopo next, or whether HD-2D should stay.

## Files

| File | Role |
| --- | --- |
| `refs/tpose-front.jpg` | Front T-pose |
| `refs/tpose-right.jpg` | Right T-pose |
| `refs/tpose-back.jpg` | Back T-pose |
| `girl1-prototype.blend` | Editable prototype |
| `girl1-look-preview.blend` | Judgment scene: approved 3D-look cards, play 1–36 to orbit |
| `look-previews/look-34.jpg` | Quality target (three-quarter) |
| `girl1-sculpt.blend` | Sculpt clay only — do not judge quality from this |
| `girl1-sculpt.glb` | Sculpt mesh export |
| `renders/sculpt-preview.jpg` | Sculpt start preview |
| `build_girl1_sculpt.py` | Rebuild sculpt file |
| `girl1-prototype.glb` | Older blockout export |
| `renders/preview-front.jpg` | Frame 1 mannequin render |
| `look-previews/look-34.jpg` | 3D-style three-quarter still (judge this) |
| `look-previews/look-right.jpg` | 3D-style profile still |
| `look-previews/look-bust.jpg` | 3D-style face still |
| `look-previews/move/walk3d_right.gif` | Approved-look side walk loop |
| `look-previews/move/walk3d_34.gif` | Approved-look three-quarter walk loop |
| `build_girl1_prototype.py` | Rebuild script |
