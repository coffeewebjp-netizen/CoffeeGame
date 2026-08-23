"""Inspect the dropped Meshy girl1 FBX. Read-only besides a temp Blender session."""

from __future__ import annotations

import json
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
FBX = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "drop" / "Meshy_AI_Azure_Blade_Maiden_biped_Animation_Walking_withSkin.fbx"
OUT = ROOT / "art" / "3d" / "trials" / "meshy-girl1" / "drop" / "inspect.json"


def bbox(obj):
    corners = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    xs, ys, zs = [c.x for c in corners], [c.y for c in corners], [c.z for c in corners]
    return {
        "min": [min(xs), min(ys), min(zs)],
        "max": [max(xs), max(ys), max(zs)],
        "size": [max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)],
    }


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(FBX), automatic_bone_orientation=True)

report = {
    "fbx": str(FBX),
    "bytes": FBX.stat().st_size,
    "objects": [],
    "armatures": [],
    "actions": [action.name for action in bpy.data.actions],
    "materials": [mat.name for mat in bpy.data.materials],
    "images": [img.name for img in bpy.data.images],
}

for obj in bpy.data.objects:
    item = {
        "name": obj.name,
        "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "location": list(obj.location),
        "scale": list(obj.scale),
        "children": [child.name for child in obj.children],
    }
    if obj.type == "MESH":
        mesh = obj.data
        item["verts"] = len(mesh.vertices)
        item["faces"] = len(mesh.polygons)
        item["tris"] = sum(len(poly.vertices) - 2 for poly in mesh.polygons)
        item["bbox"] = bbox(obj)
        item["vertex_groups"] = [group.name for group in obj.vertex_groups]
        item["modifiers"] = [mod.type + ":" + mod.name for mod in obj.modifiers]
        item["materials"] = [slot.material.name if slot.material else None for slot in obj.material_slots]
    if obj.type == "ARMATURE":
        bones = []
        for bone in obj.data.bones:
            bones.append(
                {
                    "name": bone.name,
                    "parent": bone.parent.name if bone.parent else None,
                    "head": list(bone.head_local),
                    "tail": list(bone.tail_local),
                }
            )
        item["bones"] = bones
        report["armatures"].append(item["name"])
    report["objects"].append(item)

OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps({"objects": len(report["objects"]), "actions": report["actions"], "armatures": report["armatures"]}, indent=2))
