"""Diagnose whether merged Meshy clips actually drive pose bones."""

from __future__ import annotations

import json
from pathlib import Path

import bpy

ROOT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1")
SRC = ROOT / "drop" / "sym-v2" / "Meshy_AI_Azure_Blade_Maiden_biped" / "Meshy_AI_Azure_Blade_Maiden_biped_Meshy_AI_Meshy_Merged_Animations.fbx"
EXPORT = ROOT / "export" / "girl1-meshy-sym-v2.fbx"
OUT = ROOT / "drop" / "sym-v2" / "diagnose.json"


def action_slots(action):
    if not hasattr(action, "slots"):
        return []
    rows = []
    for slot in action.slots:
        rows.append(
            {
                "name": getattr(slot, "name_display", None) or getattr(slot, "name", None),
                "identifier": getattr(slot, "identifier", None),
                "target_id_type": str(getattr(slot, "target_id_type", None)),
            }
        )
    return rows


def fcurve_stats(action):
    paths = []
    varying = 0
    for fcurve in action.fcurves:
        values = [kp.co[1] for kp in fcurve.keyframe_points]
        span = (max(values) - min(values)) if values else 0.0
        if abs(span) > 1e-5:
            varying += 1
        if len(paths) < 12:
            paths.append(
                {
                    "path": fcurve.data_path,
                    "index": fcurve.array_index,
                    "keys": len(fcurve.keyframe_points),
                    "span": round(span, 6),
                }
            )
    return {
        "fcurves": len(action.fcurves),
        "varying": varying,
        "frame_range": [float(action.frame_range[0]), float(action.frame_range[1])],
        "slots": action_slots(action),
        "sample_paths": paths,
    }


def assign_action(arm, action, use_slot):
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = action
    assigned = None
    if use_slot and hasattr(arm.animation_data, "action_slot") and hasattr(action, "slots") and len(action.slots) > 0:
        arm.animation_data.action_slot = action.slots[0]
        assigned = action.slots[0].identifier if hasattr(action.slots[0], "identifier") else str(action.slots[0])
    bpy.context.view_layer.update()
    return assigned


def pose_sample(arm, names):
    bpy.context.view_layer.update()
    out = {}
    for name in names:
        bone = arm.pose.bones.get(name)
        if bone is None:
            out[name] = None
            continue
        q = bone.rotation_quaternion
        out[name] = [round(q.w, 4), round(q.x, 4), round(q.y, 4), round(q.z, 4)]
    return out


def inspect(path: Path, use_slot: bool):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    scene = bpy.context.scene
    bones = ["Hips", "LeftArm", "RightArm", "LeftUpLeg", "RightUpLeg", "LeftHand", "RightHand"]
    actions = []
    pose = {}
    for action in bpy.data.actions:
        row = {"name": action.name, **fcurve_stats(action)}
        actions.append(row)
        assign_action(arm, action, use_slot)
        mid = int((action.frame_range[0] + action.frame_range[1]) * 0.5)
        scene.frame_set(int(action.frame_range[0]))
        start_pose = pose_sample(arm, bones)
        scene.frame_set(mid)
        mid_pose = pose_sample(arm, bones)
        pose[action.name] = {"start": start_pose, "mid": mid_pose, "changed": start_pose != mid_pose}
    return {
        "file": path.name,
        "armature": arm.name,
        "active_action": arm.animation_data.action.name if arm.animation_data and arm.animation_data.action else None,
        "has_action_slot_attr": hasattr(arm.animation_data, "action_slot") if arm.animation_data else False,
        "actions": actions,
        "pose": pose,
    }


def main():
    report = {
        "src_no_slot": inspect(SRC, False),
        "src_with_slot": inspect(SRC, True),
        "export_no_slot": inspect(EXPORT, False) if EXPORT.exists() else None,
        "export_with_slot": inspect(EXPORT, True) if EXPORT.exists() else None,
    }
    OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
