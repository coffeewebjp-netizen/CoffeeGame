"""Add a maiden Dodge clip from 360_Power_Spin_Jump without rebaking Walk/Run."""

from __future__ import annotations

import importlib.util
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]
SRC_FBX = (
    ROOT
    / "art"
    / "3d"
    / "trials"
    / "meshy-girl1"
    / "drop"
    / "sym-v2"
    / "Meshy_AI_Azure_Blade_Maiden_biped"
    / "Meshy_AI_Azure_Blade_Maiden_biped_Meshy_AI_Meshy_Merged_Animations.fbx"
)
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
EXPORT_FBX = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "export" / "girl1-meshy-sym-v2.fbx"
PREVIEWS = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "previews" / "sym-v2"
SOURCE_ACTION = "Armature|360_Power_Spin_Jump"
# Airborne sideways spin only. Frames 1-29 are a standing takeoff that
# fills the whole dodge when the 93-frame clip plays at native speed.
DODGE_WINDOW = (30, 62)


def load_sym():
    path = Path(__file__).with_name("prepare_meshy_sym_v2.py")
    spec = importlib.util.spec_from_file_location("prepare_meshy_sym_v2", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


P = load_sym()


def snapshot_source_dodge():
    if not SRC_FBX.exists():
        raise FileNotFoundError(SRC_FBX)
    P.reset()
    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    action = bpy.data.actions.get(SOURCE_ACTION)
    if action is None:
        names = [item.name for item in bpy.data.actions]
        raise RuntimeError(f"{SOURCE_ACTION} missing. have={names}")
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    bpy.ops.object.mode_set(mode="OBJECT")
    start, end = DODGE_WINDOW
    P.assign_action(arm, action)
    poses = []
    scene = bpy.context.scene
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        pose = P.snapshot_pose(arm)
        if poses:
            rest_hip = poses[0]["Hips"][0]
            location, quaternion, scale = pose["Hips"]
            location.x = rest_hip.x
            location.y = rest_hip.y
            pose["Hips"] = (location, quaternion, scale)
        poses.append(pose)
    print("snapshot dodge", len(poses), "frames")
    return poses


def bind_all_actions(arm):
    for action in bpy.data.actions:
        action.use_fake_user = True
        P.assign_action(arm, action)


def require_locomotion_motion():
    missing = []
    for name in ("Walk", "Run"):
        action = bpy.data.actions.get(name)
        if action is None or not P.action_has_motion(action):
            missing.append(name)
    if missing:
        raise RuntimeError(f"locomotion lost motion: {missing}")


def snapshot_window(arm, action, start, end):
    P.assign_action(arm, action)
    scene = bpy.context.scene
    poses = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses.append(P.snapshot_pose(arm))
    return poses


def rebake_existing(arm):
    for name in (
        "Idle",
        "Walk",
        "Run",
        "Jump",
        "Fall",
        "Land",
        "Sword",
        "MagicCharge",
        "MagicRelease",
    ):
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        start = int(round(action.frame_range[0]))
        end = int(round(action.frame_range[1]))
        poses = snapshot_window(arm, action, start, end)
        if name in ("Walk", "Run") and poses[0] == poses[min(5, len(poses) - 1)]:
            raise RuntimeError(f"{name} imported as rest pose")
        rebuild(arm, name, poses)
        print("rebaked", name, "frames", len(poses))


def rebuild(arm, name, poses):
    old = bpy.data.actions.get(name)
    rebuilt = bpy.data.actions.new(name + "__dodge")
    rebuilt.use_fake_user = True
    P.assign_action(arm, rebuilt)
    for index, pose in enumerate(poses):
        P.apply_pose(arm, pose)
        P.keyframe_pose(arm, 1 + index)
    if old is not None and old != rebuilt:
        bpy.data.actions.remove(old)
    rebuilt.name = name
    P.assign_action(arm, rebuilt)


def export_maiden(arm):
    bind_all_actions(arm)
    require_locomotion_motion()
    EXPORT_FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"} and not obj.name.startswith("PreviewOnly"):
            obj.select_set(True)
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
    P.reset()
    bpy.ops.import_scene.fbx(filepath=str(EXPORT_FBX), automatic_bone_orientation=True)
    for action in list(bpy.data.actions):
        leaf = action.name.split("|")[-1]
        if leaf != action.name and leaf not in bpy.data.actions:
            action.name = leaf
    require_locomotion_motion()
    if bpy.data.actions.get("Dodge") is None:
        raise RuntimeError("exported FBX missing Dodge")
    UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
    import shutil

    shutil.copy2(EXPORT_FBX, UNITY_FBX)


def main():
    dodge_poses = snapshot_source_dodge()
    source = UNITY_FBX if UNITY_FBX.exists() else EXPORT_FBX
    P.reset()
    bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    arm.name = "MeshyGirl1Rig"
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    bpy.ops.object.mode_set(mode="OBJECT")
    for action in list(bpy.data.actions):
        leaf = action.name.split("|")[-1]
        if leaf != action.name and leaf not in bpy.data.actions:
            action.name = leaf
    bind_all_actions(arm)
    require_locomotion_motion()
    rebake_existing(arm)
    require_locomotion_motion()
    rebuild(arm, "Dodge", dodge_poses)
    bind_all_actions(arm)
    require_locomotion_motion()
    P.PREVIEWS = PREVIEWS
    P.add_studio()
    three_q = (2.15, -2.75, 1.15)
    dodge = bpy.data.actions["Dodge"]
    P.assign_action(arm, dodge)
    mid = int((dodge.frame_range[0] + dodge.frame_range[1]) * 0.5)
    P.render_still(PREVIEWS / "dodge-mid.jpg", three_q, mid)
    export_maiden(arm)
    print("dodge clip", len(dodge_poses), "frames ->", UNITY_FBX)


if __name__ == "__main__":
    main()
