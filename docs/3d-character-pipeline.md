# 3D character pipeline

## Fixed toolchain

- Blender `4.5.10 LTS`
- Unity `6000.3.21f1`
- Blender source files are the editable originals.
- Unity receives exported FBX files. Do not place `.blend` files under Unity `Assets`.

## Coordinate and scale contract

- One Blender metre equals one Unity unit.
- Y is up and +Z is character forward.
- The actor origin is centered on the ground between the feet, or under the slime.
- Locomotion clips are in-place. `PlayerMotor3D` and enemy controllers own world movement.
- Animation never owns hit detection, rewards, health, or run state.

## Source and output layout

| Asset | Editable source | Unity output |
| --- | --- | --- |
| Heroine | `art/3d/source/heroine-v4.blend` | `unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Hero/heroine-v4.fbx` |
| Slime | `art/3d/source/slime-v2.blend` | `unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Slime/slime-v2.fbx` |

Generators live under `tools/blender/`. Manifests under `art/3d/manifests/` record
model, rig, action, and export facts so generated blockouts can be reproduced.

## Required animation names

Heroine:

- `Idle`, `Walk`, `Run`
- `Jump`, `Fall`, `Land`
- `Sword`, `AirSlash`, `Plunge`
- `SpinCharge`, `SpinRelease`
- `MagicCharge`, `MagicRelease`
- `Hurt`, `Defeated`

Slime:

- `Idle`, `Move`, `Windup`, `Attack`, `Hurt`, `Defeated`

## Runtime boundary

`ICharacterVisual` is the only gameplay-facing visual contract. The 3D implementation
maps locomotion and action requests to Animator states, rotates the model child to face
the requested world direction, and applies temporary tint without changing shared
materials. `PlayerMotor3D`, combat controllers, input, progression, and CoffeeLearning
integration must not depend on a particular mesh, bone, or animation frame.

The existing sprite renderer remains only as a missing-model fallback. New visual work
targets the Blender/FBX path rather than adding more billboard directions.

## Production progression

Heroine v4 and Slime v2 are mobile mid-poly production candidates used to validate
silhouette, materials, controls, and animation timing. Their generators recalculate
outside normals before export, and the clean-FBX validation rejects inward watertight
components so Unity backface culling cannot remove the hair crown or clothing. They
keep the prototype rig/action contracts and consolidate runtime meshes for Android.
They are not final hand-sculpted assets; later UV texture and skin-weight polish can
replace them while preserving the rig, action names, origin, scale, material roles,
and `ICharacterVisual` contract.
