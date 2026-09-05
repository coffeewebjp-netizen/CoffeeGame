"""Replace scaled-armature bone parenting with a rigid RightHand skin.

The V3 Blender scene evaluates correctly, but FBX/glTF serialize its bone child
transform roughly 100x away because the imported Meshy armature has 0.01 object
scale. This keeps the katana separate while giving every weapon vertex weight
1.0 to RightHand, avoiding child-bone inverse transforms entirely.
"""

import argparse
import importlib.util
import json
import shutil
from pathlib import Path

import bpy
from mathutils import Matrix


HERE = Path(__file__).resolve().parent


def load_v3():
    spec = importlib.util.spec_from_file_location("azure_pipeline", HERE / "prepare_azure_maiden_clean.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-blend", type=Path, required=True)
    parser.add_argument("--out-dir", type=Path, required=True)
    return parser.parse_args(bpy.app.driver_namespace.get("argv_override") or
                             __import__("sys").argv[__import__("sys").argv.index("--") + 1:])


def grip_indices(obj):
    slots = [m.name if m else "" for m in obj.data.materials]
    result = set()
    for polygon in obj.data.polygons:
        if "WrappedGrip" in slots[polygon.material_index]:
            result.update(polygon.vertices)
    if not result:
        raise RuntimeError("Could not identify wrapped-grip vertices")
    return sorted(result)


def evaluated_grip_world(obj, indices):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh()
    try:
        return sum((evaluated.matrix_world @ mesh.vertices[i].co for i in indices),
                   mesh.vertices[indices[0]].co.copy() * 0.0) / len(indices)
    finally:
        evaluated.to_mesh_clear()


def action_for(canonical):
    exact = bpy.data.actions.get(canonical)
    if exact:
        return exact
    return next(a for a in bpy.data.actions if a.name.split("|")[-1] == canonical)


def samples(rig, katana, indices):
    hand_name = next(n for n in rig.pose.bones.keys() if n.lower().replace("_", "") == "righthand")
    result = []
    for canonical, fraction in (("Idle", 0.0), ("Run", 0.25), ("Run", 0.75),
                                ("Sword", 0.0), ("Sword", 0.35), ("Sword", 0.85)):
        action = action_for(canonical)
        rig.animation_data.action = action
        start, end = action.frame_range
        frame = int(round(start + (end - start) * fraction))
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        hand = (rig.matrix_world @ rig.pose.bones[hand_name].matrix).translation
        grip = evaluated_grip_world(katana, indices)
        result.append({"action": canonical, "frame": frame,
                       "gripToHandMeters": round((grip - hand).length, 6),
                       "handWorld": [round(float(x), 6) for x in hand],
                       "gripWorld": [round(float(x), 6) for x in grip]})
    return result


def main():
    args = parse_args()
    source = args.source_blend.resolve()
    out = args.out_dir.resolve()
    if out.exists() and any(out.iterdir()):
        raise FileExistsError("Use a new output directory to preserve previous derivatives")
    out.mkdir(parents=True, exist_ok=True)
    (out / "textures").mkdir(exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    v3 = load_v3()

    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    katana = next(o for o in bpy.data.objects if o.type == "MESH" and "katana" in o.name.lower())
    hand_name = next(n for n in rig.pose.bones.keys() if n.lower().replace("_", "") == "righthand")
    indices = grip_indices(katana)

    rig.data.pose_position = "REST"
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    old_world = katana.matrix_world.copy()
    rig_inverse = rig.matrix_world.inverted_safe()
    # Bake the correct V3 rest-world shape into armature object coordinates.
    transform = rig_inverse @ old_world
    for vertex in katana.data.vertices:
        vertex.co = transform @ vertex.co

    katana.parent = rig
    katana.parent_type = "OBJECT"
    katana.parent_bone = ""
    katana.matrix_parent_inverse = Matrix.Identity(4)
    katana.matrix_basis = Matrix.Identity(4)
    for modifier in list(katana.modifiers):
        if modifier.type == "ARMATURE":
            katana.modifiers.remove(modifier)
    for group in list(katana.vertex_groups):
        katana.vertex_groups.remove(group)
    group = katana.vertex_groups.new(name=hand_name)
    group.add(range(len(katana.data.vertices)), 1.0, "REPLACE")
    modifier = katana.modifiers.new("RigidRightHandSkin", "ARMATURE")
    modifier.object = rig
    modifier.use_vertex_groups = True
    katana["rigid_attachment"] = "100% RightHand skin"
    katana["v3_bone_parent_defect"] = "FBX/GLB grip evaluated about 23.6m from RightHand"
    rig.data.pose_position = "POSE"
    bpy.context.view_layer.update()
    source_samples = samples(rig, katana, indices)
    if max(x["gripToHandMeters"] for x in source_samples) > 0.08:
        raise RuntimeError(f"Corrected source grip is too far from hand: {source_samples}")

    accepted_atlas = source.parent / "textures" / "azure-maiden-base.png"
    new_atlas = out / "textures" / "azure-maiden-base.png"
    shutil.copyfile(accepted_atlas, new_atlas)
    image = bpy.data.images.get("AzureMaidenDirectRetexture")
    if image:
        image.filepath = str(new_atlas)
        image.filepath_raw = str(new_atlas)

    blend = out / "azure-maiden-clean-runtime.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    exports = v3.export_runtime(rig, out)
    report = {
        "schemaVersion": 1,
        "taskId": "ORC-20260905-001",
        "workPackage": "WP16",
        "outputId": "OUT20-v4-socket-fix",
        "status": "prepared-awaiting-reimport-validation",
        "sourceBlend": {"path": str(source), "sha256": v3.sha256(source)},
        "repair": "separate rigid katana mesh, armature object parent, 100% RightHand vertex weights",
        "bodyGeometryRigActionsChanged": False,
        "katana": {"vertices": len(katana.data.vertices),
                   "triangles": sum(len(p.vertices) - 2 for p in katana.data.polygons),
                   "parentType": katana.parent_type, "parent": rig.name,
                   "vertexGroup": hand_name, "minimumWeight": 1.0, "maximumWeight": 1.0,
                   "materials": [m.name for m in katana.data.materials]},
        "sourceSocketSamples": source_samples,
        "atlas": {"path": str(new_atlas), "sha256": v3.sha256(new_atlas)},
        "files": {"blend": {"path": str(blend), "sha256": v3.sha256(blend)}, **exports},
    }
    manifest = out / "manifest.json"
    manifest.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("WP16_V4=" + json.dumps({"manifest": str(manifest), "samples": source_samples,
                                    "fbxSha256": exports["fbxSha256"],
                                    "glbSha256": exports["glbSha256"]}, separators=(",", ":")))


if __name__ == "__main__":
    main()
