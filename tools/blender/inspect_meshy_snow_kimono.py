"""Inspect a Meshy Snow Kimono source without modifying game assets.

Run with Blender 4.5:

  blender -b --python tools/blender/inspect_meshy_snow_kimono.py -- \
    path/to/model.glb --report path/to/report.json --preview-dir path/to/previews

The report and optional previews are the gate before rigging or Unity integration.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


SUPPORTED_EXTENSIONS = {".fbx", ".glb", ".gltf"}
PREVIEW_VIEWS = {
    "front": Vector((0.0, -1.0, 0.0)),
    "right": Vector((1.0, 0.0, 0.0)),
    "back": Vector((0.0, 1.0, 0.0)),
    "left": Vector((-1.0, 0.0, 0.0)),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--preview-dir", type=Path)
    argv = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    return parser.parse_args(argv)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.fps = 30
    world = bpy.data.worlds.new("MeshySnowKimonoInspectionWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.055, 0.065, 0.085, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.65
    scene.world = world


def import_source(source: Path) -> None:
    suffix = source.suffix.lower()
    if suffix in {".glb", ".gltf"}:
        bpy.ops.import_scene.gltf(filepath=str(source))
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(
            filepath=str(source),
            use_anim=True,
            automatic_bone_orientation=True,
        )
    else:
        raise ValueError(f"Unsupported source type {suffix!r}; expected GLB, glTF, or FBX")
    bpy.context.view_layer.update()


def world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    if not points:
        raise RuntimeError("Source contains no mesh objects")
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def image_info(image: bpy.types.Image) -> dict:
    path = bpy.path.abspath(image.filepath) if image.filepath else ""
    packed = image.packed_file is not None
    return {
        "name": image.name,
        "size": [int(image.size[0]), int(image.size[1])],
        "packed": packed,
        "filepath": path,
        "fileExists": bool(path) and Path(path).is_file(),
        "colorspace": image.colorspace_settings.name,
    }


def material_info(material: bpy.types.Material) -> dict:
    images = []
    if material.use_nodes and material.node_tree:
        images = [
            node.image.name
            for node in material.node_tree.nodes
            if node.type == "TEX_IMAGE" and node.image is not None
        ]
    return {
        "name": material.name,
        "users": material.users,
        "useNodes": material.use_nodes,
        "images": images,
        "blendMethod": getattr(material, "surface_render_method", None),
    }


def mesh_info(obj: bpy.types.Object) -> dict:
    mesh = obj.data
    mesh.calc_loop_triangles()
    total_weighted_vertices = 0
    maximum_influences = 0
    for vertex in mesh.vertices:
        count = sum(1 for group in vertex.groups if group.weight > 1e-6)
        total_weighted_vertices += int(count > 0)
        maximum_influences = max(maximum_influences, count)
    lo, hi = world_bounds([obj])
    return {
        "name": obj.name,
        "dataName": mesh.name,
        "parent": obj.parent.name if obj.parent else None,
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "materialSlots": [slot.material.name if slot.material else None for slot in obj.material_slots],
        "shapeKeys": list(mesh.shape_keys.key_blocks.keys()) if mesh.shape_keys else [],
        "vertexGroups": len(obj.vertex_groups),
        "weightedVertices": total_weighted_vertices,
        "unweightedVertices": len(mesh.vertices) - total_weighted_vertices,
        "maximumInfluencesPerVertex": maximum_influences,
        "armatureModifiers": [
            modifier.object.name if modifier.object else None
            for modifier in obj.modifiers
            if modifier.type == "ARMATURE"
        ],
        "bounds": {"min": list(lo), "max": list(hi), "size": list(hi - lo)},
        "transform": {
            "location": list(obj.location),
            "rotationEuler": list(obj.rotation_euler),
            "scale": list(obj.scale),
            "worldDeterminant": obj.matrix_world.to_3x3().determinant(),
        },
    }


def armature_info(obj: bpy.types.Object) -> dict:
    return {
        "name": obj.name,
        "parent": obj.parent.name if obj.parent else None,
        "boneCount": len(obj.data.bones),
        "bones": [
            {"name": bone.name, "parent": bone.parent.name if bone.parent else None}
            for bone in obj.data.bones
        ],
        "transform": {
            "location": list(obj.location),
            "rotationEuler": list(obj.rotation_euler),
            "scale": list(obj.scale),
        },
    }


def action_info(action: bpy.types.Action) -> dict:
    frame_start, frame_end = action.frame_range
    return {
        "name": action.name,
        "frameStart": frame_start,
        "frameEnd": frame_end,
        "frameCount": max(0.0, frame_end - frame_start + 1.0),
        "fcurves": len(action.fcurves),
        "slots": [slot.identifier for slot in action.slots] if hasattr(action, "slots") else [],
    }


def build_report(source: Path) -> tuple[dict, Vector, Vector]:
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    lo, hi = world_bounds(meshes)
    mesh_reports = [mesh_info(obj) for obj in meshes]
    image_reports = [image_info(image) for image in bpy.data.images if image.source != "VIEWER"]
    report = {
        "taskId": "ORC-20260905-001",
        "asset": "meshy-snow-kimono",
        "source": {
            "path": str(source.resolve()),
            "format": source.suffix.lower().lstrip("."),
            "bytes": source.stat().st_size,
            "sha256": sha256(source),
        },
        "scene": {
            "objectCount": len(bpy.context.scene.objects),
            "meshCount": len(meshes),
            "armatureCount": len(armatures),
            "actionCount": len(bpy.data.actions),
            "materialCount": len(bpy.data.materials),
            "imageCount": len(image_reports),
            "vertices": sum(item["vertices"] for item in mesh_reports),
            "triangles": sum(item["triangles"] for item in mesh_reports),
            "bounds": {"min": list(lo), "max": list(hi), "size": list(hi - lo)},
            "apparentHeightMetersIfZUp": hi.z - lo.z,
        },
        "meshes": mesh_reports,
        "armatures": [armature_info(obj) for obj in armatures],
        "actions": [action_info(action) for action in bpy.data.actions],
        "materials": [material_info(material) for material in bpy.data.materials],
        "images": image_reports,
        "sourceFacts": {
            "hasMesh": bool(meshes),
            "hasMaterials": bool(bpy.data.materials),
            "hasImages": bool(image_reports),
            "allExternalImagesPresent": all(item["packed"] or item["fileExists"] for item in image_reports),
            "hasArmature": bool(armatures),
            "hasSkinWeights": any(item["weightedVertices"] > 0 for item in mesh_reports),
        },
        "notes": [
            "Inspection only; no scaling, retopology, rigging, or game asset mutation was performed.",
            "Source facts require visual review before the source can pass the appearance gate.",
            "Preview labels assume the imported character faces Blender -Y, matching prior Meshy exports.",
        ],
    }
    return report, lo, hi


def point_camera(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def add_preview_lighting(center: Vector, size: Vector) -> None:
    key_data = bpy.data.lights.new("InspectionKey", "AREA")
    key_data.energy = 900.0
    key_data.shape = "DISK"
    key_data.size = max(2.0, size.z * 1.2)
    key = bpy.data.objects.new("InspectionKey", key_data)
    bpy.context.collection.objects.link(key)
    key.location = center + Vector((size.z * 1.2, -size.z * 1.5, size.z * 1.1))
    point_camera(key, center)

    fill_data = bpy.data.lights.new("InspectionFill", "AREA")
    fill_data.energy = 550.0
    fill_data.size = max(2.0, size.z)
    fill = bpy.data.objects.new("InspectionFill", fill_data)
    bpy.context.collection.objects.link(fill)
    fill.location = center + Vector((-size.z * 1.1, -size.z * 0.5, size.z * 0.65))
    point_camera(fill, center)


def render_previews(preview_dir: Path, lo: Vector, hi: Vector) -> list[dict]:
    preview_dir.mkdir(parents=True, exist_ok=True)
    center = (lo + hi) * 0.5
    size = hi - lo
    height = max(size.z, 0.01)
    radius = max(size.x, size.y, height * 0.45)
    distance = max(height * 2.1, radius * 3.0)
    camera_data = bpy.data.cameras.new("InspectionCamera")
    camera_data.lens = 58.0
    camera = bpy.data.objects.new("InspectionCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera
    add_preview_lighting(center, size)

    results = []
    for label, direction in PREVIEW_VIEWS.items():
        camera.location = center + direction * distance + Vector((0.0, 0.0, height * 0.02))
        point_camera(camera, center)
        output = (preview_dir / f"source-{label}.png").resolve()
        bpy.context.scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        results.append({"view": label, "path": str(output), "bytes": output.stat().st_size})
    return results


def main() -> None:
    args = parse_args()
    source = args.source.resolve()
    if not source.is_file():
        raise FileNotFoundError(source)
    if source.suffix.lower() not in SUPPORTED_EXTENSIONS:
        raise ValueError(f"Unsupported source extension: {source.suffix}")

    reset_scene()
    import_source(source)
    report, lo, hi = build_report(source)
    if args.preview_dir:
        report["previews"] = render_previews(args.preview_dir.resolve(), lo, hi)
    report_path = args.report.resolve()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("MESHY_SNOW_KIMONO_INSPECTION=" + json.dumps(report, ensure_ascii=False))
    print("Wrote", report_path)


if __name__ == "__main__":
    main()
