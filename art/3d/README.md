# CoffeeGAME 3D assets

- `reference/`: approved 2D modeling references and turnarounds
- `source/`: editable Blender originals
- `previews/`: rendered visual checks
- `manifests/`: machine-readable model, rig, action, and export facts

The generators in `../../tools/blender/` are the reproducible source for the first
blockout models. Unity consumes FBX copies under
`../../unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/`.

The active combat-slice assets are `heroine-v4` and `slime-v2`. Their manifests
include the Unity-facing sRGB palette and clean-FBX outside-normal validation.
Preserve scale, origin, forward axis, material roles, rig/action names, and export
contract when replacing the geometry with later hand-authored art.
