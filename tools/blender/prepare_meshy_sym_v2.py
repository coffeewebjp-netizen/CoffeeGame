"""Prepare the symmetric-T-pose Meshy FBX (merged clips) for the Trial slot."""

from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

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

    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    idle = bpy.data.actions.new(name="Idle")
    idle.use_fake_user = True
    assign_action(arm, idle)
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


def render_previews(arm):
    add_studio()
    three_q = (2.15, -2.75, 1.15)
    assign_action(arm, bpy.data.actions["Idle"])
    render_still(PREVIEWS / "idle.jpg", three_q, 1)
    for name in ("Walk", "Run", "Jump", "Sword", "MagicCharge"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        assign_action(arm, action)
        mid = int((action.frame_range[0] + action.frame_range[1]) * 0.5)
        render_still(PREVIEWS / f"{name.lower()}-mid.jpg", three_q, mid)


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
        "notes": "Symmetric T-pose Meshy pack. Sheathed sword left fused. No RightHand katana parent. Actions rebaked with Blender 4.5 slots.",
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
    prepare_actions(arm)
    make_locomotion_inplace()
    verify_baked_actions()
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_previews(arm)
    export_fbx()
    copy_to_unity()
    write_manifest(arm, mesh)
    verify_export(arm.name)


if __name__ == "__main__":
    main()
