"""Merge the held-sword Ronin attack pack into the Trial slot."""

from __future__ import annotations

import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "art" / "3d" / "trials" / "meshy-girl1"
SRC_DIR = PACK / "drop" / "ronin" / "Meshy_AI_Blue_Haired_Ronin_biped"
BLEND = PACK / "source" / "girl1-meshy-ronin.blend"
EXPORT_FBX = PACK / "export" / "girl1-meshy-ronin.fbx"
PREVIEWS = PACK / "previews" / "ronin"
MANIFEST = PACK / "manifests" / "girl1-meshy-ronin.json"
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
UNITY_ATTACK_FBX = (
    ROOT
    / "unity"
    / "CoffeeGame"
    / "Assets"
    / "CoffeeGame"
    / "Resources"
    / "Models"
    / "Hero"
    / "trial-anime-girl-attack.fbx"
)
UNITY_BACKUP = PACK / "archive" / "emptyhand-sym-v2-trial-anime-girl.fbx"
# Gameplay sword window is 0.34s. Keep the committed cut, not the wind-up.
SLASH_WINDOWS = {
    "Sword": (31, 41),
    "AirSlash": (18, 28),
}
TEX = {
    "base": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_texture_0.png",
    "metallic": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_metallic.png",
    "normal": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_normal.png",
    "roughness": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_roughness.png",
}
CLIP_FBX = {
    "Walk": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Walking_withSkin.fbx",
    "Run": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Running_withSkin.fbx",
    "Sword": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Attack_withSkin.fbx",
    "AirSlash": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Double_Combo_Attack_withSkin.fbx",
    "Jump": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Basic_Jump_withSkin.fbx",
    "SpinRelease": SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Sword_Judgment_withSkin.fbx",
}
BASE_FBX = SRC_DIR / "Meshy_AI_Blue_Haired_Ronin_biped_Character_output.fbx"


def load_sym():
    path = Path(__file__).with_name("prepare_meshy_sym_v2.py")
    spec = importlib.util.spec_from_file_location("prepare_meshy_sym_v2", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


P = load_sym()


def import_base():
    P.reset()
    bpy.ops.import_scene.fbx(filepath=str(BASE_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
    arm.name = "MeshyGirl1Rig"
    mesh.name = "MeshyGirl1"
    mesh.data.name = "MeshyGirl1"
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    bpy.ops.object.mode_set(mode="OBJECT")
    P.ground_and_scale(arm, [mesh])
    match_head_height(arm)
    P.TEX.update(TEX)
    P.assign_textures(mesh)
    cleanup_mesh(mesh)
    return arm, mesh


def match_head_height(arm):
    """Match the sheathed maiden's idle head height so slash swaps do not shrink."""
    bpy.context.view_layer.update()
    head = bone_world(arm, "Head")
    left = bone_world(arm, "LeftFoot")
    right = bone_world(arm, "RightFoot")
    if head is None or left is None or right is None or head.z < 0.2:
        return
    factor = 1.47 / head.z
    arm.scale *= factor
    bpy.context.view_layer.update()
    left = bone_world(arm, "LeftFoot")
    right = bone_world(arm, "RightFoot")
    foot = min(left.z, right.z)
    arm.location.z -= foot - 0.139
    bpy.context.view_layer.update()
    print("matched head height", round(bone_world(arm, "Head").z, 3))


def cleanup_mesh(obj):
    """Close small remesh holes. Do not delete interior faces on this thin clothing mesh."""
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.mesh.delete_loose()
    bpy.ops.mesh.fill_holes(sides=8)
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.update()
    print("cleaned mesh", obj.name, "verts", len(obj.data.vertices), "polys", len(obj.data.polygons))


def steal_clip(dest_arm, fbx_path: Path, dest_name: str):
    before_objects = set(bpy.data.objects.keys())
    before_actions = set(bpy.data.actions.keys())
    bpy.ops.import_scene.fbx(filepath=str(fbx_path), automatic_bone_orientation=True)
    src_arm = next(
        obj
        for obj in bpy.data.objects
        if obj.name not in before_objects and obj.type == "ARMATURE"
    )
    src_action = next(action for action in bpy.data.actions if action.name not in before_actions)
    P.assign_action(src_arm, src_action)
    scene = bpy.context.scene
    start = int(round(src_action.frame_range[0]))
    end = int(round(src_action.frame_range[1]))
    poses = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses.append(P.snapshot_pose(src_arm))
    baked = bpy.data.actions.new(name=dest_name + "__xfer")
    baked.use_fake_user = True
    P.assign_action(dest_arm, baked)
    for index, frame in enumerate(range(start, end + 1)):
        P.apply_pose(dest_arm, poses[index])
        P.keyframe_pose(dest_arm, frame)
    baked.name = dest_name
    for obj in list(bpy.data.objects):
        if obj.name in before_objects:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        if action.name not in before_actions and action != baked:
            bpy.data.actions.remove(action)
    return baked


def make_idle(arm):
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    idle = bpy.data.actions.new(name="Idle")
    idle.use_fake_user = True
    P.assign_action(arm, idle)
    P.keyframe_pose(arm, 1)
    P.keyframe_pose(arm, 10)
    bpy.ops.object.mode_set(mode="OBJECT")


def copy_named(src_name, dest_name):
    copied = bpy.data.actions[src_name].copy()
    copied.name = dest_name
    copied.use_fake_user = True


def rebuild_action(arm, name, poses):
    old = bpy.data.actions.get(name)
    rebuilt = bpy.data.actions.new(name + "__slash")
    rebuilt.use_fake_user = True
    P.assign_action(arm, rebuilt)
    for index, pose in enumerate(poses):
        P.apply_pose(arm, pose)
        P.keyframe_pose(arm, 1 + index)
    if old is not None:
        bpy.data.actions.remove(old)
    rebuilt.name = name
    P.assign_action(arm, rebuilt)


def extract_slash_windows(arm):
    scene = bpy.context.scene
    for name, (src_start, src_end) in SLASH_WINDOWS.items():
        action = bpy.data.actions.get(name)
        if action is None:
            raise RuntimeError(f"missing {name}")
        P.assign_action(arm, action)
        poses = []
        for src in range(src_start, src_end + 1):
            scene.frame_set(src)
            bpy.context.view_layer.update()
            poses.append(P.snapshot_pose(arm))
        rebuild_action(arm, name, poses)
        print("slash window", name, src_start, src_end, "frames", len(poses))


def plant_combat_hips(arm):
    """Keep combat clips on the actor. AirSlash otherwise starts a metre behind Idle."""
    idle = bpy.data.actions.get("Idle")
    if idle is None or "Hips" not in arm.pose.bones:
        return
    scene = bpy.context.scene
    P.assign_action(arm, idle)
    scene.frame_set(int(round(idle.frame_range[0])))
    bpy.context.view_layer.update()
    rest = arm.pose.bones["Hips"].location.copy()
    for name in ("Sword", "AirSlash", "SpinRelease"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        P.assign_action(arm, action)
        poses = []
        start = int(round(action.frame_range[0]))
        end = int(round(action.frame_range[1]))
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            hips = arm.pose.bones["Hips"]
            hips.location.x = rest.x
            hips.location.y = rest.y
            poses.append(P.snapshot_pose(arm))
        rebuild_action(arm, name, poses)
        print("planted hips", name, "rest", [round(rest.x, 3), round(rest.y, 3), round(rest.z, 3)])


def bone_world(arm, name):
    bone = arm.pose.bones.get(name)
    if bone is None:
        return None
    return arm.matrix_world @ bone.matrix @ Vector((0.0, 0.0, 0.0))


def shift_hips_world(arm, delta):
    hips = arm.pose.bones.get("Hips")
    if hips is None or delta.length < 1e-5:
        return
    bpy.context.view_layer.update()
    world = arm.matrix_world @ hips.matrix
    world.translation += delta
    if hips.parent:
        parent = arm.matrix_world @ hips.parent.matrix
        hips.matrix = parent.inverted() @ world
    else:
        hips.matrix = arm.matrix_world.inverted() @ world


def plant_combat_feet(arm):
    """Lower combat poses so the lowest foot matches Idle geta height."""
    idle = bpy.data.actions.get("Idle")
    if idle is None:
        return
    scene = bpy.context.scene
    P.assign_action(arm, idle)
    scene.frame_set(int(round(idle.frame_range[0])))
    bpy.context.view_layer.update()
    rest_left = bone_world(arm, "LeftFoot")
    rest_right = bone_world(arm, "RightFoot")
    if rest_left is None or rest_right is None:
        return
    rest_foot = min(rest_left.z, rest_right.z)
    for name in ("Sword", "AirSlash", "Plunge", "SpinRelease"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        P.assign_action(arm, action)
        poses = []
        start = int(round(action.frame_range[0]))
        end = int(round(action.frame_range[1]))
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            left = bone_world(arm, "LeftFoot")
            right = bone_world(arm, "RightFoot")
            current = min(left.z, right.z)
            shift_hips_world(arm, Vector((0.0, 0.0, rest_foot - current)))
            bpy.context.view_layer.update()
            poses.append(P.snapshot_pose(arm))
        rebuild_action(arm, name, poses)
        print("planted feet", name, "restFoot", round(rest_foot, 3))


def aim_bone_towards(arm, bone_name, world_dir):
    bone = arm.pose.bones.get(bone_name)
    if bone is None:
        return
    bpy.context.view_layer.update()
    current = ((arm.matrix_world @ bone.matrix).to_3x3() @ Vector((0.0, 1.0, 0.0)))
    if current.length < 0.001:
        return
    rot = current.normalized().rotation_difference(world_dir.normalized())
    if rot.angle < 1e-4:
        return
    P.rotate_bone_world(arm, bone_name, rot.axis, rot.angle)


def make_plunge(arm):
    idle = bpy.data.actions["Idle"]
    P.assign_action(arm, idle)
    scene = bpy.context.scene
    scene.frame_set(int(round(idle.frame_range[0])))
    bpy.context.view_layer.update()
    P.apply_pose(arm, P.snapshot_pose(arm))
    down = Vector((0.0, 0.0, -1.0))
    aim_bone_towards(arm, "RightShoulder", down)
    aim_bone_towards(arm, "RightArm", down)
    aim_bone_towards(arm, "RightForeArm", down)
    aim_bone_towards(arm, "RightHand", down)
    aim_bone_towards(arm, "LeftArm", Vector((0.15, 0.0, -1.0)))
    P.rotate_bone_world(arm, "Spine", Vector((1.0, 0.0, 0.0)), math.radians(12.0))
    P.rotate_bone_world(arm, "Head", Vector((1.0, 0.0, 0.0)), math.radians(18.0))
    hips = arm.pose.bones.get("Hips")
    if hips is not None:
        hips.location.x = 0.0
        hips.location.y = 0.0
    pose = P.snapshot_pose(arm)
    rebuild_action(arm, "Plunge", [pose for _ in range(10)])
    print("plunge frames", 10)


def export_and_copy(copy_to_locomotion=False, copy_to_attack=True):
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
    if copy_to_locomotion:
        UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
        if UNITY_FBX.exists() and not UNITY_BACKUP.exists():
            UNITY_BACKUP.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(UNITY_FBX, UNITY_BACKUP)
        shutil.copy2(EXPORT_FBX, UNITY_FBX)
    if copy_to_attack:
        UNITY_ATTACK_FBX.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(EXPORT_FBX, UNITY_ATTACK_FBX)


def copy_textures_to_unity():
    dest_dir = UNITY_ATTACK_FBX.parent
    mapping = {
        "texture_0.png": "Meshy_AI_Blue_Haired_Ronin_biped_texture_0.png",
        "texture_0_metallic.png": "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_metallic.png",
        "texture_0_normal.png": "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_normal.png",
        "texture_0_roughness.png": "Meshy_AI_Blue_Haired_Ronin_biped_texture_0_roughness.png",
    }
    for src in TEX.values():
        suffix = src.name.split("biped_")[-1]
        dest_name = mapping.get(suffix, src.name)
        dest = dest_dir / dest_name
        if dest.exists():
            continue
        shutil.copy2(src, dest)


def render_previews(arm):
    P.PREVIEWS = PREVIEWS
    P.add_studio()
    three_q = (2.15, -2.75, 1.15)
    P.assign_action(arm, bpy.data.actions["Idle"])
    P.render_still(PREVIEWS / "idle.jpg", three_q, 1)
    P.render_still(PREVIEWS / "idle-front.jpg", (0.0, -3.35, 1.05), 1)
    run = bpy.data.actions.get("Run")
    if run is not None:
        P.assign_action(arm, run)
        run_mid = int((run.frame_range[0] + run.frame_range[1]) * 0.5)
        P.render_still(PREVIEWS / "run-side.jpg", (3.35, 0.0, 1.05), run_mid)
    for name in ("Walk", "Run", "Jump", "Sword", "AirSlash"):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        P.assign_action(arm, action)
        mid = int((action.frame_range[0] + action.frame_range[1]) * 0.5)
        P.render_still(PREVIEWS / f"{name.lower()}-mid.jpg", three_q, mid)
    sword = bpy.data.actions.get("Sword")
    if sword is not None:
        P.assign_action(arm, sword)
        P.render_still(PREVIEWS / "sword-side.jpg", (3.35, 0.0, 1.05), 1)
        P.render_still(
            PREVIEWS / "sword-side-mid.jpg",
            (3.35, 0.0, 1.05),
            int((sword.frame_range[0] + sword.frame_range[1]) * 0.5),
        )
    plunge = bpy.data.actions.get("Plunge")
    if plunge is not None:
        P.assign_action(arm, plunge)
        P.render_still(PREVIEWS / "plunge.jpg", (2.15, -2.75, 1.15), 1)
        P.render_still(PREVIEWS / "plunge-side.jpg", (3.35, 0.0, 1.05), 1)


def main():
    missing = [name for name, path in CLIP_FBX.items() if not path.exists()]
    if missing or not BASE_FBX.exists():
        raise FileNotFoundError(missing or BASE_FBX)
    arm, mesh = import_base()
    make_idle(arm)
    for dest_name, path in CLIP_FBX.items():
        steal_clip(arm, path, dest_name)
        print("stole", dest_name)
    copy_named("Jump", "Fall")
    copy_named("Jump", "Land")
    copy_named("Idle", "MagicCharge")
    P.make_locomotion_inplace()
    extract_slash_windows(arm)
    plant_combat_hips(arm)
    keep = {
        "Idle",
        "Walk",
        "Run",
        "Jump",
        "Fall",
        "Land",
        "Sword",
        "AirSlash",
        "SpinRelease",
        "MagicCharge",
    }
    for action in list(bpy.data.actions):
        if action.name not in keep:
            bpy.data.actions.remove(action)
    for name in ("Walk", "Run", "Jump", "Sword", "AirSlash"):
        if name not in bpy.data.actions or not P.action_has_motion(bpy.data.actions[name]):
            raise RuntimeError(f"{name} missing motion")
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_previews(arm)
    export_and_copy(copy_to_locomotion=False, copy_to_attack=True)
    copy_textures_to_unity()
    data = {
        "schemaVersion": 1,
        "asset": "trial-meshy-ronin-attack",
        "taskId": "ORC-20260823-004",
        "actions": sorted(action.name for action in bpy.data.actions),
        "triangles": sum(len(p.vertices) - 2 for p in mesh.data.polygons),
        "notes": "Held-sword Ronin pack. Sword/AirSlash trimmed to the cut. Combat hips planted to Idle so AirSlash does not teleport. Sheathed maiden stays the locomotion mesh. HD-2D untouched.",
    }
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(data, indent=2))
    P.EXPORT_FBX = EXPORT_FBX
    P.verify_export(arm.name)


def slash_only():
    arm, _mesh = import_base()
    make_idle(arm)
    for name in ("Sword", "AirSlash", "SpinRelease"):
        steal_clip(arm, CLIP_FBX[name], name)
        print("stole", name)
    extract_slash_windows(arm)
    plant_combat_hips(arm)
    make_plunge(arm)
    plant_combat_feet(arm)
    keep = {"Idle", "Sword", "AirSlash", "SpinRelease", "Plunge"}
    for action in list(bpy.data.actions):
        if action.name not in keep:
            bpy.data.actions.remove(action)
    render_previews(arm)
    export_and_copy(copy_to_locomotion=False, copy_to_attack=True)
    print("slash-only export", EXPORT_FBX, "->", UNITY_ATTACK_FBX)


if __name__ == "__main__":
    if "--slash-only" in sys.argv:
        slash_only()
    else:
        main()
