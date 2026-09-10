import json
import os
from pathlib import Path

import bpy


TRIAL_ROOT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-split-ink-v11")
EXPORT_DIR = TRIAL_ROOT / "exports"
EXPORT_DIR.mkdir(parents=True, exist_ok=True)

# Start from a deterministic empty scene so every object arriving through the
# bridge belongs to this candidate.
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

if "meshy" not in bpy.context.preferences.addons:
    bpy.ops.preferences.addon_enable(module="meshy")

bpy.ops.meshy.bridge_start("INVOKE_DEFAULT")
print("COFFEEGAME_MESHY_BRIDGE_READY 127.0.0.1:5324", flush=True)


def save_when_imported():
    imported = [obj for obj in bpy.data.objects if obj.name.startswith("Meshy_")]
    if not imported:
        return 1.0

    blend_path = EXPORT_DIR / "split-ink-dragon-rigged.blend"
    glb_path = EXPORT_DIR / "split-ink-dragon-rigged.glb"
    fbx_path = EXPORT_DIR / "split-ink-dragon-rigged.fbx"
    report_path = EXPORT_DIR / "bridge-import-report.json"

    try:
        bpy.ops.file.pack_all()
    except RuntimeError as exc:
        print(f"COFFEEGAME_PACK_WARNING {exc}", flush=True)

    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        export_animations=True,
    )
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=False,
        add_leaf_bones=False,
        bake_anim=True,
        path_mode="COPY",
        embed_textures=True,
    )

    mesh_objects = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
    report = {
        "status": "imported",
        "objects": len(bpy.data.objects),
        "meshObjects": len(mesh_objects),
        "armatures": len(armatures),
        "vertices": sum(len(obj.data.vertices) for obj in mesh_objects),
        "polygons": sum(len(obj.data.polygons) for obj in mesh_objects),
        "actions": [action.name for action in bpy.data.actions],
        "blend": str(blend_path),
        "glb": str(glb_path),
        "fbx": str(fbx_path),
    }
    report_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("COFFEEGAME_MESHY_BRIDGE_SAVED", json.dumps(report), flush=True)
    return None


bpy.app.timers.register(save_when_imported, first_interval=1.0)
