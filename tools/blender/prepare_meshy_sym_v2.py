"""Prepare the symmetric-T-pose Meshy FBX (merged clips) for the Trial slot."""

from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "art" / "3d" / "trials" / "meshy-girl1"
SRC_DIR = PACK / "drop" / "sym-v2" / "Meshy_AI_Azure_Blade_Maiden_biped"
SRC_FBX = SRC_DIR / "Meshy_AI_Azure_Blade_Maiden_biped_Meshy_AI_Meshy_Merged_Animations.fbx"
BLEND = PACK / "source" / "girl1-meshy-sym-v2.blend"
EXPORT_FBX = PACK / "export" / "girl1-meshy-sym-v2.fbx"
UNITY_FBX = (
    ROOT
    / "unity"
    / "CoffeeGame"
    / "Assets"
    / "CoffeeGame"
    / "Resources"
    / "Models"
    / "Hero"
    / "trial-anime-girl.fbx"
)
UNITY_BACKUP = PACK / "archive" / "held-sword-trial-anime-girl.fbx"
PREVIEWS = PACK / "previews" / "sym-v2"
MANIFEST = PACK / "manifests" / "girl1-meshy-sym-v2.json"
TARGET_HEIGHT = 1.62
TEX = {
    "base": SRC_DIR / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0.png",
    "metallic": SRC_DIR / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_metallic.png",
    "normal": SRC_DIR / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_normal.png",
    "roughness": SRC_DIR / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_roughness.png",
}
ACTION_RENAME = {
    "Armature|Walking": "Walk",
    "Armature|Running": "Run",
    "Armature|Regular_Jump": "Jump",
    "Armature|Attack": "Sword",
    "Armature|mage_soell_cast_7": "MagicCharge",
}
# Character faces -Y with +Z up. World +Y drop takes T-pose arms to the sides;
# world +X pitch leans the torso toward -Y (forward).
IDLE_ARM_DROP = {
    "LeftShoulder": (Vector((0.0, 1.0, 0.0)), math.radians(12.0)),
    "RightShoulder": (Vector((0.0, 1.0, 0.0)), math.radians(-12.0)),
    "LeftArm": (Vector((0.0, 1.0, 0.0)), math.radians(78.0)),
    "RightArm": (Vector((0.0, 1.0, 0.0)), math.radians(-78.0)),
}
IDLE_ELBOW_BEND = {
    "LeftForeArm": (Vector((1.0, 0.0, 0.0)), math.radians(-18.0)),
    "RightForeArm": (Vector((1.0, 0.0, 0.0)), math.radians(-18.0)),
}
# Extra torso pitch toward -Y. Neck/head counter so the face stays forward
# (the reference run looks ahead, not at the ground).
RUN_FORWARD_LEAN = {
    "Hips": math.radians(22.0),
    "Spine02": math.radians(6.0),
    "Spine01": math.radians(5.0),
    "Spine": math.radians(4.0),
}
RUN_HEAD_LOOK_FORWARD = {
    "neck": math.radians(-28.0),
    "Head": math.radians(-14.0),
}
RUN_LIMB_SCALE = {
    "LeftShoulder": 1.2,
    "RightShoulder": 1.2,
    "LeftArm": 1.45,
    "RightArm": 1.45,
    "LeftForeArm": 1.25,
    "RightForeArm": 1.25,
    "LeftUpLeg": 1.4,
    "RightUpLeg": 1.4,
    "LeftLeg": 1.3,
    "RightLeg": 1.3,
}
SWORD_SLASH_ARM = ("RightShoulder", "RightArm", "RightForeArm", "RightHand")


def reset() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    scene.render.fps = 30
    world = bpy.data.worlds.new("MeshySymWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.14, 0.15, 0.17, 1.0)
    bg.inputs[1].default_value = 1.0


def world_bbox_objects(objects):
    pts = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            pts.append(obj.matrix_world @ Vector(corner))
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def import_fbx():
    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
    arm.name = "MeshyGirl1Rig"
    mesh.name = "MeshyGirl1"
    mesh.data.name = "MeshyGirl1"
    bpy.context.view_layer.update()
    return arm, mesh


def ground_and_scale(arm, meshes):
    bpy.context.view_layer.update()
    lo, hi = world_bbox_objects(meshes)
    height = max(0.001, hi.z - lo.z)
    scale = TARGET_HEIGHT / height
    arm.scale = (arm.scale.x * scale, arm.scale.y * scale, arm.scale.z * scale)
    bpy.context.view_layer.update()
    lo, hi = world_bbox_objects(meshes)
    arm.location.x -= (lo.x + hi.x) * 0.5
    arm.location.y -= (lo.y + hi.y) * 0.5
    arm.location.z -= lo.z
    bpy.context.view_layer.update()


def assign_textures(mesh):
    if not TEX["base"].exists():
        return
    mat = mesh.active_material or bpy.data.materials.new("MeshyGirl1_PBR")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(TEX["base"]), check_existing=True)
    links.new(tex.outputs["Color"], principled.inputs["Base Color"])
    if mesh.data.materials:
        mesh.data.materials[0] = mat
    else:
        mesh.data.materials.append(mat)


def assign_action(arm, action):
    """Bind a layered Blender 4.4+ action so it actually drives the armature."""
    if arm.animation_data is None:
        arm.animation_data_create()
    ad = arm.animation_data
    ad.action = action
    if not hasattr(ad, "action_slot") or not hasattr(action, "slots"):
        bpy.context.view_layer.update()
        return
    slot = None
    for candidate in action.slots:
        ident = getattr(candidate, "identifier", "") or ""
        name = getattr(candidate, "name_display", None) or getattr(candidate, "name", "") or ""
        if arm.name in ident or arm.name in name:
            slot = candidate
            break
    if slot is None and len(action.slots) > 0:
        slot = action.slots[0]
    if slot is None:
        slot = action.slots.new("OBJECT", arm.name)
    ad.action_slot = slot
    bpy.context.view_layer.update()


def snapshot_pose(arm):
    return {
        bone.name: (bone.location.copy(), bone.rotation_quaternion.copy(), bone.scale.copy())
        for bone in arm.pose.bones
    }


def apply_pose(arm, pose):
    for name, (location, quaternion, scale) in pose.items():
        bone = arm.pose.bones.get(name)
        if bone is None:
            continue
        bone.location = location
        bone.rotation_quaternion = quaternion
        bone.scale = scale


def keyframe_pose(arm, frame):
    for bone in arm.pose.bones:
        bone.keyframe_insert("location", frame=frame)
        bone.keyframe_insert("rotation_quaternion", frame=frame)
        bone.keyframe_insert("scale", frame=frame)


def rotate_bone_world(arm, bone_name, world_axis, angle_rad):
    bone = arm.pose.bones.get(bone_name)
    if bone is None or abs(angle_rad) < 1e-6:
        return
    bpy.context.view_layer.update()
    axis_local = bone.matrix.to_3x3().inverted() @ world_axis
    if axis_local.length < 0.001:
        return
    extra = Quaternion(axis_local.normalized(), angle_rad)
    bone.rotation_quaternion = extra @ bone.rotation_quaternion


def farthest_pair(points):
    best_dist = -1.0
    best = (points[0], points[1] if len(points) > 1 else points[0])
    for i in range(len(points)):
        for j in range(i + 1, len(points)):
            dist = (points[i] - points[j]).length
            if dist > best_dist:
                best_dist = dist
                best = (points[i], points[j])
    return best


def separate_sheathed_sword(mesh, arm) -> bpy.types.Object:
    bpy.context.view_layer.update()
    hips = arm.matrix_world @ arm.data.bones["Hips"].head_local
    coords = [mesh.matrix_world @ vert.co for vert in mesh.data.vertices]
    # Sheath hangs down-left-back from the waist. Ignore floor/geta.
    candidates = [
        i
        for i, pos in enumerate(coords)
        if pos.x < hips.x - 0.07 and 0.04 < pos.z < hips.z - 0.04
    ]
    if not candidates:
        raise RuntimeError("No sheathed-sword candidate verts")
    tip_index = max(
        candidates,
        key=lambda i: (hips.xy - coords[i].xy).length + (hips.z - coords[i].z),
    )
    tip = coords[tip_index]
    grip = hips + Vector((-0.10, 0.04, 0.04))
    axis = tip - grip
    axis_len = axis.length
    if axis_len < 0.25:
        raise RuntimeError(f"Sheathed sword axis too short ({axis_len:.3f}m)")

    adjacency = [[] for _ in range(len(mesh.data.vertices))]
    for edge in mesh.data.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)

    chosen = []
    seen = {tip_index}
    queue = [tip_index]
    radius = 0.04
    while queue:
        index = queue.pop()
        pos = coords[index]
        t = max(0.0, min(1.0, (pos - grip).dot(axis) / (axis_len * axis_len)))
        proj = grip + axis * t
        if (pos - proj).length > radius:
            continue
        chosen.append(index)
        for neighbor in adjacency[index]:
            if neighbor in seen:
                continue
            seen.add(neighbor)
            npos = coords[neighbor]
            nt = max(0.0, min(1.0, (npos - grip).dot(axis) / (axis_len * axis_len)))
            nproj = grip + axis * nt
            if (npos - nproj).length <= radius:
                queue.append(neighbor)

    stick = []
    for index in chosen:
        pos = coords[index]
        t = max(0.0, min(1.0, (pos - grip).dot(axis) / (axis_len * axis_len)))
        proj = grip + axis * t
        if (pos - proj).length <= 0.028:
            stick.append(index)
    chosen = stick
    if len(chosen) < 60:
        raise RuntimeError(f"Sheathed sword island too small ({len(chosen)} verts)")
    pts = [coords[i] for i in chosen]
    size = (
        max(p.x for p in pts) - min(p.x for p in pts),
        max(p.y for p in pts) - min(p.y for p in pts),
        max(p.z for p in pts) - min(p.z for p in pts),
    )
    ordered = tuple(sorted(size))
    if ordered[1] > 0.11 or ordered[2] < 0.45:
        raise RuntimeError(f"Sheathed sword island is not a stick {tuple(round(v, 3) for v in ordered)}")
    print(json.dumps({"sword_verts": len(chosen), "axis_m": round(axis_len, 3), "bbox": [round(v, 3) for v in ordered]}))

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for index in chosen:
        mesh.data.vertices[index].select = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")
    sword = next(
        obj
        for obj in bpy.data.objects
        if obj.type == "MESH" and obj != mesh and obj.name.startswith("MeshyGirl1")
    )
    sword.name = "MeshyGirl1SwordSheath"
    sword.data.name = "MeshyGirl1SwordSheath"
    for mod in list(sword.modifiers):
        sword.modifiers.remove(mod)
    sword.vertex_groups.clear()
    return sword


def parent_to_bone(obj, arm, bone_name):
    obj.parent = None
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    arm.data.bones.active = arm.data.bones[bone_name]
    bpy.ops.object.parent_set(type="BONE_RELATIVE")
    bpy.context.view_layer.update()
    if obj.parent_bone != bone_name:
        raise RuntimeError(f"{obj.name} parent bone is {obj.parent_bone!r}")


def place_drawn_sword(sword, arm):
    bpy.context.view_layer.update()
    points = [sword.matrix_world @ vert.co.copy() for vert in sword.data.vertices]
    end_a, end_b = farthest_pair(points)
    handle, tip = (end_a, end_b) if end_a.z >= end_b.z else (end_b, end_a)
    blade = tip - handle
    if blade.length < 0.2:
        raise RuntimeError(f"Drawn sword too short ({blade.length:.3f}m)")
    grip = handle.lerp(tip, 0.12)
    blade.normalize()
    hand = arm.pose.bones["RightHand"]
    hand_mat = arm.matrix_world @ hand.matrix
    finger = (hand_mat.to_3x3() @ Vector((0.0, 1.0, 0.0))).normalized()
    if finger.length < 0.001:
        finger = Vector((0.0, -1.0, 0.0))
    rot = blade.rotation_difference(finger)
    sword.matrix_world = (
        Matrix.Translation(hand_mat.translation)
        @ rot.to_matrix().to_4x4()
        @ Matrix.Translation(-grip)
        @ sword.matrix_world
    )
    bpy.context.view_layer.update()


def setup_swords(mesh, arm):
    sheath = separate_sheathed_sword(mesh, arm)
    drawn = sheath.copy()
    drawn.data = sheath.data.copy()
    drawn.name = "MeshyGirl1SwordDrawn"
    drawn.data.name = "MeshyGirl1SwordDrawn"
    drawn.parent = None
    bpy.context.collection.objects.link(drawn)
    parent_to_bone(sheath, arm, "Hips")
    place_drawn_sword(drawn, arm)
    parent_to_bone(drawn, arm, "RightHand")
    drawn.hide_render = True
    drawn.hide_viewport = True
    return sheath, drawn


def set_sword_drawn(sheath, drawn, drawn_visible: bool):
    if sheath is not None:
        sheath.hide_render = drawn_visible
        sheath.hide_viewport = drawn_visible
    if drawn is not None:
        drawn.hide_render = not drawn_visible
        drawn.hide_viewport = not drawn_visible


def slash_arm_angles(t: float):
    """t in 0..1. Positive X lifts the hanging arm backward; negative swings forward."""
    if t < 0.22:
        u = t / 0.22
        windup = 55.0 * u
        swing = 0.0
    elif t < 0.52:
        u = (t - 0.22) / 0.30
        u = u * u
        windup = 55.0
        swing = 130.0 * u
    else:
        u = (t - 0.52) / 0.48
        windup = 55.0
        swing = 130.0 + 15.0 * u
    x_deg = windup - swing
    return {
        "RightShoulder": math.radians(x_deg * 0.28),
        "RightArm": math.radians(x_deg),
        "RightForeArm": math.radians(x_deg * 0.18),
        "RightHand": math.radians(x_deg * 0.08),
    }


def rebuild_sword_slash(arm):
    action = bpy.data.actions.get("Sword")
    if action is None:
        return
    assign_action(arm, action)
    scene = bpy.context.scene
    start = int(round(action.frame_range[0]))
    end = int(round(action.frame_range[1]))
    span = max(1, end - start)
    frames = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        t = (frame - start) / span
        for name, angle in slash_arm_angles(t).items():
            rotate_bone_world(arm, name, Vector((1.0, 0.0, 0.0)), angle)
        bpy.context.view_layer.update()
        frames.append(snapshot_pose(arm))
    slashed = bpy.data.actions.new(name="Sword__slash")
    slashed.use_fake_user = True
    assign_action(arm, slashed)
    for index, frame in enumerate(range(start, end + 1)):
        apply_pose(arm, frames[index])
        keyframe_pose(arm, frame)
    bpy.data.actions.remove(action)
    slashed.name = "Sword"


def pose_idle_standing(arm):
    """Lower T-pose arms into a standing rest. Sword stays sheathed at the hip."""
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    for name, (axis, angle) in IDLE_ARM_DROP.items():
        rotate_bone_world(arm, name, axis, angle)
    bpy.context.view_layer.update()
    for name, (axis, angle) in IDLE_ELBOW_BEND.items():
        rotate_bone_world(arm, name, axis, angle)
    bpy.context.view_layer.update()


def mean_quaternion(quats):
    acc = Quaternion((0.0, 0.0, 0.0, 0.0))
    for quat in quats:
        sample = quat.copy()
        if acc.dot(sample) < 0.0:
            sample.negate()
        acc.w += sample.w
        acc.x += sample.x
        acc.y += sample.y
        acc.z += sample.z
    if acc.magnitude < 1e-8:
        return Quaternion((1.0, 0.0, 0.0, 0.0))
    acc.normalize()
    return acc


def exaggerate_from_mean(bone, mean_quat, factor: float):
    if bone is None or factor <= 1.001:
        return
    current = bone.rotation_quaternion.copy()
    delta = mean_quat.rotation_difference(current)
    axis, angle = delta.to_axis_angle()
    if abs(angle) < 1e-5:
        return
    bone.rotation_quaternion = mean_quat @ Quaternion(axis, angle * factor)


def lean_run_forward(arm):
    """Rebuild Run: extra torso lean, forward gaze, bigger arm/leg reach."""
    action = bpy.data.actions.get("Run")
    if action is None:
        return
    assign_action(arm, action)
    scene = bpy.context.scene
    start = int(round(action.frame_range[0]))
    end = int(round(action.frame_range[1]))
    originals = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        originals.append(snapshot_pose(arm))
    limb_means = {}
    for name in RUN_LIMB_SCALE:
        samples = [pose[name][1] for pose in originals if name in pose]
        if samples:
            limb_means[name] = mean_quaternion(samples)
    frames = []
    for pose in originals:
        apply_pose(arm, pose)
        bpy.context.view_layer.update()
        for name, factor in RUN_LIMB_SCALE.items():
            exaggerate_from_mean(arm.pose.bones.get(name), limb_means[name], factor)
        bpy.context.view_layer.update()
        for name, angle in RUN_FORWARD_LEAN.items():
            rotate_bone_world(arm, name, Vector((1.0, 0.0, 0.0)), angle)
        bpy.context.view_layer.update()
        for name, angle in RUN_HEAD_LOOK_FORWARD.items():
            rotate_bone_world(arm, name, Vector((1.0, 0.0, 0.0)), angle)
        bpy.context.view_layer.update()
        frames.append(snapshot_pose(arm))
    leaned = bpy.data.actions.new(name="Run__lean")
    leaned.use_fake_user = True
    assign_action(arm, leaned)
    for index, frame in enumerate(range(start, end + 1)):
        apply_pose(arm, frames[index])
        keyframe_pose(arm, frame)
    bpy.data.actions.remove(action)
    leaned.name = "Run"


def bake_named_action(arm, source, dest_name):
    assign_action(arm, source)
    scene = bpy.context.scene
    start = int(round(source.frame_range[0]))
    end = int(round(source.frame_range[1]))
    frames = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        frames.append(snapshot_pose(arm))
    baked = bpy.data.actions.new(name=dest_name)
    baked.use_fake_user = True
    assign_action(arm, baked)
    for index, frame in enumerate(range(start, end + 1)):
        apply_pose(arm, frames[index])
        keyframe_pose(arm, frame)
    return baked


def copy_action(action, dest_name):
    copied = action.copy()
    copied.name = dest_name
    copied.use_fake_user = True
    return copied


def prepare_actions(arm):
    missing = [old for old in ACTION_RENAME if old not in bpy.data.actions]
    if missing:
        raise RuntimeError("Missing Meshy clips: " + ", ".join(missing))

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"

    baked = {}
    for old, new in ACTION_RENAME.items():
        baked[new] = bake_named_action(arm, bpy.data.actions[old], new + "__baked")

    idle = bpy.data.actions.new(name="Idle")
    idle.use_fake_user = True
    assign_action(arm, idle)
    pose_idle_standing(arm)
    keyframe_pose(arm, 1)
    keyframe_pose(arm, 10)
    bpy.ops.object.mode_set(mode="OBJECT")

    keep_source = {action for action in baked.values()}
    keep_source.add(idle)
    for action in list(bpy.data.actions):
        if action not in keep_source:
            bpy.data.actions.remove(action)
    for name, action in baked.items():
        action.name = name

    copy_action(bpy.data.actions["MagicCharge"], "MagicRelease")
    copy_action(bpy.data.actions["Jump"], "Fall")
    copy_action(bpy.data.actions["Jump"], "Land")

    walk = bpy.data.actions["Walk"]
    assign_action(arm, walk)
    scene = bpy.context.scene
    scene.frame_start = int(walk.frame_range[0])
    scene.frame_end = int(walk.frame_range[1])


def make_locomotion_inplace():
    for name in ("Walk", "Run"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        hip_curves = [
            fcurve
            for fcurve in action.fcurves
            if fcurve.data_path.endswith("location") and "Hips" in fcurve.data_path
        ]
        for fcurve in hip_curves:
            if fcurve.array_index in (0, 1) and fcurve.keyframe_points:
                rest = fcurve.keyframe_points[0].co[1]
                for key in fcurve.keyframe_points:
                    key.co[1] = rest
                fcurve.update()


def add_studio():
    scene = bpy.context.scene
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.68, 0.74, 1.0)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    ground_mat = bpy.data.materials.new("TrialGround")
    ground_mat.use_nodes = True
    bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, -0.002))
    ground = bpy.context.object
    ground.name = "PreviewOnly_Ground"
    ground.data.materials.append(ground_mat)


def point_camera(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_still(path: Path, location, frame=1):
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    cam_data = bpy.data.cameras.new("TrialCam")
    cam_data.lens = 40
    cam = bpy.data.objects.new("TrialCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = location
    point_camera(cam, (0.0, 0.0, 0.82))
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def render_previews(arm, sheath, drawn):
    add_studio()
    three_q = (2.15, -2.75, 1.15)
    set_sword_drawn(sheath, drawn, False)
    assign_action(arm, bpy.data.actions["Idle"])
    render_still(PREVIEWS / "idle.jpg", three_q, 1)
    render_still(PREVIEWS / "idle-front.jpg", (0.0, -3.35, 1.05), 1)
    assign_action(arm, bpy.data.actions["Run"])
    run = bpy.data.actions["Run"]
    run_mid = int((run.frame_range[0] + run.frame_range[1]) * 0.5)
    render_still(PREVIEWS / "run-side.jpg", (3.35, 0.0, 1.05), run_mid)
    for name in ("Walk", "Run", "Jump", "MagicCharge"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        assign_action(arm, action)
        mid = int((action.frame_range[0] + action.frame_range[1]) * 0.5)
        render_still(PREVIEWS / f"{name.lower()}-mid.jpg", three_q, mid)
    set_sword_drawn(sheath, drawn, True)
    sword = bpy.data.actions.get("Sword")
    if sword is not None:
        assign_action(arm, sword)
        mid = int((sword.frame_range[0] + sword.frame_range[1]) * 0.5)
        render_still(PREVIEWS / "sword-mid.jpg", three_q, mid)
        render_still(PREVIEWS / "sword-side.jpg", (3.35, 0.0, 1.05), mid)
    set_sword_drawn(sheath, drawn, False)


def export_fbx():
    EXPORT_FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"} and not obj.name.startswith("PreviewOnly"):
            obj.select_set(True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT_FBX),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        path_mode="COPY",
        embed_textures=False,
    )


def copy_to_unity():
    UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
    if UNITY_FBX.exists() and not UNITY_BACKUP.exists():
        UNITY_BACKUP.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(UNITY_FBX, UNITY_BACKUP)
    shutil.copy2(EXPORT_FBX, UNITY_FBX)
    dest_dir = UNITY_FBX.parent
    for src in TEX.values():
        if src.exists():
            shutil.copy2(src, dest_dir / src.name)


def action_has_motion(action) -> bool:
    for fcurve in action.fcurves:
        values = [key.co[1] for key in fcurve.keyframe_points]
        if values and (max(values) - min(values)) > 1e-4:
            return True
    return False


def write_manifest(arm, mesh):
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    data = {
        "schemaVersion": 1,
        "asset": "trial-meshy-girl1-sym-v2",
        "taskId": "ORC-20260823-004",
        "source": str(SRC_FBX.relative_to(ROOT)).replace("\\", "/"),
        "actions": sorted(action.name for action in bpy.data.actions),
        "triangles": sum(len(p.vertices) - 2 for p in mesh.data.polygons),
        "notes": "Idle arms down. Run: bigger limb swing, face looks forward. Sword still sheathed.",
    }
    MANIFEST.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(data, indent=2))


def verify_baked_actions():
    required = ("Idle", "Walk", "Run", "Jump", "Sword", "MagicCharge")
    missing = [name for name in required if name not in bpy.data.actions]
    if missing:
        raise RuntimeError("Baked actions missing: " + ", ".join(missing))
    for name in ("Walk", "Run", "Jump", "Sword", "MagicCharge"):
        if not action_has_motion(bpy.data.actions[name]):
            raise RuntimeError(f"{name} baked without pose motion")


def verify_export(arm_name: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(EXPORT_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    walk = next((action for action in bpy.data.actions if action.name.endswith("Walk")), None)
    if walk is None:
        raise RuntimeError("Exported FBX has no Walk clip")
    if not action_has_motion(walk):
        raise RuntimeError("Exported Walk clip has no pose motion")
    assign_action(arm, walk)
    scene = bpy.context.scene
    bones = ("LeftUpLeg", "RightUpLeg", "LeftArm", "RightArm")
    scene.frame_set(int(walk.frame_range[0]))
    bpy.context.view_layer.update()
    start = {name: tuple(arm.pose.bones[name].rotation_quaternion) for name in bones if name in arm.pose.bones}
    scene.frame_set(int((walk.frame_range[0] + walk.frame_range[1]) * 0.5))
    bpy.context.view_layer.update()
    mid = {name: tuple(arm.pose.bones[name].rotation_quaternion) for name in bones if name in arm.pose.bones}
    if start == mid:
        raise RuntimeError("Exported Walk pose does not change at mid-frame")
    print(json.dumps({"export_walk_ok": True, "armature": arm.name, "src_armature": arm_name, "walk": walk.name}))


def main():
    if not SRC_FBX.exists():
        raise FileNotFoundError(SRC_FBX)
    reset()
    arm, mesh = import_fbx()
    ground_and_scale(arm, [mesh])
    assign_textures(mesh)
    sheath, drawn = None, None
    prepare_actions(arm)
    make_locomotion_inplace()
    lean_run_forward(arm)
    verify_baked_actions()
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_previews(arm, sheath, drawn)
    export_fbx()
    copy_to_unity()
    write_manifest(arm, mesh)
    verify_export(arm.name)


if __name__ == "__main__":
    main()
