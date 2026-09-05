"""Prepare a freshly rigged Meshy Azure Maiden GLB for isolated review.

This script writes only to the supplied output directory. It imports a clean rigged GLB,
retargets the verified Meshy GLB motion donors through their bind/rest matrices,
adds a rigid right-hand katana, bakes exactly sixteen runtime actions, exports an
FBX/GLB pair, renders actual action frames, and reimports the FBX for validation.

Run with Blender 4.5 or newer, for example:

  blender.exe --background --python prepare_azure_maiden_clean.py -- \
    --source-glb C:/path/to/new-rigged.glb \
    --texture-source-glb C:/path/to/compatible-cleaner-atlas.glb \
    --motion-donor-dir C:/path/to/exact-rest-donors \
    --out-dir C:/path/to/new-result
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import statistics
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector


TASK_ID = "ORC-20260905-001"
WORK_PACKAGE = "WP16"
OUTPUT_ID = "OUT20"
FPS = 30
TARGET_HEIGHT_M = 1.62
ACTION_NAMES = [
    "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
    "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
    "Hurt", "Defeated", "Dodge",
]
DONOR_CANDIDATES = {
    "run": ("meshy-clean-run-fast.glb", "meshy-run-fast.glb"),
    "jump": ("meshy-clean-regular-jump.glb", "meshy-regular-jump.glb"),
    "sword": ("meshy-clean-katana-power-slash.glb", "meshy-katana-power-slash.glb"),
    "magic": ("meshy-clean-charged-spell.glb", "meshy-clean-charged-spell-cast.glb", "meshy-charged-spell-cast.glb"),
}
REQUIRED_BONES = {
    "Hips", "Spine", "Spine01", "Spine02", "Neck", "Head",
    "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
    "RightShoulder", "RightArm", "RightForeArm", "RightHand",
    "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase",
    "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
}


def parse_args() -> argparse.Namespace:
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-glb", type=Path, required=True)
    parser.add_argument("--texture-source-glb", type=Path, required=True)
    parser.add_argument("--motion-donor-dir", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    parser.add_argument("--target-height", type=float, default=TARGET_HEIGHT_M)
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


GLB_COMPONENT_SIZE = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
GLB_TYPE_COUNT = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}


def read_glb(path: Path) -> tuple[dict, bytes]:
    data = path.read_bytes()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF" or version != 2 or length != len(data):
        raise RuntimeError(f"Invalid GLB 2 container: {path}")
    offset = 12
    chunks = {}
    while offset < len(data):
        size, kind = struct.unpack_from("<II", data, offset)
        offset += 8
        chunks[kind] = data[offset:offset + size]
        offset += size
    return json.loads(chunks[0x4E4F534A]), chunks[0x004E4942]


def glb_accessor_digest(document: dict, binary: bytes, index: int) -> dict:
    accessor = document["accessors"][index]
    view = document["bufferViews"][accessor["bufferView"]]
    item_size = GLB_COMPONENT_SIZE[accessor["componentType"]] * GLB_TYPE_COUNT[accessor["type"]]
    stride = view.get("byteStride", item_size)
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    if stride == item_size:
        raw = binary[start:start + item_size * accessor["count"]]
    else:
        raw = b"".join(binary[start + row * stride:start + row * stride + item_size]
                       for row in range(accessor["count"]))
    return {"count": accessor["count"], "sha256": hashlib.sha256(raw).hexdigest()}


def verified_texture_swap(source_path: Path, texture_source_path: Path,
                          body: bpy.types.Object, out_dir: Path) -> dict:
    source_doc, source_bin = read_glb(source_path)
    texture_doc, texture_bin = read_glb(texture_source_path)
    source_primitive = source_doc["meshes"][0]["primitives"][0]
    texture_primitive = texture_doc["meshes"][0]["primitives"][0]
    source_indices = glb_accessor_digest(source_doc, source_bin, source_primitive["indices"])
    texture_indices = glb_accessor_digest(texture_doc, texture_bin, texture_primitive["indices"])
    source_uv = glb_accessor_digest(source_doc, source_bin, source_primitive["attributes"]["TEXCOORD_0"])
    texture_uv = glb_accessor_digest(texture_doc, texture_bin, texture_primitive["attributes"]["TEXCOORD_0"])
    source_position = glb_accessor_digest(source_doc, source_bin, source_primitive["attributes"]["POSITION"])
    texture_position = glb_accessor_digest(texture_doc, texture_bin, texture_primitive["attributes"]["POSITION"])
    if source_indices != texture_indices or source_uv != texture_uv or source_position["count"] != texture_position["count"]:
        raise RuntimeError("Direct retexture refused: topology/index/UV accessors are not exactly compatible")
    images = texture_doc.get("images", [])
    if len(images) != 1 or "bufferView" not in images[0] or images[0].get("mimeType") != "image/png":
        raise RuntimeError("Direct retexture requires exactly one embedded PNG atlas")
    image_view = texture_doc["bufferViews"][images[0]["bufferView"]]
    image_start = image_view.get("byteOffset", 0)
    image_bytes = texture_bin[image_start:image_start + image_view["byteLength"]]
    texture_dir = out_dir / "textures"
    texture_dir.mkdir(parents=True, exist_ok=True)
    extracted = texture_dir / "azure-maiden-base.png"
    extracted.write_bytes(image_bytes)
    atlas_hash = hashlib.sha256(image_bytes).hexdigest()
    image = bpy.data.images.load(str(extracted), check_existing=False)
    image.name = "AzureMaidenDirectRetexture"
    replaced = []
    for material in body.data.materials:
        if material is None or not material.use_nodes:
            continue
        for node in material.node_tree.nodes:
            if node.type == "TEX_IMAGE" and node.image is not None:
                replaced.append({"material": material.name, "node": node.name, "oldImage": node.image.name})
                node.image = image
    if not replaced:
        raise RuntimeError("Direct retexture found no body image node to replace")
    return {
        "path": str(texture_source_path), "bytes": texture_source_path.stat().st_size,
        "sha256": sha256(texture_source_path),
        "compatibility": {
            "positionCountMatch": source_position["count"] == texture_position["count"],
            "indicesExact": source_indices == texture_indices,
            "texcoord0Exact": source_uv == texture_uv,
            "sourcePositionSha256": source_position["sha256"],
            "textureRigPositionSha256": texture_position["sha256"],
            "geometryAndBindCopied": False,
        },
        "atlas": {"path": str(extracted), "bytes": len(image_bytes), "sha256": atlas_hash},
        "replacedNodes": replaced,
    }


def resolve_donors(directory: Path) -> dict[str, Path]:
    result = {}
    for label, candidates in DONOR_CANDIDATES.items():
        match = next((directory / name for name in candidates if (directory / name).is_file()), None)
        if match is None:
            raise FileNotFoundError(f"Missing {label} donor; tried: {', '.join(str(directory / name) for name in candidates)}")
        result[label] = match
    return result


def canonical(name: str) -> str:
    result = name.rsplit(":", 1)[-1].rsplit("|", 1)[-1]
    return result.replace(" ", "").replace("_", "").lower()


def bone_map(rig: bpy.types.Object) -> dict[str, str]:
    result: dict[str, str] = {}
    for bone in rig.data.bones:
        key = canonical(bone.name)
        if key in result:
            raise RuntimeError(f"Ambiguous canonical bone {key}: {result[key]}, {bone.name}")
        result[key] = bone.name
    return result


def lookup(mapping: dict[str, str], name: str) -> str:
    key = canonical(name)
    if key not in mapping:
        raise KeyError(name)
    return mapping[key]


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 720
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.fps = FPS
    scene.render.fps_base = 1.0
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("WP16World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.055, 0.065, 0.085, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    scene.world = world


def assign_action(rig: bpy.types.Object, action: bpy.types.Action | None) -> None:
    if rig.animation_data is None:
        rig.animation_data_create()
    rig.animation_data.action = action
    if action is None or not hasattr(rig.animation_data, "action_slot") or not hasattr(action, "slots"):
        bpy.context.view_layer.update()
        return
    slot = None
    for candidate in action.slots:
        ident = getattr(candidate, "identifier", "") or ""
        display = getattr(candidate, "name_display", "") or getattr(candidate, "name", "") or ""
        if rig.name in ident or rig.name in display:
            slot = candidate
            break
    if slot is None and len(action.slots):
        slot = action.slots[0]
    if slot is None:
        slot = action.slots.new("OBJECT", rig.name)
    rig.animation_data.action_slot = slot
    bpy.context.view_layer.update()


def clear_pose(rig: bpy.types.Object) -> None:
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def world_bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    if not points:
        raise RuntimeError("No mesh bounds")
    low = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    high = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return low, high


def ground_and_scale(rig: bpy.types.Object, meshes: list[bpy.types.Object], height: float) -> dict:
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    low, high = world_bounds(meshes)
    source_height = max(0.001, high.z - low.z)
    factor = height / source_height
    rig.scale = tuple(value * factor for value in rig.scale)
    bpy.context.view_layer.update()
    low, high = world_bounds(meshes)
    rig.location.x -= (low.x + high.x) * 0.5
    rig.location.y -= (low.y + high.y) * 0.5
    rig.location.z -= low.z
    bpy.context.view_layer.update()
    rig.data.pose_position = "POSE"
    return {"sourceHeight": source_height, "scaleFactor": factor, "targetHeight": height}


def choose_armature(objects: list[bpy.types.Object]) -> bpy.types.Object:
    rigs = [obj for obj in objects if obj.type == "ARMATURE"]
    if not rigs:
        raise RuntimeError("GLB contains no armature")
    return max(rigs, key=lambda obj: len(obj.data.bones))


def rig_meshes(rig: bpy.types.Object, objects: list[bpy.types.Object]) -> list[bpy.types.Object]:
    meshes = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        linked = obj.parent == rig or any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers)
        if linked:
            meshes.append(obj)
    if not meshes:
        meshes = [obj for obj in objects if obj.type == "MESH"]
    return meshes


def import_glb(path: Path) -> tuple[list[bpy.types.Object], list[bpy.types.Action]]:
    if not path.is_file():
        raise FileNotFoundError(path)
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(path))
    return (
        [obj for obj in bpy.data.objects if obj not in before_objects],
        [action for action in bpy.data.actions if action not in before_actions],
    )


def action_score(action: bpy.types.Action) -> tuple[float, int]:
    span = float(action.frame_range[1] - action.frame_range[0])
    slots = len(action.slots) if hasattr(action, "slots") else 0
    return span, slots


def select_substantive_action(actions: list[bpy.types.Action], source: Path) -> bpy.types.Action:
    if not actions:
        raise RuntimeError(f"No actions imported from {source.name}")
    preferred = [action for action in actions if "rigify_clip" in action.name.lower()]
    candidates = preferred or actions
    return max(candidates, key=action_score)


def parent_order(rig: bpy.types.Object) -> list[bpy.types.PoseBone]:
    def depth(bone: bpy.types.PoseBone) -> int:
        value, parent = 0, bone.parent
        while parent is not None:
            value += 1
            parent = parent.parent
        return value
    return sorted(rig.pose.bones, key=depth)


def rest_snapshot(rig: bpy.types.Object) -> dict[str, Matrix]:
    return {canonical(bone.name): bone.matrix_local.copy() for bone in rig.data.bones}


def pose_snapshot(rig: bpy.types.Object) -> dict[str, Matrix]:
    return {canonical(bone.name): bone.matrix.copy() for bone in rig.pose.bones}


def capture_donor(path: Path, fractions: set[float] | None = None, full: bool = False) -> dict:
    objects, actions = import_glb(path)
    rig = choose_armature(objects)
    mapping = bone_map(rig)
    missing = [name for name in REQUIRED_BONES if canonical(name) not in mapping]
    if missing:
        raise RuntimeError(f"{path.name} is missing bones: {missing}")
    action = select_substantive_action(actions, path)
    clear_pose(rig)
    rig.hide_viewport = False
    assign_action(rig, action)
    start, end = action.frame_range
    frames = set(range(int(round(start)), int(round(end)) + 1)) if full else set()
    for fraction in fractions or set():
        frames.add(int(round(start + (end - start) * fraction)))
    samples = {}
    for frame in sorted(frames):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = pose_snapshot(rig)
    report = {
        "path": str(path), "file": path.name, "bytes": path.stat().st_size,
        "sha256": sha256(path), "action": action.name,
        "frameRange": [float(start), float(end)], "durationSeconds": round((end - start) / FPS, 5),
        "bones": len(rig.data.bones), "boneNames": [bone.name for bone in rig.data.bones],
    }
    result = {"rest": rest_snapshot(rig), "samples": samples, "report": report}
    assign_action(rig, None)
    for obj in objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    for candidate in actions:
        if candidate.name in bpy.data.actions:
            bpy.data.actions.remove(candidate)
    return result


def clip_sample(clip: dict, fraction: float) -> dict[str, Matrix]:
    start, end = clip["report"]["frameRange"]
    wanted = int(round(start + (end - start) * fraction))
    frame = min(clip["samples"], key=lambda value: abs(value - wanted))
    return clip["samples"][frame]


def quaternion_angle(a: Matrix, b: Matrix) -> float:
    qa, qb = a.to_quaternion(), b.to_quaternion()
    angle = abs(qa.rotation_difference(qb).angle)
    return math.degrees(min(angle, abs(math.tau - angle)))


def compare_rests(target: dict[str, Matrix], donor: dict[str, Matrix]) -> dict:
    shared = sorted(set(target) & set(donor))
    if not shared:
        raise RuntimeError("Target and donor share no canonical bones")
    hips = canonical("Hips")
    t_origin = target[hips].translation
    d_origin = donor[hips].translation
    t_scale = max((matrix.translation - t_origin).length for matrix in target.values()) or 1.0
    d_scale = max((matrix.translation - d_origin).length for matrix in donor.values()) or 1.0
    orientation = [quaternion_angle(target[name], donor[name]) for name in shared]
    position = [
        ((target[name].translation - t_origin) / t_scale - (donor[name].translation - d_origin) / d_scale).length
        for name in shared
    ]
    result = {
        "sharedBones": len(shared),
        "targetBones": len(target),
        "donorBones": len(donor),
        "maxRestOrientationDegrees": round(max(orientation), 5),
        "medianRestOrientationDegrees": round(statistics.median(orientation), 5),
        "maxNormalizedHeadOffset": round(max(position), 7),
        "medianNormalizedHeadOffset": round(statistics.median(position), 7),
    }
    result["exactGlbRestMatch"] = result["maxRestOrientationDegrees"] < 0.05 and result["maxNormalizedHeadOffset"] < 0.0001
    result["strategy"] = "direct-compatible rest mapping" if result["exactGlbRestMatch"] else "per-bone targetRest @ inverse(donorRest) @ donorPose"
    result["visualReviewRequired"] = not result["exactGlbRestMatch"]
    return result


def validate_matrix_convention() -> dict:
    """Prove the multiplication order with a non-commuting 90-degree fixture."""
    donor_rest = Matrix.Translation((1.0, 2.0, 3.0)) @ Matrix.Rotation(math.radians(35.0), 4, "Z")
    local_motion = Matrix.Rotation(math.radians(90.0), 4, "Y")
    donor_pose = donor_rest @ local_motion
    target_rest = Matrix.Translation((-2.0, 0.5, 4.0)) @ Matrix.Rotation(math.radians(-40.0), 4, "X")
    expected = target_rest @ local_motion
    chosen = target_rest @ donor_rest.inverted_safe() @ donor_pose
    world_delta_alternative = donor_pose @ donor_rest.inverted_safe() @ target_rest
    chosen_error = max(abs(chosen[row][column] - expected[row][column]) for row in range(4) for column in range(4))
    alternative_error = max(abs(world_delta_alternative[row][column] - expected[row][column])
                            for row in range(4) for column in range(4))
    if chosen_error > 1e-6 or alternative_error < 1e-3:
        raise RuntimeError(f"Retarget matrix convention fixture failed: {chosen_error}, {alternative_error}")
    return {
        "fixture": "donor rest at Z+35deg, target rest at X-40deg, donor local motion Y+90deg",
        "chosenOrder": "targetRest @ inverse(donorRest) @ donorPose",
        "chosenMaxMatrixError": chosen_error,
        "worldDeltaAlternativeMaxMatrixError": round(alternative_error, 6),
        "result": "chosen order reproduces targetRest @ localMotion",
    }


def transferred_pose(target_rest: dict[str, Matrix], clip: dict, donor_pose: dict[str, Matrix]) -> dict[str, Matrix]:
    result = {}
    for name, target_matrix in target_rest.items():
        if name not in clip["rest"] or name not in donor_pose:
            result[name] = target_matrix.copy()
            continue
        result[name] = target_matrix @ clip["rest"][name].inverted_safe() @ donor_pose[name]
    return result


def blend_matrices(a: dict[str, Matrix], b: dict[str, Matrix], factor: float) -> dict[str, Matrix]:
    result = {}
    for name, a_matrix in a.items():
        b_matrix = b[name]
        a_loc, a_rot, a_scale = a_matrix.decompose()
        b_loc, b_rot, b_scale = b_matrix.decompose()
        result[name] = Matrix.LocRotScale(
            a_loc.lerp(b_loc, factor), a_rot.slerp(b_rot, factor), a_scale.lerp(b_scale, factor)
        )
    return result


def root_planted(pose: dict[str, Matrix], reference: Vector, plant_xy: bool, plant_z: bool) -> dict[str, Matrix]:
    hips = canonical("Hips")
    current = pose[hips].translation
    correction = Vector((reference.x - current.x if plant_xy else 0.0,
                         reference.y - current.y if plant_xy else 0.0,
                         reference.z - current.z if plant_z else 0.0))
    transform = Matrix.Translation(correction)
    return {name: transform @ matrix for name, matrix in pose.items()}


def apply_armature_pose(rig: bpy.types.Object, pose: dict[str, Matrix]) -> None:
    mapping = bone_map(rig)
    clear_pose(rig)
    bpy.context.view_layer.update()
    for bone in parent_order(rig):
        matrix = pose.get(canonical(bone.name))
        if matrix is not None:
            bone.matrix = matrix
            bpy.context.view_layer.update()


def key_pose(rig: bpy.types.Object, frame: int) -> None:
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.keyframe_insert("location", frame=frame, group=bone.name)
        bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
        bone.keyframe_insert("scale", frame=frame, group=bone.name)


def write_action(rig: bpy.types.Object, name: str, keys: list[tuple[int, dict[str, Matrix]]],
                 root_reference: Vector, plant_xy: bool = True, plant_z: bool = False) -> bpy.types.Action:
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    assign_action(rig, action)
    for frame, pose in keys:
        apply_armature_pose(rig, root_planted(pose, root_reference, plant_xy, plant_z))
        key_pose(rig, frame)
    return action


def copy_action(source: bpy.types.Action, name: str) -> bpy.types.Action:
    result = source.copy()
    result.name = name
    result.use_fake_user = True
    return result


def pose_delta(rig: bpy.types.Object, action: bpy.types.Action) -> float:
    assign_action(rig, action)
    poses = []
    for frame in (int(round(action.frame_range[0])), int(round(sum(action.frame_range) * 0.5))):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses.append({canonical(bone.name): bone.matrix.copy() for bone in rig.pose.bones})
    return sum((poses[0][name].translation - poses[1][name].translation).length +
               poses[0][name].to_quaternion().rotation_difference(poses[1][name].to_quaternion()).angle
               for name in poses[0])


def build_actions(rig: bpy.types.Object, target_rest: dict[str, Matrix], clips: dict) -> dict:
    # The clean source is authored in the requested neutral A-pose. Use that
    # bind/rest pose for Idle instead of borrowing a frame from another body.
    base = {name: matrix.copy() for name, matrix in target_rest.items()}
    root_reference = base[canonical("Hips")].translation.copy()
    idle = write_action(rig, "Idle", [(1, base), (30, base)], root_reference)

    run_clip = clips["run"]
    run_frames = sorted(run_clip["samples"])
    run_poses = [transferred_pose(target_rest, run_clip, run_clip["samples"][frame]) for frame in run_frames]
    walk = write_action(rig, "Walk", [(i + 1, blend_matrices(base, pose, 0.42)) for i, pose in enumerate(run_poses)], root_reference)
    run = write_action(rig, "Run", [(i + 1, pose) for i, pose in enumerate(run_poses)], root_reference)

    def sample(label: str, fraction: float) -> dict[str, Matrix]:
        return transferred_pose(target_rest, clips[label], clip_sample(clips[label], fraction))

    jump = write_action(rig, "Jump", [(1, sample("jump", 0.00)), (7, sample("jump", 0.18)),
                                       (13, sample("jump", 0.42))], root_reference, plant_z=True)
    fall = write_action(rig, "Fall", [(1, sample("jump", 0.42)), (9, sample("jump", 0.62)),
                                       (18, sample("jump", 0.75))], root_reference, plant_z=True)
    land = write_action(rig, "Land", [(1, sample("jump", 0.75)), (4, sample("jump", 0.88)),
                                       (8, sample("jump", 1.00))], root_reference, plant_z=True)

    # Gameplay emits the hit at PlayAction start. Frame 1 is therefore already
    # the donor cut, while the remaining 1.05 s is visual hold and recovery.
    sword = write_action(rig, "Sword", [(1, sample("sword", 0.50)), (4, sample("sword", 0.60)),
                                         (11, sample("sword", 0.75)), (18, sample("sword", 0.82)),
                                         (26, blend_matrices(base, sample("sword", 0.88), 0.35)), (32.5, base)], root_reference)
    air = copy_action(sword, "AirSlash")
    plunge = copy_action(sword, "Plunge")

    magic_charge = write_action(rig, "MagicCharge", [(1, sample("magic", 0.00)),
                                                       (10, sample("magic", 0.25)),
                                                       (20, sample("magic", 0.58))], root_reference)
    magic_release = write_action(rig, "MagicRelease", [(1, sample("magic", 0.96)),
                                                         (8, sample("magic", 0.99)),
                                                         (18, blend_matrices(base, sample("magic", 1.00), 0.35)),
                                                         (25, base)], root_reference)
    spin_charge = copy_action(magic_charge, "SpinCharge")
    spin_release = copy_action(sword, "SpinRelease")
    dodge = write_action(rig, "Dodge", [(1, sample("sword", 0.50)), (8, sample("sword", 0.68)),
                                         (16, sample("sword", 0.82)), (24, base)], root_reference)
    hurt_pose = blend_matrices(base, sample("jump", 0.75), 0.45)
    hurt = write_action(rig, "Hurt", [(1, base), (4, hurt_pose), (10, base)], root_reference, plant_z=True)
    defeated = write_action(rig, "Defeated", [(1, base), (10, sample("jump", 0.75)),
                                               (30, sample("jump", 0.75))], root_reference, plant_z=True)
    assign_action(rig, idle)
    names = sorted(action.name for action in bpy.data.actions)
    if names != sorted(ACTION_NAMES):
        raise RuntimeError(f"Expected exactly 16 actions, got {names}")
    motion = {name: round(pose_delta(rig, bpy.data.actions[name]), 6)
              for name in ("Walk", "Run", "Jump", "Sword", "MagicCharge", "MagicRelease", "Dodge")}
    if any(value <= 0.01 for value in motion.values()):
        raise RuntimeError(f"Action lost motion: {motion}")
    return {
        "actions": names,
        "motionDelta": motion,
        "eventTiming": {
            "combatEmission": "PlayAction frame 1",
            "swordHitFrame": 1,
            "swordGameplayCooldownSeconds": 0.34,
            "swordVisualRecoverySeconds": round((sword.frame_range[1] - sword.frame_range[0]) / FPS, 3),
            "swordInterruptibleAfterSeconds": 0.34,
            "magicReleaseEmissionFrame": 1,
        },
        "rootMotion": "Hips X/Y planted for all actions; Jump/Fall/Land Z planted because the game motor owns Y",
    }


def mesh_analysis(rig: bpy.types.Object, meshes: list[bpy.types.Object]) -> dict:
    mapping = bone_map(rig)
    groups = {canonical(name) for mesh in meshes for name in mesh.vertex_groups.keys()}
    weighted, unweighted, max_influences, sums = 0, 0, 0, []
    vertices = triangles = 0
    per_mesh = []
    for mesh in meshes:
        mesh.data.calc_loop_triangles()
        vertices += len(mesh.data.vertices)
        triangles += len(mesh.data.loop_triangles)
        local_unweighted = 0
        for vertex in mesh.data.vertices:
            weights = [membership.weight for membership in vertex.groups if membership.weight > 1e-6]
            max_influences = max(max_influences, len(weights))
            if weights:
                weighted += 1
                sums.append(sum(weights))
            else:
                unweighted += 1
                local_unweighted += 1
        per_mesh.append({"name": mesh.name, "vertices": len(mesh.data.vertices),
                         "triangles": len(mesh.data.loop_triangles), "materials": len(mesh.data.materials),
                         "unweightedVertices": local_unweighted})
    required_missing = sorted(name for name in REQUIRED_BONES if canonical(name) not in mapping)
    weighted_group_missing = sorted(name for name in REQUIRED_BONES if canonical(name) not in groups)
    if required_missing:
        raise RuntimeError(f"Source rig missing required bones: {required_missing}")
    if unweighted:
        raise RuntimeError(f"Source contains {unweighted} unweighted vertices")
    return {
        "meshes": per_mesh, "meshCount": len(meshes), "vertices": vertices, "triangles": triangles,
        "bones": len(rig.data.bones), "boneNames": [bone.name for bone in rig.data.bones],
        "requiredBonesMissing": required_missing, "requiredWeightedGroupsMissing": weighted_group_missing,
        "weightedVertices": weighted, "unweightedVertices": unweighted, "maxInfluences": max_influences,
        "weightSumMin": round(min(sums), 6) if sums else None,
        "weightSumMax": round(max(sums), 6) if sums else None,
    }


def add_box(vertices: list, faces: list, materials: list, low: tuple, high: tuple, material_index: int) -> None:
    x0, y0, z0 = low
    x1, y1, z1 = high
    start = len(vertices)
    vertices.extend([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),
                     (x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)])
    faces.extend([(start+a,start+b,start+c,start+d) for a,b,c,d in
                  ((0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(4,0,3,7))])
    materials.extend([material_index] * 6)


def apply_static_right_grip(rig: bpy.types.Object, meshes: list[bpy.types.Object]) -> dict:
    """Conservatively close only the distal RightHand-weighted geometry.

    The 24-bone Meshy skeleton has no finger joints, so this creates a static
    grip in bind geometry. Wrist/palm vertices stay fixed and the left hand is
    explicitly excluded. Coordinates are normalized from the actual hand cloud
    rather than assumed from FBX axes.
    """
    if len(meshes) != 1:
        raise RuntimeError("Static grip requires the verified single source mesh")
    body = meshes[0]
    groups = {canonical(group.name): group for group in body.vertex_groups}
    right = groups.get(canonical("RightHand"))
    left = groups.get(canonical("LeftHand"))
    if right is None or left is None:
        raise RuntimeError("Static grip requires RightHand and LeftHand vertex groups")
    bone_name = lookup(bone_map(rig), "RightHand")
    bone = rig.data.bones[bone_name]
    to_bone = bone.matrix_local.inverted() @ rig.matrix_world.inverted() @ body.matrix_world
    from_bone = to_bone.inverted()

    rows = []
    weights = {}
    left_weights = {}
    for vertex in body.data.vertices:
        right_weight = 0.0
        left_weight = 0.0
        for link in vertex.groups:
            if link.group == right.index:
                right_weight = link.weight
            elif link.group == left.index:
                left_weight = link.weight
        if right_weight >= 0.50:
            point = to_bone @ vertex.co
            rows.append((point.y, vertex.index))
            weights[vertex.index] = right_weight
            left_weights[vertex.index] = left_weight
    if len(rows) < 500:
        raise RuntimeError(f"Right-hand selection unexpectedly small: {len(rows)}")
    y_values = sorted(y for y, _ in rows)
    quantile = lambda fraction: y_values[int(round((len(y_values) - 1) * fraction))]
    y_start = quantile(0.27)
    y_end = quantile(0.96)
    span = max(1e-6, y_end - y_start)
    moved = 0
    thumb_moved = 0
    max_delta = 0.0
    wrist_moved = 0
    left_hand_moved = 0
    for vertex in body.data.vertices:
        weight = weights.get(vertex.index, 0.0)
        if weight < 0.50 or left_weights.get(vertex.index, 0.0) > 0.10:
            continue
        point = to_bone @ vertex.co
        if point.y < 2.80:
            continue
        is_thumb = point.x > 2.80 and point.y < 7.20
        t = min(1.0, max(0.0, (point.y - y_start) / span))
        smooth = t * t * (3.0 - 2.0 * t)
        weight_strength = min(1.0, max(0.0, (weight - 0.50) / 0.45))
        influence = ((min(1.0, max(0.0, (point.x - 2.80) / 2.20))
                      if is_thumb else smooth) * weight_strength)
        if influence <= 1e-6:
            continue
        target = point.copy()
        if is_thumb:
            # Fold the geometric thumb across the handle instead of leaving the
            # conspicuous open-hand silhouette produced by a Y-only falloff.
            target.x = point.x + (1.55 - point.x) * (0.78 * influence)
            target.y = point.y + (5.35 - point.y) * (0.42 * influence)
            target.z = point.z + (-1.65 - point.z) * (0.58 * influence)
            thumb_moved += 1
        else:
            # Draw spread fingertips toward the handle axis, shorten their
            # forward reach, and curl them around the grip underside.
            target.x *= (1.0 - 0.62 * influence)
            target.y = y_start + (point.y - y_start) * (1.0 - 0.64 * influence)
            curl_depth = -2.80 + 0.45 * t
            target.z = point.z + (curl_depth - point.z) * (0.84 * influence)
        delta = (target - point).length
        vertex.co = from_bone @ target
        moved += 1
        max_delta = max(max_delta, delta)
        if point.y < 2.80:
            wrist_moved += 1
        if left_weights.get(vertex.index, 0.0) > 0.10:
            left_hand_moved += 1
    body.data.update()
    if moved < 250 or wrist_moved or left_hand_moved:
        raise RuntimeError(f"Static grip safety check failed: moved={moved}, wrist={wrist_moved}, left={left_hand_moved}")
    return {
        "method": "bind-space distal RightHand vertex curl",
        "rightHandCandidates": len(rows),
        "movedVertices": moved,
        "thumbVerticesMoved": thumb_moved,
        "minimumRightHandWeight": 0.50,
        "distalFingerBoundaryBoneLocalY": round(y_start, 6),
        "hardWristPreservationBoneLocalY": 2.80,
        "distalReferenceBoneLocalY": round(y_end, 6),
        "maxBoneLocalDelta": round(max_delta, 6),
        "wristVerticesMoved": wrist_moved,
        "leftHandVerticesMoved": left_hand_moved,
    }


def add_tube(vertices: list, faces: list, materials: list, rings: list[tuple], sides: int,
             material_for_segment) -> None:
    """Add an elliptical tube along local Y; ring tuples are (y, cx, rx, rz)."""
    starts = []
    for y, cx, rx, rz in rings:
        start = len(vertices)
        starts.append(start)
        for index in range(sides):
            angle = 2.0 * math.pi * index / sides
            vertices.append((cx + rx * math.cos(angle), y, rz * math.sin(angle)))
    for ring_index in range(len(rings) - 1):
        a = starts[ring_index]
        b = starts[ring_index + 1]
        for side in range(sides):
            faces.append((a + side, a + (side + 1) % sides,
                          b + (side + 1) % sides, b + side))
            materials.append(material_for_segment(ring_index, side))
    faces.append(tuple(reversed([starts[0] + side for side in range(sides)])))
    materials.append(material_for_segment(0, 0))
    faces.append(tuple(starts[-1] + side for side in range(sides)))
    materials.append(material_for_segment(max(0, len(rings) - 2), 0))


def add_curved_blade(vertices: list, faces: list, materials: list) -> None:
    # Hexagonal sections create a real bevel rather than a rectangular bar.
    # Curvature and taper are intentionally modest so the silhouette reads as a
    # katana without fighting the hand-authored animation.
    sections = []
    for index in range(11):
        t = index / 10.0
        y = 0.108 + 0.742 * t
        center_x = -0.036 * (t ** 1.65)
        half_width = 0.0165 * (1.0 - 0.46 * t)
        thickness = 0.0045 * (1.0 - 0.30 * t)
        sections.append((y, center_x, half_width, thickness))
    starts = []
    for y, center_x, width, thickness in sections:
        start = len(vertices)
        starts.append(start)
        vertices.extend([
            (center_x - width * 0.62, y, thickness),
            (center_x + width * 0.62, y, thickness),
            (center_x + width, y, 0.0),
            (center_x + width * 0.62, y, -thickness),
            (center_x - width * 0.62, y, -thickness),
            (center_x - width, y, 0.0),
        ])
    for section in range(len(sections) - 1):
        a, b = starts[section], starts[section + 1]
        for side in range(6):
            faces.append((a + side, a + (side + 1) % 6, b + (side + 1) % 6, b + side))
            # The cutting edge is brighter; the broad spine/faces are darker.
            materials.append(0 if side in (1, 2) else 1)
    faces.append(tuple(reversed([starts[0] + side for side in range(6)])))
    materials.append(1)
    tip = len(vertices)
    vertices.append((-0.043, 0.898, 0.0))
    last = starts[-1]
    for side in range(6):
        faces.append((last + side, last + (side + 1) % 6, tip))
        materials.append(0 if side in (1, 2) else 1)


def create_katana(rig: bpy.types.Object) -> bpy.types.Object:
    mapping = bone_map(rig)
    right_hand = lookup(mapping, "RightHand")
    verts: list[tuple] = []
    faces: list[tuple] = []
    material_indices: list[int] = []
    grip_rings = []
    for index in range(11):
        t = index / 10.0
        # Raised alternating rings read as a dark wrapped tsuka in motion.
        swell = 1.0 + (0.08 if index % 2 else 0.0)
        grip_rings.append((-0.065 + 0.147 * t, 0.0, 0.0135 * swell, 0.0105 * swell))
    add_tube(verts, faces, material_indices, grip_rings, 8,
             lambda ring, side: 2 if ring % 2 == 0 else 3)
    # Shaped twelve-sided tsuba, brass collar, and pommel.
    add_tube(verts, faces, material_indices,
             [(0.083, 0.0, 0.045, 0.030), (0.096, 0.0, 0.045, 0.030)], 12,
             lambda ring, side: 4)
    add_tube(verts, faces, material_indices,
             [(0.096, 0.0, 0.019, 0.014), (0.114, 0.0, 0.017, 0.012)], 8,
             lambda ring, side: 5)
    add_tube(verts, faces, material_indices,
             [(-0.078, 0.0, 0.017, 0.013), (-0.064, 0.0, 0.014, 0.011)], 8,
             lambda ring, side: 5)
    add_curved_blade(verts, faces, material_indices)
    mesh = bpy.data.meshes.new("AzureMaidenKatanaMesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    katana = bpy.data.objects.new("AzureMaidenKatana", mesh)
    bpy.context.collection.objects.link(katana)
    for name, color, metallic, roughness in (
        ("KatanaPolishedEdge", (0.72, 0.80, 0.91, 1.0), 0.90, 0.14),
        ("KatanaSteelSpine", (0.20, 0.28, 0.39, 1.0), 0.80, 0.24),
        ("KatanaWrappedGripBlack", (0.012, 0.009, 0.015, 1.0), 0.05, 0.78),
        ("KatanaWrappedGripCrimson", (0.12, 0.018, 0.025, 1.0), 0.10, 0.62),
        ("KatanaShapedTsuba", (0.24, 0.12, 0.025, 1.0), 0.72, 0.30),
        ("KatanaBrassFittings", (0.47, 0.25, 0.045, 1.0), 0.72, 0.23),
    ):
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = color
        mat.metallic = metallic
        mat.roughness = roughness
        mesh.materials.append(mat)
    for polygon, index in zip(mesh.polygons, material_indices):
        polygon.material_index = index
    rig.data.pose_position = "REST"
    bpy.context.view_layer.update()
    hand_world = rig.matrix_world @ rig.pose.bones[right_hand].matrix
    # Meshy imports the armature object at 0.01 scale. Bone parenting therefore
    # inherits that scale unless the desired world transform is restored after
    # parent_set. Build a unit-scale hand transform so the 0.98 m sword remains
    # human-sized in Blender and in the exported runtime files.
    desired_world = (Matrix.Translation(hand_world.translation) @
                     hand_world.to_quaternion().to_matrix().to_4x4() @
                     Matrix.Translation((0.0, -0.015, -0.015)))
    katana.matrix_world = desired_world
    bpy.ops.object.select_all(action="DESELECT")
    katana.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    rig.data.bones.active = rig.data.bones[right_hand]
    bpy.ops.object.parent_set(type="BONE_RELATIVE")
    katana.matrix_world = desired_world
    rig.data.pose_position = "POSE"
    bpy.context.view_layer.update()
    if katana.parent != rig or katana.parent_bone != right_hand:
        raise RuntimeError("Katana bone parenting failed")
    return katana


def add_studio() -> None:
    ground_mat = bpy.data.materials.new("PreviewOnlyGroundMaterial")
    ground_mat.diffuse_color = (0.12, 0.14, 0.18, 1.0)
    bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, -0.006))
    ground = bpy.context.object
    ground.name = "PreviewOnly_Ground"
    ground.data.materials.append(ground_mat)
    bpy.ops.object.light_add(type="AREA", location=(-2.8, -3.6, 4.5))
    key = bpy.context.object
    key.name = "PreviewOnly_Key"
    key.data.energy = 1150
    key.data.shape = "DISK"
    key.data.size = 4.0
    key.rotation_euler = (math.radians(25), 0.0, math.radians(-35))
    bpy.ops.object.light_add(type="AREA", location=(2.5, -0.5, 2.8))
    fill = bpy.context.object
    fill.name = "PreviewOnly_Fill"
    fill.data.energy = 650
    fill.data.size = 3.0


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_pose(rig: bpy.types.Object, action: bpy.types.Action | None, frame: int, path: Path,
                camera_side: str = "front", rest: bool = False) -> None:
    rig.data.pose_position = "REST" if rest else "POSE"
    assign_action(rig, action)
    scene = bpy.context.scene
    # Keep frame selection here. Earlier preview helpers rendered frame 1 after
    # callers selected another frame; every evidence image now owns its frame.
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    camera_data = bpy.data.cameras.new("WP16EvidenceCamera")
    camera_data.lens = 50
    camera = bpy.data.objects.new("WP16EvidenceCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (0.0, -3.15, 0.88) if camera_side == "front" else (3.15, 0.0, 0.88)
    point_camera(camera, Vector((0.0, 0.0, 0.82)))
    scene.camera = camera
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)
    rig.data.pose_position = "POSE"


def render_action(rig: bpy.types.Object, action_name: str, frame: int, path: Path,
                  camera_side: str = "front") -> None:
    render_pose(rig, bpy.data.actions[action_name], frame, path, camera_side)


def render_source_evidence(rig: bpy.types.Object, source_action: bpy.types.Action | None, out_dir: Path) -> dict:
    specs = [("source-neutral", None, 1, True)]
    if source_action is not None:
        midpoint = int(round(sum(source_action.frame_range) * 0.5))
        specs.append(("source-running", source_action, midpoint, False))
    result = {}
    for label, action, frame, rest in specs:
        for side in ("front", "side"):
            path = out_dir / "previews" / f"{label}-{side}.png"
            render_pose(rig, action, frame, path, side, rest=rest)
            result[path.name] = {
                "action": action.name if action else "REST", "frame": frame,
                "sha256": sha256(path), "path": str(path),
            }
    return result


def render_evidence(rig: bpy.types.Object, out_dir: Path) -> dict:
    specs = [
        ("neutral", "Idle", 1),
        ("run-cycle-a", "Run", 1),
        ("run-cycle-b", "Run", int(round(sum(bpy.data.actions["Run"].frame_range) * 0.5))),
        ("run-cycle-c", "Run", int(round(bpy.data.actions["Run"].frame_range[1]))),
        ("jump-launch", "Jump", 1),
        ("jump-rise", "Jump", 7),
        ("jump-peak", "Jump", 13),
        ("sword-emission", "Sword", 1),
        ("sword-followthrough", "Sword", 11),
        ("sword-recovery", "Sword", 26),
        ("sword-neutral", "Sword", 32),
        ("magic-release-emission", "MagicRelease", 1),
        ("magic-release-peak", "MagicRelease", 8),
        ("magic-release-recovery", "MagicRelease", 18),
        ("magic-release-neutral", "MagicRelease", 25),
    ]
    result = {}
    for label, action, frame in specs:
        for side in ("front", "side"):
            path = out_dir / "previews" / f"{label}-{side}.png"
            render_action(rig, action, frame, path, side)
            result[path.name] = {"action": action, "frame": frame, "sha256": sha256(path), "path": str(path)}
    return result


def render_grip_closeups(rig: bpy.types.Object, out_dir: Path) -> dict:
    scene = bpy.context.scene
    prior_resolution = (scene.render.resolution_x, scene.render.resolution_y)
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    result = {}
    hand_name = lookup(bone_map(rig), "RightHand")
    for label, action_name, frame in (("grip-sword-emission", "Sword", 1),
                                      ("grip-sword-followthrough", "Sword", 11)):
        assign_action(rig, bpy.data.actions[action_name])
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        hand_matrix = rig.matrix_world @ rig.pose.bones[hand_name].matrix
        hand = hand_matrix.translation
        palm_axis = (hand_matrix.to_3x3() @ Vector((0.0, 0.0, 1.0))).normalized()
        for side in ("front", "side", "palm"):
            camera_data = bpy.data.cameras.new("WP16GripCamera")
            camera_data.type = "ORTHO"
            camera_data.ortho_scale = 0.42
            camera = bpy.data.objects.new("WP16GripCamera", camera_data)
            bpy.context.collection.objects.link(camera)
            camera.location = hand + (Vector((0.0, -0.72, 0.0)) if side == "front" else
                                      Vector((0.72, 0.0, 0.0)) if side == "side" else palm_axis * 0.72)
            point_camera(camera, hand)
            scene.camera = camera
            path = out_dir / "previews" / f"{label}-{side}.png"
            scene.render.filepath = str(path)
            bpy.ops.render.render(write_still=True)
            result[path.name] = {"action": action_name, "frame": frame, "sha256": sha256(path), "path": str(path)}
            bpy.data.objects.remove(camera, do_unlink=True)
            bpy.data.cameras.remove(camera_data)
    scene.render.resolution_x, scene.render.resolution_y = prior_resolution
    return result


def save_textures(out_dir: Path) -> list[dict]:
    texture_dir = out_dir / "textures"
    texture_dir.mkdir(parents=True, exist_ok=True)
    result = []
    for index, image in enumerate(bpy.data.images):
        if image.type != "IMAGE" or image.size[0] <= 0 or image.size[1] <= 0:
            continue
        stem = Path(image.name).stem or f"image-{index}"
        path = (texture_dir / "azure-maiden-base.png" if image.name == "AzureMaidenDirectRetexture"
                else texture_dir / f"{stem}.png")
        if image.name == "AzureMaidenDirectRetexture":
            # Keep the extracted atlas byte-for-byte identical to the verified
            # Meshy payload; Blender's image.save() would losslessly recompress
            # it and make the recorded provenance hash misleading.
            result.append({"name": image.name, "path": str(path), "width": image.size[0], "height": image.size[1],
                           "sha256": sha256(path)})
            continue
        try:
            image.filepath_raw = str(path)
            image.file_format = "PNG"
            image.save()
        except RuntimeError:
            continue
        result.append({"name": image.name, "path": str(path), "width": image.size[0], "height": image.size[1],
                       "sha256": sha256(path)})
    return result


def select_runtime_objects(rig: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj == rig or (obj.type == "MESH" and not obj.name.startswith("PreviewOnly")):
            obj.hide_set(False)
            obj.hide_render = False
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig


def export_runtime(rig: bpy.types.Object, out_dir: Path) -> dict:
    fbx = out_dir / "azure-maiden-clean-runtime.fbx"
    glb = out_dir / "azure-maiden-clean-runtime.glb"
    select_runtime_objects(rig)
    bpy.ops.export_scene.fbx(
        filepath=str(fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", use_space_transform=True,
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="COPY", embed_textures=False,
    )
    select_runtime_objects(rig)
    bpy.ops.export_scene.gltf(
        filepath=str(glb), export_format="GLB", use_selection=True,
        export_apply=False, export_animations=True, export_animation_mode="ACTIONS",
        export_materials="EXPORT", export_yup=True,
    )
    return {"fbx": str(fbx), "fbxSha256": sha256(fbx), "glb": str(glb), "glbSha256": sha256(glb)}


def reimport_validate(fbx_path: Path) -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), automatic_bone_orientation=False)
    rig = max((obj for obj in bpy.data.objects if obj.type == "ARMATURE"), key=lambda obj: len(obj.data.bones))
    actions = list(bpy.data.actions)
    mapped = {}
    for required in ACTION_NAMES:
        matches = [action for action in actions if action.name == required or action.name.endswith("|" + required)]
        if len(matches) != 1:
            raise RuntimeError(f"FBX action mapping failed for {required}: {[action.name for action in matches]}")
        mapped[required] = matches[0]
    extras = [action.name for action in actions if action not in mapped.values()]
    if extras:
        raise RuntimeError(f"Unexpected FBX actions: {extras}")
    mapping = bone_map(rig)
    hips = lookup(mapping, "Hips")
    motion, root_xy, root_z = {}, {}, {}
    for name in ("Run", "Jump", "Sword", "MagicCharge", "MagicRelease", "Dodge"):
        action = mapped[name]
        motion[name] = round(pose_delta(rig, action), 6)
        assign_action(rig, action)
        positions = []
        for frame in range(int(round(action.frame_range[0])), int(round(action.frame_range[1])) + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            positions.append((rig.matrix_world @ rig.pose.bones[hips].matrix).translation.copy())
        root_xy[name] = round(max(Vector((p.x - positions[0].x, p.y - positions[0].y)).length for p in positions), 7)
        root_z[name] = round(max(abs(p.z - positions[0].z) for p in positions), 7)
        if motion[name] <= 0.01 or root_xy[name] > 0.001:
            raise RuntimeError(f"FBX motion/root validation failed for {name}: {motion[name]}, {root_xy[name]}")
    for name in ("Jump", "Fall", "Land"):
        action = mapped[name]
        assign_action(rig, action)
        positions = []
        for frame in range(int(round(action.frame_range[0])), int(round(action.frame_range[1])) + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            positions.append((rig.matrix_world @ rig.pose.bones[hips].matrix).translation.copy())
        root_z[name] = round(max(abs(p.z - positions[0].z) for p in positions), 7)
        if root_z[name] > 0.001:
            raise RuntimeError(f"Motor-owned vertical root remained in {name}: {root_z[name]}")
    katana = next((obj for obj in bpy.data.objects if "Katana" in obj.name), None)
    katana_dimensions = None
    if katana is not None:
        assign_action(rig, mapped["Idle"])
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()
        katana_dimensions = [round(value, 5) for value in katana.dimensions]
        if max(katana_dimensions) < 0.75:
            raise RuntimeError(f"FBX katana is undersized after bone parenting: {katana_dimensions}")
    return {
        "actions": {name: mapped[name].name for name in ACTION_NAMES},
        "actionCount": len(mapped), "extraActions": extras,
        "bones": len(rig.data.bones), "motionDelta": motion,
        "rootXYMaxExcursion": root_xy, "rootZMaxExcursion": root_z,
        "katana": {"present": katana is not None, "parent": katana.parent.name if katana and katana.parent else None,
                   "parentBone": katana.parent_bone if katana else None, "dimensions": katana_dimensions},
    }


def main() -> None:
    args = parse_args()
    args.source_glb = args.source_glb.resolve()
    args.texture_source_glb = args.texture_source_glb.resolve()
    args.motion_donor_dir = args.motion_donor_dir.resolve()
    args.out_dir = args.out_dir.resolve()
    if args.out_dir.exists() and any(args.out_dir.iterdir()):
        raise FileExistsError("Use a new output directory to preserve previous derivatives")
    args.out_dir.mkdir(parents=True, exist_ok=True)
    missing = [str(path) for path in (args.source_glb, args.texture_source_glb) if not path.is_file()]
    if missing:
        raise FileNotFoundError("Missing required inputs: " + ", ".join(missing))
    donor_paths = resolve_donors(args.motion_donor_dir)

    reset_scene()
    source_objects, source_actions = import_glb(args.source_glb)
    rig = choose_armature(source_objects)
    meshes = rig_meshes(rig, source_objects)
    rig.name = "AzureMaidenCleanRig"
    for index, mesh in enumerate(meshes, 1):
        mesh.name = "AzureMaidenCleanBody" if len(meshes) == 1 else f"AzureMaidenCleanBody{index:02d}"
        for material in mesh.data.materials:
            if material:
                material.use_backface_culling = False
    source_action = select_substantive_action(source_actions, args.source_glb) if source_actions else None
    source_action_report = None
    if source_action is not None:
        source_action_report = {"name": source_action.name, "frameRange": [float(v) for v in source_action.frame_range],
                                "durationSeconds": round((source_action.frame_range[1] - source_action.frame_range[0]) / FPS, 5)}
    assign_action(rig, None)
    clear_pose(rig)
    scale_report = ground_and_scale(rig, meshes, args.target_height)
    target_rest = rest_snapshot(rig)
    source_report = mesh_analysis(rig, meshes)
    texture_report = verified_texture_swap(args.source_glb, args.texture_source_glb, meshes[0], args.out_dir)
    add_studio()
    source_previews = render_source_evidence(rig, source_action, args.out_dir)
    assign_action(rig, None)
    clear_pose(rig)
    for action in source_actions:
        if action.name in bpy.data.actions:
            bpy.data.actions.remove(action)
    grip_report = apply_static_right_grip(rig, meshes)

    fraction_sets = {
        "run": set(),
        "jump": {0.0, 0.18, 0.42, 0.62, 0.75, 0.88, 1.0},
        "sword": {0.50, 0.60, 0.68, 0.75, 0.82, 0.88},
        "magic": {0.0, 0.25, 0.58, 0.96, 0.99, 1.0},
    }
    clips = {}
    for label, path in donor_paths.items():
        clips[label] = capture_donor(path, fraction_sets[label], full=(label == "run"))
    compatibility = {label: compare_rests(target_rest, clip["rest"]) for label, clip in clips.items()}
    hierarchy_target = {canonical(bone.name): canonical(bone.parent.name) if bone.parent else None for bone in rig.data.bones}
    for label, clip in clips.items():
        if set(target_rest) != set(clip["rest"]):
            raise RuntimeError(f"Bone set differs for {label}; safe automatic retarget refused")

    action_report = build_actions(rig, target_rest, clips)
    katana = create_katana(rig)
    katana["rigid_parent"] = "RightHand"
    katana["grip_axis"] = "+Y along RightHand rest axis"
    previews = render_evidence(rig, args.out_dir)
    previews = {**previews, **render_grip_closeups(rig, args.out_dir)}
    previews = {**source_previews, **previews}
    textures = save_textures(args.out_dir)
    katana_report = {"name": katana.name, "parentBone": katana.parent_bone,
                     "vertices": len(katana.data.vertices),
                     "triangles": sum(len(p.vertices)-2 for p in katana.data.polygons),
                     "materials": [material.name for material in katana.data.materials],
                     "dimensions": [round(value, 5) for value in katana.dimensions],
                     "design": "curved tapered hex-bevel blade, wrapped octagonal grip, shaped tsuba"}
    blend = args.out_dir / "azure-maiden-clean-runtime.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    exports = export_runtime(rig, args.out_dir)
    reimport = reimport_validate(Path(exports["fbx"]))
    report = {
        "schemaVersion": 1,
        "taskId": TASK_ID, "workPackage": WORK_PACKAGE, "outputId": OUTPUT_ID,
        "status": "prepared-awaiting-independent-visual-acceptance",
        "source": {"path": str(args.source_glb), "bytes": args.source_glb.stat().st_size,
                   "sha256": sha256(args.source_glb)},
        "sourceGeometryAndSkin": source_report,
        "textureSource": texture_report,
        "sourceEmbeddedAction": source_action_report,
        "scale": scale_report,
        "donors": {label: clip["report"] for label, clip in clips.items()},
        "restCompatibility": compatibility,
        "matrixConventionFixture": validate_matrix_convention(),
        "retargetMethod": "Each target pose matrix is targetRest @ inverse(donorRest) @ donorPose; the resulting target matrix_basis channels are baked after whole-skeleton root planting.",
        "hierarchy": hierarchy_target,
        **action_report,
        "staticRightGrip": grip_report,
        "katana": katana_report,
        "previews": previews,
        "textures": textures,
        "files": {"blend": str(blend), "blendSha256": sha256(blend), **exports},
        "fbxReimport": reimport,
        "notes": [
            "No old fused body geometry or weights are copied into the new source.",
            "Charged-slash/custom spin motion is intentionally excluded; SpinCharge/SpinRelease reuse accepted magic/sword poses.",
            "All evidence renders explicitly set and render the recorded action frame.",
        ],
    }
    manifest = args.out_dir / "manifest.json"
    manifest.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("WP16_COMPLETE=" + json.dumps({"manifest": str(manifest), "sourceSha256": report["source"]["sha256"],
                                           "actions": report["actions"], "fbxReimport": reimport["actionCount"]}))


if __name__ == "__main__":
    main()
