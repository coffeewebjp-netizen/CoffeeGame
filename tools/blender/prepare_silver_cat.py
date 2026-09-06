"""Merge Meshy motion GLBs into one normalized, textured Unity FBX.

Usage:
  blender --background --python prepare_silver_cat.py -- \
    output.fbx report.json texture.png preview-dir input1.glb [input2.glb ...]
"""

import json
import hashlib
import math
import os
import re
import sys

import bpy
from mathutils import Vector


TARGET_HEIGHT_METRES = 1.30
REQUIRED = ("Idle", "Walk", "Run", "Jump", "Dodge", "MagicRelease", "Hurt", "Defeated")


def arguments():
    values = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    if len(values) < 5:
        raise SystemExit("Expected output.fbx report.json texture.png preview-dir input.glb [...]")
    return [os.path.abspath(value) for value in values]


def normalize(value):
    return re.sub(r"[^a-z0-9]+", "", (value or "").lower())


def portable_path(value):
    try:
        return os.path.relpath(value, os.getcwd()).replace("\\", "/")
    except ValueError:
        return os.path.basename(value)


def sha256_file(value):
    digest = hashlib.sha256()
    with open(value, "rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest().upper()


def source_record(value):
    relative = portable_path(value)
    # Downloaded private Meshy motion GLBs are task inputs, not repository assets.
    # Record their stable filename/hash without implying that drop/raw is shipped.
    recorded_path = os.path.basename(value) if "/drop/raw/" in relative else relative
    return {
        "file": recorded_path,
        "bytes": os.path.getsize(value),
        "sha256": sha256_file(value),
    }


def canonical_motion(value):
    value = normalize(value)
    aliases = (
        ("Defeated", ("death", "dead", "defeated", "die")),
        ("MagicRelease", ("magespellcast", "spellcast", "magic", "cast")),
        ("Dodge", ("standdodge", "dodge", "evade")),
        ("Hurt", ("hitreaction", "hurt", "damage", "hit")),
        ("Jump", ("happyjump", "jump", "leap")),
        ("Walk", ("femalewalk", "walking", "walk")),
        ("Run", ("running2", "running", "sprint", "run")),
        ("Idle", ("waiting1", "waiting", "idle", "stand")),
    )
    for canonical, tokens in aliases:
        if any(token in value for token in tokens):
            return canonical
    return None


def motion_name(path, action_name):
    canonical = canonical_motion(action_name)
    if canonical:
        return canonical
    canonical = canonical_motion(os.path.basename(path))
    if canonical:
        return canonical
    raise RuntimeError(f"Cannot map motion from {path}: {action_name}")


def import_motion(path):
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    if path.lower().endswith(".fbx"):
        bpy.ops.import_scene.fbx(filepath=path, use_anim=True)
    else:
        bpy.ops.import_scene.gltf(filepath=path, import_pack_images=True)
    return (
        [obj for obj in bpy.data.objects if obj not in before_objects],
        [action for action in bpy.data.actions if action not in before_actions],
    )


def mesh_bounds(mesh_objects, evaluated=False):
    points = []
    depsgraph = bpy.context.evaluated_depsgraph_get() if evaluated else None
    for obj in mesh_objects:
        source = obj.evaluated_get(depsgraph) if evaluated else obj
        points.extend(source.matrix_world @ Vector(corner) for corner in source.bound_box)
    mins = [min(point[i] for point in points) for i in range(3)]
    maxs = [max(point[i] for point in points) for i in range(3)]
    return mins, maxs, [maxs[i] - mins[i] for i in range(3)]


def set_action(armature, action):
    armature.animation_data_create()
    armature.animation_data.action = action
    if action is not None and len(action.slots) > 0:
        armature.animation_data.action_slot = action.slots[0]


def action_sample_frames(action):
    first, last = action.frame_range
    return sorted({
        int(round(first)),
        int(round(first + (last - first) * 0.25)),
        int(round(first + (last - first) * 0.50)),
        int(round(first + (last - first) * 0.75)),
        int(round(last)),
    })


def measure_motion(armature, actions):
    """Record enough sampled pose change to catch empty or rigid animation takes."""
    records = {}
    for name, action in sorted(actions.items()):
        set_action(armature, action)
        samples = []
        for frame in action_sample_frames(action):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            samples.append({
                bone.name: (
                    armature.matrix_world @ bone.head,
                    (armature.matrix_world @ bone.matrix).to_quaternion(),
                )
                for bone in armature.pose.bones
            })

        max_displacement = 0.0
        max_rotation = 0.0
        moved_bones = set()
        baseline = samples[0]
        for sample in samples[1:]:
            for bone_name, (base_head, base_rotation) in baseline.items():
                head, rotation = sample[bone_name]
                displacement = (head - base_head).length
                rotation_angle = base_rotation.rotation_difference(rotation).angle
                rotation_degrees = math.degrees(min(rotation_angle, math.tau - rotation_angle))
                max_displacement = max(max_displacement, displacement)
                max_rotation = max(max_rotation, rotation_degrees)
                if displacement > 0.002 or rotation_degrees > 0.5:
                    moved_bones.add(bone_name)

        records[name] = {
            "frame_range": list(action.frame_range),
            "sample_frames": action_sample_frames(action),
            "moved_bones": len(moved_bones),
            "max_head_displacement_metres": max_displacement,
            "max_rotation_degrees": max_rotation,
        }
    return records


def retarget_action(source_armature, source_action, target_armature, canonical):
    """Bake a source pose into the cat rig using armature-space rest deltas."""
    source_to_target = {
        "Pelvis": "Hips",
        "Spine": "Spine",
        "Chest": "Spine01",
        "Neck": "neck",
        "Head": "Head",
        "UpperArm.L": "LeftArm",
        "Forearm.L": "LeftForeArm",
        "Hand.L": "LeftHand",
        "UpperArm.R": "RightArm",
        "Forearm.R": "RightForeArm",
        "Hand.R": "RightHand",
        "Thigh.L": "LeftUpLeg",
        "Shin.L": "LeftLeg",
        "Foot.L": "LeftFoot",
        "Thigh.R": "RightUpLeg",
        "Shin.R": "RightLeg",
        "Foot.R": "RightFoot",
    }
    absent = [
        f"{source}->{target}" for source, target in source_to_target.items()
        if source not in source_armature.data.bones or target not in target_armature.data.bones
    ]
    if absent:
        raise RuntimeError("Retarget bone mapping is incomplete: " + ", ".join(absent))

    target_action = bpy.data.actions.new(canonical)
    target_action.use_fake_user = True
    set_action(source_armature, source_action)
    set_action(target_armature, target_action)
    for pose_bone in target_armature.pose.bones:
        pose_bone.rotation_mode = "QUATERNION"
        pose_bone.matrix_basis.identity()

    first = int(math.floor(source_action.frame_range[0]))
    last = int(math.ceil(source_action.frame_range[1]))
    for frame in range(first, last + 1):
        set_action(source_armature, source_action)
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        target_poses = {}
        for source_name, target_name in source_to_target.items():
            source_pose_bone = source_armature.pose.bones[source_name]
            source_rest_bone = source_armature.data.bones[source_name]
            target_poses[target_name] = {
                "rotation": source_pose_bone.matrix.to_quaternion(),
                "translation_delta": source_pose_bone.head - source_rest_bone.head_local,
            }

        set_action(target_armature, target_action)
        bpy.context.scene.frame_set(frame)
        for target_name in target_poses:
            target_armature.pose.bones[target_name].matrix_basis.identity()
        bpy.context.view_layer.update()
        for target_name, source_pose in target_poses.items():
            pose_bone = target_armature.pose.bones[target_name]
            matrix = source_pose["rotation"].to_matrix().to_4x4()
            if target_name == "Hips":
                matrix.translation = target_armature.data.bones[target_name].head_local + source_pose["translation_delta"]
            else:
                matrix.translation = pose_bone.head
            pose_bone.matrix = matrix
            bpy.context.view_layer.update()
        for target_name in target_poses:
            pose_bone = target_armature.pose.bones[target_name]
            pose_bone.keyframe_insert("location", frame=frame, group=target_name)
            pose_bone.keyframe_insert("rotation_quaternion", frame=frame, group=target_name)
            pose_bone.keyframe_insert("scale", frame=frame, group=target_name)

    set_action(target_armature, target_action)
    return target_action


def extract_base_map(texture_path):
    images = [image for image in bpy.data.images if image.name != "Render Result" and image.size[0] > 0]
    if not images:
        raise RuntimeError("The base GLB contains no packed texture image")
    base = next(
        (image for image in images if image.name.lower().startswith("texture_0")),
        next((image for image in images if "normal" not in image.name.lower()), images[0]),
    )
    os.makedirs(os.path.dirname(texture_path), exist_ok=True)
    base.filepath_raw = texture_path
    base.file_format = "PNG"
    base.save()
    return {"source_name": base.name, "size": list(base.size), "colorspace": base.colorspace_settings.name}


def look_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_previews(mesh_objects, armature, actions, preview_dir):
    os.makedirs(preview_dir, exist_ok=True)
    bpy.context.scene.render.engine = "BLENDER_EEVEE_NEXT"
    bpy.context.scene.render.resolution_x = 512
    bpy.context.scene.render.resolution_y = 512
    bpy.context.scene.render.resolution_percentage = 100
    bpy.context.scene.render.image_settings.file_format = "PNG"
    bpy.context.scene.render.film_transparent = False
    if bpy.context.scene.world is None:
        bpy.context.scene.world = bpy.data.worlds.new("Silver Cat Preview World")
    bpy.context.scene.world.color = (0.018, 0.025, 0.045)

    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.lens = 52
    bpy.context.scene.camera = camera
    bpy.ops.object.light_add(type="AREA", location=(2.5, -2.5, 2.7))
    bpy.context.object.data.energy = 1100
    bpy.context.object.data.shape = "DISK"
    bpy.context.object.data.size = 3.0
    look_at(bpy.context.object, (0, 0, 0.85))
    bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.0, 1.35))
    bpy.context.object.data.energy = 750
    bpy.context.object.data.size = 2.0
    look_at(bpy.context.object, (0, 0, 0.85))

    outputs = []
    for key in REQUIRED:
        for pose_bone in armature.pose.bones:
            pose_bone.matrix_basis.identity()
        set_action(armature, actions[key])
        # A death/KO state is non-looping in Unity, so its review image must show
        # the terminal pose that the Animator will hold after playback.
        frame = (
            int(round(actions[key].frame_range[1]))
            if key == "Defeated"
            else int(sum(actions[key].frame_range) * 0.5)
        )
        bpy.context.scene.frame_set(frame)
        mins, maxs, dims = mesh_bounds(mesh_objects, evaluated=True)
        center = Vector(((mins[0] + maxs[0]) * 0.5, (mins[1] + maxs[1]) * 0.5, (mins[2] + maxs[2]) * 0.5))
        distance = max(dims) * 1.55
        camera.location = center + Vector((0, -distance, max(dims) * 0.06))
        look_at(camera, center)
        path = os.path.join(preview_dir, f"silver-cat-{key.lower()}-front.png")
        bpy.context.scene.render.filepath = path
        bpy.ops.render.render(write_still=True)
        outputs.append(portable_path(path))
    for pose_bone in armature.pose.bones:
        pose_bone.matrix_basis.identity()
    set_action(armature, actions["Idle"])
    bpy.context.scene.frame_set(int(sum(actions["Idle"].frame_range) * 0.5))
    mins, maxs, dims = mesh_bounds(mesh_objects, evaluated=True)
    center = Vector(((mins[0] + maxs[0]) * 0.5, (mins[1] + maxs[1]) * 0.5, (mins[2] + maxs[2]) * 0.5))
    camera.location = center + Vector((0, max(dims) * 1.55, max(dims) * 0.06))
    look_at(camera, center)
    path = os.path.join(preview_dir, "silver-cat-idle-back.png")
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    outputs.append(portable_path(path))
    return outputs


output_fbx, report_path, texture_path, preview_dir, *inputs = arguments()
for path in inputs:
    if not os.path.isfile(path):
        raise RuntimeError("Missing motion GLB: " + path)

bpy.ops.wm.read_factory_settings(use_empty=True)
base_objects = []
base_armature = None
motions = {}
source_records = []

for index, path in enumerate(inputs):
    objects, new_actions = import_motion(path)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    skinned_meshes = [
        obj for obj in objects
        if obj.type == "MESH" and any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
    ]
    if len(armatures) != 1 or not skinned_meshes or not new_actions:
        raise RuntimeError(
            f"Expected one armature, at least one skinned mesh, and animation in {path}; "
            f"got {len(armatures)}, {len(skinned_meshes)}, {len(new_actions)} actions"
        )

    if index == 0:
        base_objects = objects
        base_armature = armatures[0]
        if len(new_actions) != 1:
            raise RuntimeError("The first input must be one Meshy cat motion GLB")

    source_armature = armatures[0]
    compatible_rig = set(source_armature.data.bones.keys()) == set(base_armature.data.bones.keys())
    for action in new_actions:
        original_action_name = action.name
        canonical = canonical_motion(original_action_name)
        if canonical is None and len(new_actions) == 1:
            canonical = motion_name(path, original_action_name)
        if canonical is None or canonical in motions:
            continue

        if compatible_rig and canonical == "Idle" and action.frame_range[1] - action.frame_range[0] < 2:
            continue
        if compatible_rig:
            prepared_action = action
            transfer_mode = "direct-compatible-rig"
        elif canonical in {"Idle", "Walk", "Jump", "Hurt", "Defeated"}:
            prepared_action = retarget_action(source_armature, action, base_armature, canonical)
            transfer_mode = "armature-space-retarget"
        else:
            continue

        prepared_action.name = canonical
        prepared_action.use_fake_user = True
        motions[canonical] = prepared_action
        source_records.append(
            {
                **source_record(path),
                "motion": canonical,
                "source_action": original_action_name,
                "frame_range": list(prepared_action.frame_range),
                "transfer": transfer_mode,
            }
        )

    if index != 0:
        retained = set(motions.values())
        source_armature.animation_data_clear()
        for obj in objects:
            bpy.data.objects.remove(obj, do_unlink=True)
        for action in new_actions:
            if action not in retained:
                bpy.data.actions.remove(action, do_unlink=True)

missing = [name for name in REQUIRED if name not in motions]
if missing:
    raise RuntimeError("Required motions are missing: " + ", ".join(missing))

magic_charge = motions["MagicRelease"].copy()
magic_charge.name = "MagicCharge"
magic_charge.use_fake_user = True
motions["MagicCharge"] = magic_charge
bpy.context.scene.render.fps = 24

for obj in list(base_objects):
    if obj.type == "MESH" and not any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
        bpy.data.objects.remove(obj, do_unlink=True)

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if not mesh_objects or base_armature is None:
    raise RuntimeError("Base skin is missing after cleanup")

base_armature.data.pose_position = "REST"
bpy.context.scene.frame_set(0)
rest_min, rest_max, rest_dimensions = mesh_bounds(mesh_objects, evaluated=True)
raw_height = rest_dimensions[2]
if raw_height <= 0.1:
    raise RuntimeError("Unexpected rest-pose height: " + str(raw_height))
export_scale = TARGET_HEIGHT_METRES / raw_height
base_armature.data.pose_position = "POSE"

texture_record = extract_base_map(texture_path)
motion_validation = measure_motion(base_armature, motions)
preview_paths = render_previews(mesh_objects, base_armature, motions, preview_dir)

set_action(base_armature, motions["Idle"])
for obj in bpy.context.scene.objects:
    obj.select_set(obj == base_armature or obj in mesh_objects)
bpy.context.view_layer.objects.active = base_armature

os.makedirs(os.path.dirname(output_fbx), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=output_fbx,
    use_selection=True,
    global_scale=export_scale,
    apply_unit_scale=True,
    apply_scale_options="FBX_SCALE_ALL",
    axis_forward="-Z",
    axis_up="Y",
    object_types={"ARMATURE", "MESH"},
    use_mesh_modifiers=True,
    mesh_smooth_type="FACE",
    add_leaf_bones=False,
    primary_bone_axis="Y",
    secondary_bone_axis="X",
    use_armature_deform_only=True,
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,
    bake_anim_step=1.0,
    bake_anim_simplify_factor=0.0,
    path_mode="AUTO",
    embed_textures=False,
)

triangles = 0
vertices = 0
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
    vertices += len(obj.data.vertices)

report = {
    "output_fbx": portable_path(output_fbx),
    "target_height_metres": TARGET_HEIGHT_METRES,
    "rest_bounds": {"min": rest_min, "max": rest_max, "dimensions": rest_dimensions},
    "export_global_scale": export_scale,
    "mesh": {"objects": len(mesh_objects), "vertices": vertices, "triangles": triangles},
    "armature": {"object": base_armature.name, "bones": len(base_armature.data.bones)},
    "motions": source_records,
    "exported_action_names": sorted(motions),
    "motion_validation": motion_validation,
    "texture": texture_record,
    "previews": preview_paths,
    "artifacts": {
        "fbx": {
            "file": portable_path(output_fbx),
            "bytes": os.path.getsize(output_fbx),
            "sha256": sha256_file(output_fbx),
        },
        "basecolor": {
            "file": portable_path(texture_path),
            "bytes": os.path.getsize(texture_path),
            "sha256": sha256_file(texture_path),
        },
    },
}
os.makedirs(os.path.dirname(report_path), exist_ok=True)
with open(report_path, "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)
print(json.dumps(report, indent=2, ensure_ascii=False))
