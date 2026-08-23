"""Prepare the Meshy girl1 FBX for the CoffeeGAME Trial slot.

Separates the fused katana, parents it rigidly to RightHand, names Idle/Walk,
grounds the actor, and exports FBX. Does not replace heroine-v4 or HD-2D sources.
"""

from __future__ import annotations

import json
import math
import shutil
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
PACK = ROOT / "art" / "3d" / "trials" / "meshy-girl1"
DROP = PACK / "drop"
SRC_FBX = DROP / "Meshy_AI_Azure_Blade_Maiden_biped_Animation_Walking_withSkin.fbx"
BLEND = PACK / "source" / "girl1-meshy-walk.blend"
EXPORT_FBX = PACK / "export" / "girl1-meshy-walk.fbx"
UNITY_FBX = (
    ROOT
    / "unity"
    / "CoffeeGame"
    / "Assets"
    / "CoffeeGame"
    / "Resources"
    / "Models"
    / "Hero"
    / "trial-anime-girl.fbx"
)
UNITY_BACKUP = PACK / "archive" / "previous-trial-anime-girl.fbx"
PREVIEWS = PACK / "previews"
MANIFEST = PACK / "manifests" / "girl1-meshy-walk.json"
TARGET_HEIGHT = 1.62
TEX = {
    "base": DROP / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0.png",
    "metallic": DROP / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_metallic.png",
    "normal": DROP / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_normal.png",
    "roughness": DROP / "Meshy_AI_Azure_Blade_Maiden_biped_texture_0_roughness.png",
}


def reset() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    scene.render.fps = 30
    world = bpy.data.worlds.new("MeshyTrialWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.14, 0.15, 0.17, 1.0)
    bg.inputs[1].default_value = 1.0


def import_fbx():
    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX), automatic_bone_orientation=True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH")
    arm.name = "MeshyGirl1Rig"
    mesh.name = "MeshyGirl1"
    mesh.data.name = "MeshyGirl1"
    bpy.context.view_layer.update()
    return arm, mesh


def world_bbox_objects(objects):
    pts = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        for corner in obj.bound_box:
            pts.append(obj.matrix_world @ Vector(corner))
    xs, ys, zs = [p.x for p in pts], [p.y for p in pts], [p.z for p in pts]
    return Vector((min(xs), min(ys), min(zs))), Vector((max(xs), max(ys), max(zs)))


def ground_and_scale(arm, meshes):
    bpy.context.view_layer.update()
    lo, hi = world_bbox_objects(meshes)
    height = max(0.001, hi.z - lo.z)
    scale = TARGET_HEIGHT / height
    arm.scale = (
        arm.scale.x * scale,
        arm.scale.y * scale,
        arm.scale.z * scale,
    )
    bpy.context.view_layer.update()
    lo, hi = world_bbox_objects(meshes)
    arm.location.x -= (lo.x + hi.x) * 0.5
    arm.location.y -= (lo.y + hi.y) * 0.5
    arm.location.z -= lo.z
    bpy.context.view_layer.update()


def evaluated_world_positions(mesh_obj):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    eval_obj = mesh_obj.evaluated_get(depsgraph)
    eval_mesh = eval_obj.to_mesh()
    mat = eval_obj.matrix_world
    coords = [mat @ vert.co.copy() for vert in eval_mesh.vertices]
    eval_obj.to_mesh_clear()
    return coords


def separate_sword(mesh, arm) -> bpy.types.Object:
    bpy.context.view_layer.update()
    hand_pos = arm.matrix_world @ arm.data.bones["RightHand"].head_local
    coords = [mesh.matrix_world @ vert.co for vert in mesh.data.vertices]
    tip_index = max(range(len(coords)), key=lambda i: (coords[i] - hand_pos).length)
    tip = coords[tip_index]
    blade_len = (tip - hand_pos).length
    if blade_len < 0.25:
        raise RuntimeError(f"Sword tip too close to hand ({blade_len:.3f}m)")

    adjacency = [[] for _ in range(len(mesh.data.vertices))]
    for edge in mesh.data.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)

    axis = tip - hand_pos
    axis_len = axis.length
    chosen = []
    seen = {tip_index}
    queue = [tip_index]
    radius = 0.055
    while queue:
        index = queue.pop()
        pos = coords[index]
        t = max(0.0, min(1.0, (pos - hand_pos).dot(axis) / (axis_len * axis_len)))
        proj = hand_pos + axis * t
        dist = (pos - proj).length
        if dist > radius:
            continue
        chosen.append(index)
        for neighbor in adjacency[index]:
            if neighbor in seen:
                continue
            seen.add(neighbor)
            npos = coords[neighbor]
            nt = max(0.0, min(1.0, (npos - hand_pos).dot(axis) / (axis_len * axis_len)))
            nproj = hand_pos + axis * nt
            if (npos - nproj).length <= radius:
                queue.append(neighbor)

    if len(chosen) < 80:
        raise RuntimeError(f"Sword island too small ({len(chosen)} verts)")

    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="DESELECT")
    bpy.ops.object.mode_set(mode="OBJECT")
    for index in chosen:
        mesh.data.vertices[index].select = True
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.separate(type="SELECTED")
    bpy.ops.object.mode_set(mode="OBJECT")

    sword = next(
        obj
        for obj in bpy.data.objects
        if obj.type == "MESH" and obj != mesh and obj.name.startswith("MeshyGirl1")
    )
    sword.name = "MeshyGirl1Sword"
    sword.data.name = "MeshyGirl1Sword"
    for mod in list(sword.modifiers):
        sword.modifiers.remove(mod)
    sword.vertex_groups.clear()
    return sword


def parent_sword_to_hand(sword, arm):
    bpy.ops.object.select_all(action="DESELECT")
    sword.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    arm.data.bones.active = arm.data.bones["RightHand"]
    bpy.ops.object.parent_set(type="BONE_RELATIVE")
    bpy.context.view_layer.update()
    if sword.parent_bone != "RightHand":
        raise RuntimeError(f"Sword parent bone is {sword.parent_bone!r}")


def assign_textures(mesh, sword):
    if not TEX["base"].exists():
        return
    mat = mesh.active_material or bpy.data.materials.new("MeshyGirl1_PBR")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    principled = next(node for node in nodes if node.type == "BSDF_PRINCIPLED")
    image = bpy.data.images.load(str(TEX["base"]), check_existing=True)
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs["Color"], principled.inputs["Base Color"])
    if TEX["roughness"].exists():
        rough_img = bpy.data.images.load(str(TEX["roughness"]), check_existing=True)
        rough = nodes.new("ShaderNodeTexImage")
        rough.image = rough_img
        rough.image.colorspace_settings.name = "Non-Color"
        links.new(rough.outputs["Color"], principled.inputs["Roughness"])
    if TEX["metallic"].exists():
        metal_img = bpy.data.images.load(str(TEX["metallic"]), check_existing=True)
        metal = nodes.new("ShaderNodeTexImage")
        metal.image = metal_img
        metal.image.colorspace_settings.name = "Non-Color"
        links.new(metal.outputs["Color"], principled.inputs["Metallic"])
    if TEX["normal"].exists():
        nimg = bpy.data.images.load(str(TEX["normal"]), check_existing=True)
        ntex = nodes.new("ShaderNodeTexImage")
        ntex.image = nimg
        ntex.image.colorspace_settings.name = "Non-Color"
        nmap = nodes.new("ShaderNodeNormalMap")
        links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        links.new(nmap.outputs["Normal"], principled.inputs["Normal"])
    if mesh.data.materials:
        mesh.data.materials[0] = mat
    else:
        mesh.data.materials.append(mat)
    if sword.data.materials:
        sword.data.materials[0] = mat
    else:
        sword.data.materials.append(mat)


def rename_actions(arm):
    if not bpy.data.actions:
        raise RuntimeError("No actions in FBX")
    walk = bpy.data.actions[0]
    walk.name = "Walk"
    if arm.animation_data is None:
        arm.animation_data_create()
    scene = bpy.context.scene
    start = int(walk.frame_range[0])
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    idle = bpy.data.actions.new(name="Idle")
    arm.animation_data.action = idle
    for bone in arm.pose.bones:
        bone.keyframe_insert("location", frame=1)
        bone.keyframe_insert("rotation_quaternion", frame=1)
        bone.keyframe_insert("scale", frame=1)
    bpy.ops.object.mode_set(mode="OBJECT")
    run = walk.copy()
    run.name = "Run"
    arm.animation_data.action = walk
    scene.frame_start = start
    scene.frame_end = int(walk.frame_range[1])


def make_walk_inplace(arm):
    action = bpy.data.actions.get("Walk")
    if action is None:
        return
    hip_curves = [
        fcurve
        for fcurve in action.fcurves
        if fcurve.data_path.endswith("location") and "Hips" in fcurve.data_path
    ]
    if not hip_curves:
        hip_curves = [
            fcurve
            for fcurve in action.fcurves
            if fcurve.data_path == "location"
        ]
    # Zero planar hip travel; keep vertical bob (index 2 after Blender Z-up).
    for fcurve in hip_curves:
        if fcurve.array_index in (0, 1):
            rest = fcurve.keyframe_points[0].co[1] if fcurve.keyframe_points else 0.0
            for key in fcurve.keyframe_points:
                key.co[1] = rest
            fcurve.update()


def add_studio():
    scene = bpy.context.scene
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.68, 0.74, 1.0)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
    ground_mat = bpy.data.materials.new("TrialGround")
    ground_mat.use_nodes = True
    ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.78, 0.76, 0.72, 1)
    bpy.ops.mesh.primitive_plane_add(size=10, location=(0, 0, -0.002))
    ground = bpy.context.object
    ground.name = "PreviewOnly_Ground"
    ground.data.materials.append(ground_mat)


def point_camera(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_still(path: Path, location, frame=1):
    scene = bpy.context.scene
    scene.frame_set(frame)
    cam_data = bpy.data.cameras.new("TrialCam")
    cam_data.lens = 40
    cam = bpy.data.objects.new("TrialCam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = location
    point_camera(cam, (0.0, 0.0, 0.82))
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def render_previews(arm):
    add_studio()
    if arm.animation_data is None:
        arm.animation_data_create()
    arm.animation_data.action = bpy.data.actions["Idle"]
    render_still(PREVIEWS / "front.jpg", (0.0, -3.35, 1.05), int(bpy.data.actions["Idle"].frame_range[0]))
    render_still(PREVIEWS / "three-quarter.jpg", (2.15, -2.75, 1.15), int(bpy.data.actions["Idle"].frame_range[0]))
    walk = bpy.data.actions["Walk"]
    arm.animation_data.action = walk
    mid = int((walk.frame_range[0] + walk.frame_range[1]) * 0.5)
    render_still(PREVIEWS / "walk-mid.jpg", (2.15, -2.75, 1.15), mid)
    render_still(PREVIEWS / "walk-side.jpg", (3.35, 0.0, 1.05), mid)


def export_fbx():
    EXPORT_FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"} and not obj.name.startswith("PreviewOnly"):
            obj.select_set(True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT_FBX),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_step=1.0,
        path_mode="COPY",
        embed_textures=False,
    )


def copy_to_unity():
    UNITY_FBX.parent.mkdir(parents=True, exist_ok=True)
    if UNITY_FBX.exists() and not UNITY_BACKUP.exists():
        UNITY_BACKUP.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(UNITY_FBX, UNITY_BACKUP)
    shutil.copy2(EXPORT_FBX, UNITY_FBX)
    for src in TEX.values():
        if src.exists():
            shutil.copy2(src, UNITY_FBX.parent / src.name)


def write_manifest(arm, mesh, sword, extra):
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    lo, hi = world_bbox_objects([mesh, sword])
    data = {
        "schemaVersion": 1,
        "asset": "trial-meshy-girl1",
        "status": "meshy-walk-trial",
        "taskId": "ORC-20260823-004",
        "source": str(SRC_FBX.relative_to(ROOT)).replace("\\", "/"),
        "blend": str(BLEND.relative_to(ROOT)).replace("\\", "/"),
        "exportFbx": str(EXPORT_FBX.relative_to(ROOT)).replace("\\", "/"),
        "unityFbx": str(UNITY_FBX.relative_to(ROOT)).replace("\\", "/"),
        "actions": sorted(action.name for action in bpy.data.actions),
        "bones": [bone.name for bone in arm.data.bones],
        "bodyTriangles": sum(len(p.vertices) - 2 for p in mesh.data.polygons),
        "swordTriangles": sum(len(p.vertices) - 2 for p in sword.data.polygons),
        "heightMeters": hi.z - lo.z,
        "origin": "ground-center",
        "swordParentBone": "RightHand",
        "notes": extra,
        "playableHeroine": "HD-2D unchanged; enable CoffeeGAME > Trial > Use anime-girl 3D",
    }
    MANIFEST.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    return data


def main():
    if not SRC_FBX.exists():
        raise FileNotFoundError(SRC_FBX)
    reset()
    arm, mesh = import_fbx()
    sword = separate_sword(mesh, arm)
    parent_sword_to_hand(sword, arm)
    ground_and_scale(arm, [mesh, sword])
    assign_textures(mesh, sword)
    rename_actions(arm)
    make_walk_inplace(arm)
    extra = {
        "swordVerts": len(sword.data.vertices),
        "bodyVerts": len(mesh.data.vertices),
    }
    BLEND.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_previews(arm)
    export_fbx()
    copy_to_unity()
    data = write_manifest(arm, mesh, sword, extra)
    print(json.dumps(data, indent=2))


if __name__ == "__main__":
    main()
