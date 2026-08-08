"""Reimport and validate heroine-v3 FBX, including backface-cull safety.

Run:
  blender -b --python tools/blender/validate_hero_v3_fbx.py
"""

from __future__ import annotations

import json
from collections import defaultdict, deque
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v3.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v3.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v3.json"
REPORT_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v3-fbx-validation.json"
PREVIEWS = [
    ROOT / "art" / "3d" / "previews" / "heroine-v3-front.png",
    ROOT / "art" / "3d" / "previews" / "heroine-v3-game-camera.png",
]
REQUIRED_ACTIONS = {
    "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
    "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
    "Hurt", "Defeated",
}


def normalized_action_names():
    return sorted({action.name.split("|")[-1] for action in bpy.data.actions})


def scene_counts():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    return {
        "objects": len(bpy.context.scene.objects),
        "meshObjects": len(meshes),
        "meshNames": sorted(obj.name for obj in meshes),
        "vertices": sum(len(obj.data.vertices) for obj in meshes),
        "triangles": sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes),
        "materials": len({mat for obj in meshes for mat in obj.data.materials if mat}),
        "armatures": len(rigs),
        "bones": sum(len(rig.data.bones) for rig in rigs),
        "actions": len(normalized_action_names()),
        "actionNames": normalized_action_names(),
    }


def closed_component_volumes(obj):
    """Return world-space volumes for every watertight triangle island.

    FBX can legitimately leave curve-tube ends open, so only watertight islands
    are used as an outside-winding assertion. That covers the head/crown,
    ribbons, skin, clothes, skirt, eyes, boots, blade and sheath.
    """
    mesh = obj.data
    mesh.calc_loop_triangles()
    triangles = list(mesh.loop_triangles)
    vertex_to_triangles = defaultdict(list)
    for triangle_index, triangle in enumerate(triangles):
        for vertex_index in triangle.vertices:
            vertex_to_triangles[vertex_index].append(triangle_index)

    visited = set()
    results = []
    for seed in range(len(triangles)):
        if seed in visited:
            continue
        queue = deque([seed])
        visited.add(seed)
        component = []
        while queue:
            triangle_index = queue.popleft()
            component.append(triangle_index)
            for vertex_index in triangles[triangle_index].vertices:
                for neighbor in vertex_to_triangles[vertex_index]:
                    if neighbor not in visited:
                        visited.add(neighbor)
                        queue.append(neighbor)

        edge_counts = defaultdict(int)
        volume = 0.0
        for triangle_index in component:
            ids = list(triangles[triangle_index].vertices)
            for index in range(3):
                edge_counts[tuple(sorted((ids[index], ids[(index + 1) % 3])))] += 1
            a, b, c = (obj.matrix_world @ mesh.vertices[vertex_index].co for vertex_index in ids)
            volume += a.dot(b.cross(c)) / 6.0
        closed = all(count == 2 for count in edge_counts.values())
        results.append({
            "triangles": len(component),
            "closed": closed,
            "signedWorldVolume": volume,
        })
    return results


def normal_qa():
    objects = []
    negative = []
    for obj in [candidate for candidate in bpy.context.scene.objects if candidate.type == "MESH"]:
        components = closed_component_volumes(obj)
        closed = [component for component in components if component["closed"] and abs(component["signedWorldVolume"]) > 1e-12]
        failures = [component for component in closed if component["signedWorldVolume"] < -1e-10]
        objects.append({
            "name": obj.name,
            "determinant": obj.matrix_world.to_3x3().determinant(),
            "componentCount": len(components),
            "closedComponentCount": len(closed),
            "negativeClosedComponentCount": len(failures),
            "minimumClosedVolume": min((component["signedWorldVolume"] for component in closed), default=0.0),
        })
        if failures:
            negative.append(obj.name)
    return {"objects": objects, "negativeClosedMeshes": negative, "backfaceCullSafe": not negative}


def preview_info():
    result = []
    for path in PREVIEWS:
        image = bpy.data.images.load(str(path), check_existing=False)
        result.append({
            "path": str(path.relative_to(ROOT)).replace("\\", "/"),
            "size": [int(image.size[0]), int(image.size[1])],
            "bytes": path.stat().st_size,
        })
        bpy.data.images.remove(image)
    return result


def validate():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    source_counts = scene_counts()
    source_normals = normal_qa()
    source_missing = sorted(REQUIRED_ACTIONS - set(source_counts["actionNames"]))
    previews = preview_info()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_anim=True)
    fbx_counts = scene_counts()
    fbx_normals = normal_qa()
    fbx_missing = sorted(REQUIRED_ACTIONS - set(fbx_counts["actionNames"]))

    checks = {
        "blendReadable": bool(source_counts["objects"]),
        "sourceMeshContract": source_counts["meshObjects"] == 3,
        "sourceRigContract": source_counts["armatures"] == 1 and source_counts["bones"] == 20,
        "sourceActionContract": not source_missing,
        "sourceMobileBudget": source_counts["vertices"] <= 40000 and source_counts["triangles"] <= 80000,
        "sourceOutsideNormals": source_normals["backfaceCullSafe"],
        "fbxMeshContract": fbx_counts["meshObjects"] == 3,
        "fbxRigContract": fbx_counts["armatures"] == 1 and fbx_counts["bones"] == 20,
        "fbxActionContract": not fbx_missing,
        "fbxOutsideNormals": fbx_normals["backfaceCullSafe"],
        "previewsExist": all(path.exists() and path.stat().st_size > 10000 for path in PREVIEWS),
    }
    passed = all(checks.values())
    report = {
        "asset": "heroine-v3",
        "passed": passed,
        "checks": checks,
        "source": source_counts,
        "fbxReimport": fbx_counts,
        "missingActions": {"source": source_missing, "fbx": fbx_missing},
        "outsideNormalQA": {"source": source_normals, "fbxReimport": fbx_normals},
        "previews": previews,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest["validation"] = {
        "reopened": True,
        "fbxReimportPassed": passed,
        "outsideNormalQAPassed": source_normals["backfaceCullSafe"] and fbx_normals["backfaceCullSafe"],
        "missingRequiredActions": fbx_missing,
        "report": str(REPORT_PATH.relative_to(ROOT)).replace("\\", "/"),
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("HEROINE_V3_VALIDATION=" + json.dumps(report, ensure_ascii=False))
    if not passed:
        raise RuntimeError("Heroine v3 validation failed: " + json.dumps(checks))


if __name__ == "__main__":
    validate()
