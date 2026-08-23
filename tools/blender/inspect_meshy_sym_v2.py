"""List armatures, meshes, and actions in the symmetric Meshy v2 FBXs."""

from pathlib import Path
import json
import bpy

ROOT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\drop\sym-v2\Meshy_AI_Azure_Blade_Maiden_biped")
OUT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\drop\sym-v2\inspect.json")
files = sorted(ROOT.glob("*.fbx"))
report = []
for fbx in files:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx), automatic_bone_orientation=True)
    meshes = []
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        meshes.append(
            {
                "name": obj.name,
                "verts": len(obj.data.vertices),
                "tris": sum(len(p.vertices) - 2 for p in obj.data.polygons),
                "uvs": [uv.name for uv in obj.data.uv_layers],
            }
        )
    arms = []
    for obj in bpy.data.objects:
        if obj.type != "ARMATURE":
            continue
        arms.append({"name": obj.name, "bones": [b.name for b in obj.data.bones]})
    report.append(
        {
            "file": fbx.name,
            "bytes": fbx.stat().st_size,
            "actions": [a.name for a in bpy.data.actions],
            "action_ranges": {
                a.name: [float(a.frame_range[0]), float(a.frame_range[1])]
                for a in bpy.data.actions
            },
            "armatures": arms,
            "meshes": meshes,
        }
    )
OUT.write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
