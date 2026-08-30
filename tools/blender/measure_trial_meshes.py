"""Compare maiden vs Ronin height, feet, and hips."""

from pathlib import Path
import json
import bpy
from mathutils import Vector

MAIDEN = Path(r"C:\work\CoffeeGAME\unity\CoffeeGame\Assets\CoffeeGame\Resources\Models\Hero\trial-anime-girl.fbx")
RONIN = Path(r"C:\work\CoffeeGAME\unity\CoffeeGame\Assets\CoffeeGame\Resources\Models\Hero\trial-anime-girl-attack.fbx")
OUT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\previews\mesh-measure.json")


def bone_world(arm, name):
    bone = arm.pose.bones.get(name)
    if bone is None:
        return None
    return arm.matrix_world @ bone.matrix @ Vector((0.0, 0.0, 0.0))


def mesh_bbox(mesh):
    bpy.context.view_layer.update()
    coords = [mesh.matrix_world @ Vector(v.co) for v in mesh.data.vertices]
    xs = [p.x for p in coords]
    ys = [p.y for p in coords]
    zs = [p.z for p in coords]
    return {
        "min": [min(xs), min(ys), min(zs)],
        "max": [max(xs), max(ys), max(zs)],
        "height": max(zs) - min(zs),
    }


def measure(path, action_name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
    if arm.animation_data is None:
        arm.animation_data_create()
    action = None
    for candidate in bpy.data.actions:
        if candidate.name.split("|")[-1] == action_name:
            action = candidate
            break
    if action is not None:
        arm.animation_data.action = action
        if hasattr(arm.animation_data, "action_slot") and action.slots:
            arm.animation_data.action_slot = action.slots[0]
        bpy.context.scene.frame_set(int(round(action.frame_range[0])))
        bpy.context.view_layer.update()
    hips = bone_world(arm, "Hips")
    left = bone_world(arm, "LeftFoot")
    right = bone_world(arm, "RightFoot")
    head = bone_world(arm, "Head")
    bbox = mesh_bbox(mesh)
    return {
        "file": path.name,
        "action": action_name,
        "arm": arm.name,
        "hips": None if hips is None else [round(hips.x, 3), round(hips.y, 3), round(hips.z, 3)],
        "leftFoot": None if left is None else [round(left.x, 3), round(left.y, 3), round(left.z, 3)],
        "rightFoot": None if right is None else [round(right.x, 3), round(right.y, 3), round(right.z, 3)],
        "head": None if head is None else [round(head.x, 3), round(head.y, 3), round(head.z, 3)],
        "bbox": {
            "min": [round(v, 3) for v in bbox["min"]],
            "max": [round(v, 3) for v in bbox["max"]],
            "height": round(bbox["height"], 3),
        },
    }


def main():
    rows = [
        measure(MAIDEN, "Idle"),
        measure(MAIDEN, "Run"),
        measure(RONIN, "Idle"),
        measure(RONIN, "Sword"),
        measure(RONIN, "Plunge"),
    ]
    OUT.write_text(json.dumps(rows, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(rows, indent=2))


if __name__ == "__main__":
    main()
