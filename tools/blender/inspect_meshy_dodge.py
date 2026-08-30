"""Measure which 360_Power_Spin_Jump frames are the sideways flying pose."""

from __future__ import annotations

import importlib.util
from math import degrees
from pathlib import Path

import bpy
from mathutils import Vector

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
OUT = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "previews" / "sym-v2" / "dodge-inspect.txt"
PREVIEWS = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "previews" / "sym-v2"
SOURCE_ACTION = "Armature|360_Power_Spin_Jump"


def load_sym():
    path = Path(__file__).with_name("prepare_meshy_sym_v2.py")
    spec = importlib.util.spec_from_file_location("prepare_meshy_sym_v2", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


P = load_sym()


def bone_world(arm, name):
    bone = arm.pose.bones.get(name)
    if bone is None:
        return None
    return arm.matrix_world @ bone.head


def main():
    P.reset()
    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    action = bpy.data.actions.get(SOURCE_ACTION)
    if action is None:
        raise RuntimeError("missing " + SOURCE_ACTION)
    P.assign_action(arm, action)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    for bone in arm.pose.bones:
        bone.rotation_mode = "QUATERNION"
    bpy.ops.object.mode_set(mode="OBJECT")
    start = int(round(action.frame_range[0]))
    end = int(round(action.frame_range[1]))
    scene = bpy.context.scene
    lines = [f"action {SOURCE_ACTION} frames {start}-{end}"]
    best = []
    for frame in range(start, end + 1):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        hips = bone_world(arm, "Hips")
        head = bone_world(arm, "Head")
        left_foot = bone_world(arm, "LeftFoot") or bone_world(arm, "LeftToeBase")
        right_foot = bone_world(arm, "RightFoot") or bone_world(arm, "RightToeBase")
        if hips is None or head is None:
            continue
        up = head - hips
        horiz = Vector((up.x, up.y, 0.0))
        tilt = degrees(up.to_track_quat("Z", "Y").to_euler().x) if up.length > 0.001 else 0.0
        side = horiz.length / max(up.length, 0.001)
        min_foot = min(
            left_foot.z if left_foot is not None else hips.z,
            right_foot.z if right_foot is not None else hips.z,
        )
        row = (
            f"f{frame:03d} hipsz={hips.z:5.2f} headz={head.z:5.2f} "
            f"footz={min_foot:5.2f} tilt={tilt:6.1f} side={side:4.2f}"
        )
        lines.append(row)
        best.append((side, abs(hips.z), frame, row))
    best.sort(reverse=True)
    lines.append("top sideways:")
    lines.extend(item[3] for item in best[:12])
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines), encoding="utf-8")
    print("\n".join(lines[:8]))
    print("...")
    print("\n".join(item[3] for item in best[:12]))
    P.add_studio()
    for frame in (1, 16, 28, 40, 47, 56, 68, 80, 93):
        P.assign_action(arm, action)
        P.render_still(PREVIEWS / f"dodge-f{frame:02d}.jpg", (2.15, -2.75, 1.15), frame)
        print("rendered", frame)


if __name__ == "__main__":
    main()
