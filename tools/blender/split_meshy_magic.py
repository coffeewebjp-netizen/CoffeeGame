"""Split the maiden MagicCharge clip into gather vs throw."""

from __future__ import annotations

import importlib.util
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[2]
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
CHARGE_WINDOW = (10, 48)
RELEASE_WINDOW = (48, 78)


def load_sym():
    path = Path(__file__).with_name("prepare_meshy_sym_v2.py")
    spec = importlib.util.spec_from_file_location("prepare_meshy_sym_v2", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


P = load_sym()


def snapshot_window(arm, action, start, end):
    P.assign_action(arm, action)
    scene = bpy.context.scene
    poses = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses.append(P.snapshot_pose(arm))
    return poses


def rebuild(arm, name, poses):
    old = bpy.data.actions.get(name)
    rebuilt = bpy.data.actions.new(name + "__split")
    rebuilt.use_fake_user = True
    P.assign_action(arm, rebuilt)
    for index, pose in enumerate(poses):
        P.apply_pose(arm, pose)
        P.keyframe_pose(arm, 1 + index)
    if old is not None and old != rebuilt:
        bpy.data.actions.remove(old)
    rebuilt.name = name
    P.assign_action(arm, rebuilt)


def rigidify_saya(mesh, arm):
    """Lock fused saya verts to Hips so Run does not bend the sheath."""
    bpy.context.view_layer.update()
    hips = arm.matrix_world @ arm.data.bones["Hips"].head_local
    indices = []
    for index, vert in enumerate(mesh.data.vertices):
        pos = mesh.matrix_world @ vert.co
        if pos.x < hips.x - 0.08 and 0.03 < pos.z < hips.z - 0.05 and abs(pos.y) < 0.22:
            indices.append(index)
    hips_group = mesh.vertex_groups.get("Hips")
    if hips_group is None or not indices:
        print("saya rigidify skipped", "verts", len(indices))
        return
    for index in indices:
        for group in mesh.vertex_groups:
            group.remove([index])
        hips_group.add([index], 1.0, "REPLACE")
    print("saya rigidified", len(indices), "verts")


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


def export_maiden():
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
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
    UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
    import shutil

    shutil.copy2(EXPORT_FBX, UNITY_FBX)


def main():
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
    for name in ("Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword"):
        if name in bpy.data.actions:
            action = bpy.data.actions[name]
            start = int(round(action.frame_range[0]))
            end = int(round(action.frame_range[1]))
            poses = snapshot_window(arm, action, start, end)
            if name in ("Walk", "Run") and poses[0] == poses[min(5, len(poses) - 1)]:
                raise RuntimeError(f"{name} imported as rest pose")
            rebuild(arm, name, poses)
            print("rebaked", name, "frames", len(poses))
    require_locomotion_motion()
    source_charge = bpy.data.actions.get("MagicCharge")
    if source_charge is None:
        raise RuntimeError("MagicCharge missing")
    charge_poses = snapshot_window(arm, source_charge, CHARGE_WINDOW[0], CHARGE_WINDOW[1])
    release_poses = snapshot_window(arm, source_charge, RELEASE_WINDOW[0], RELEASE_WINDOW[1])
    rebuild(arm, "MagicCharge", charge_poses)
    rebuild(arm, "MagicRelease", release_poses)
    bind_all_actions(arm)
    require_locomotion_motion()
    P.PREVIEWS = PREVIEWS
    P.add_studio()
    three_q = (2.15, -2.75, 1.15)
    charge = bpy.data.actions["MagicCharge"]
    P.assign_action(arm, charge)
    P.render_still(PREVIEWS / "magiccharge-mid.jpg", three_q, int((charge.frame_range[0] + charge.frame_range[1]) * 0.5))
    release = bpy.data.actions["MagicRelease"]
    P.assign_action(arm, release)
    P.render_still(PREVIEWS / "magicrelease-mid.jpg", three_q, int((release.frame_range[0] + release.frame_range[1]) * 0.5))
    run = bpy.data.actions["Run"]
    P.assign_action(arm, run)
    P.render_still(PREVIEWS / "run-mid.jpg", three_q, int((run.frame_range[0] + run.frame_range[1]) * 0.5))
    export_maiden()
    print("magic split", CHARGE_WINDOW, RELEASE_WINDOW, "->", UNITY_FBX)


if __name__ == "__main__":
    main()
