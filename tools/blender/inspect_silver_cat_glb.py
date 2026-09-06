"""Inspect a Meshy silver-cat GLB and extract its packed texture images.

Usage:
  blender --background --python inspect_silver_cat_glb.py -- input.glb report.json texture-dir
"""

import json
import os
import re
import sys

import bpy
from mathutils import Vector


def after_double_dash():
    return sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []


def safe_name(value):
    value = re.sub(r"[^A-Za-z0-9_.-]+", "-", value or "image").strip("-.")
    return value or "image"


def portable_path(value):
    try:
        return os.path.relpath(value, os.getcwd()).replace("\\", "/")
    except ValueError:
        return os.path.basename(value)


def image_kind(image):
    name = (image.name or "").lower()
    if "normal" in name:
        return "normal"
    if any(token in name for token in ("metal", "rough", "orm", "occlusion")):
        return "packed"
    return "basecolor"


args = after_double_dash()
if len(args) != 3:
    raise SystemExit("Expected: input.glb report.json texture-dir")

input_path, report_path, texture_dir = map(os.path.abspath, args)
os.makedirs(os.path.dirname(report_path), exist_ok=True)
os.makedirs(texture_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
if input_path.lower().endswith(".fbx"):
    bpy.ops.import_scene.fbx(filepath=input_path, use_anim=True)
else:
    bpy.ops.import_scene.gltf(filepath=input_path, import_pack_images=True)

meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]

world_points = []
for obj in meshes:
    world_points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)

mins = [min(point[i] for point in world_points) for i in range(3)] if world_points else [0, 0, 0]
maxs = [max(point[i] for point in world_points) for i in range(3)] if world_points else [0, 0, 0]
dimensions = [maxs[i] - mins[i] for i in range(3)]

for armature in armatures:
    armature.data.pose_position = "REST"
bpy.context.scene.frame_set(0)
depsgraph = bpy.context.evaluated_depsgraph_get()
rest_points = []
for obj in meshes:
    evaluated = obj.evaluated_get(depsgraph)
    rest_points.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
rest_mins = [min(point[i] for point in rest_points) for i in range(3)] if rest_points else [0, 0, 0]
rest_maxs = [max(point[i] for point in rest_points) for i in range(3)] if rest_points else [0, 0, 0]
rest_dimensions = [rest_maxs[i] - rest_mins[i] for i in range(3)]
skinned_rest_points = []
for obj in meshes:
    if not any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
        continue
    evaluated = obj.evaluated_get(depsgraph)
    skinned_rest_points.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
skinned_rest_mins = [min(point[i] for point in skinned_rest_points) for i in range(3)]
skinned_rest_maxs = [max(point[i] for point in skinned_rest_points) for i in range(3)]
skinned_rest_dimensions = [skinned_rest_maxs[i] - skinned_rest_mins[i] for i in range(3)]

images = []
used_names = set()
for image in bpy.data.images:
    if image.name == "Render Result" or image.size[0] <= 0 or image.size[1] <= 0:
        continue
    kind = image_kind(image)
    name = safe_name(image.name)
    if not name.lower().endswith(".png"):
        name += ".png"
    candidate = name
    suffix = 2
    while candidate.lower() in used_names:
        root, ext = os.path.splitext(name)
        candidate = f"{root}-{suffix}{ext}"
        suffix += 1
    used_names.add(candidate.lower())
    output_path = os.path.join(texture_dir, candidate)
    image.filepath_raw = output_path
    image.file_format = "PNG"
    image.save()
    images.append(
        {
            "name": image.name,
            "kind": kind,
            "size": list(image.size),
            "colorspace": image.colorspace_settings.name,
            "output": portable_path(output_path),
        }
    )

mesh_records = []
for obj in meshes:
    mesh = obj.data
    mesh.calc_loop_triangles()
    mesh_records.append(
        {
            "object": obj.name,
            "mesh": mesh.name,
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.loop_triangles),
            "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
            "armature_modifiers": [modifier.object.name for modifier in obj.modifiers if modifier.type == "ARMATURE" and modifier.object],
            "vertex_groups": len(obj.vertex_groups),
        }
    )

armature_records = []
for obj in armatures:
    armature_records.append(
        {
            "object": obj.name,
            "bones": len(obj.data.bones),
            "bone_names": [bone.name for bone in obj.data.bones],
        }
    )

action_records = []
for action in bpy.data.actions:
    action_records.append(
        {
            "name": action.name,
            "frame_range": list(action.frame_range),
            "frame_count": action.frame_range[1] - action.frame_range[0] + 1,
            "slots": [slot.identifier for slot in action.slots],
        }
    )

report = {
    "source": portable_path(input_path),
    "scene_fps": bpy.context.scene.render.fps,
    "animated_bounds": {"min": mins, "max": maxs, "dimensions": dimensions},
    "rest_bounds": {"min": rest_mins, "max": rest_maxs, "dimensions": rest_dimensions},
    "skinned_rest_bounds": {"min": skinned_rest_mins, "max": skinned_rest_maxs, "dimensions": skinned_rest_dimensions},
    "meshes": mesh_records,
    "mesh_totals": {
        "objects": len(meshes),
        "vertices": sum(record["vertices"] for record in mesh_records),
        "triangles": sum(record["triangles"] for record in mesh_records),
    },
    "armatures": armature_records,
    "actions": action_records,
    "materials": [material.name for material in bpy.data.materials],
    "images": images,
}

with open(report_path, "w", encoding="utf-8") as handle:
    json.dump(report, handle, indent=2, ensure_ascii=False)

print(json.dumps(report, indent=2, ensure_ascii=False))
