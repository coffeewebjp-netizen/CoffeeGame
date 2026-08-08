"""Generate CoffeeGAME's first production 3D slime asset.

Run with Blender, not regular Python:
    blender --background --python tools/blender/generate_slime.py
    blender --background --python tools/blender/generate_slime.py -- --validate

The asset is authored Z-up and facing Blender -Y. The FBX axis conversion makes
that Unity +Z-forward / +Y-up while keeping the character origin on the ground.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO_ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = REPO_ROOT / "art" / "3d" / "source" / "slime-v1.blend"
FBX_PATH = (
    REPO_ROOT
    / "unity"
    / "CoffeeGame"
    / "Assets"
    / "CoffeeGame"
    / "Resources"
    / "Models"
    / "Slime"
    / "slime-v1.fbx"
)
MANIFEST_PATH = REPO_ROOT / "art" / "3d" / "manifests" / "slime-v1.json"
PREVIEW_PATH = REPO_ROOT / "art" / "3d" / "previews" / "slime-v1.png"
ACTION_NAMES = ("Idle", "Move", "Windup", "Attack", "Hurt", "Defeated")


def set_input(material: bpy.types.Material, name: str, value) -> None:
    node = material.node_tree.nodes.get("Principled BSDF")
    if node and name in node.inputs:
        node.inputs[name].default_value = value


def material(name: str, color, *, metallic=0.0, roughness=0.35, alpha=1.0,
             transmission=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    set_input(mat, "Base Color", color)
    set_input(mat, "Metallic", metallic)
    set_input(mat, "Roughness", roughness)
    set_input(mat, "IOR", 1.34)
    set_input(mat, "Alpha", alpha)
    set_input(mat, "Transmission Weight", transmission)
    if emission is not None:
        set_input(mat, "Emission Color", emission)
        set_input(mat, "Emission Strength", emission_strength)
    mat.diffuse_color = (color[0], color[1], color[2], alpha)
    if alpha < 1.0:
        # Blender 4.2+ replaced blend_method with surface_render_method.
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "DITHERED"
        mat.use_transparency_overlap = False
    return mat


def make_body_mesh(name: str):
    # Deliberately irregular rings make a readable low-poly silhouette while
    # remaining close to 0.75 m wide and 0.55 m tall.
    rings = (
        (0.000, 0.210),
        (0.035, 0.330),
        (0.115, 0.372),
        (0.235, 0.365),
        (0.360, 0.315),
        (0.470, 0.225),
        (0.535, 0.105),
        (0.550, 0.000),
    )
    segments = 20
    verts = []
    for ring_index, (z, radius) in enumerate(rings):
        if radius == 0.0:
            verts.append((0.0, 0.0, z))
            continue
        for segment in range(segments):
            angle = (math.tau * segment / segments) + (ring_index % 2) * 0.035
            wobble = 1.0 + 0.018 * math.sin(segment * 3.0 + ring_index)
            x = radius * wobble * math.cos(angle)
            y = radius * 0.84 * (1.0 + 0.012 * math.cos(segment * 2.0)) * math.sin(angle)
            verts.append((x, y, z))

    faces = []
    ring_count = len(rings) - 1
    for ring in range(ring_count - 1):
        start = ring * segments
        next_start = (ring + 1) * segments
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((start + segment, start + nxt, next_start + nxt, next_start + segment))
    cap_index = len(verts) - 1
    last_ring_start = (ring_count - 1) * segments
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((last_ring_start + segment, last_ring_start + nxt, cap_index))

    # The lowest ring is closed so the model has no open/non-manifold bottom.
    bottom_center = len(verts)
    verts.append((0.0, 0.0, 0.0))
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom_center, nxt, segment))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("SoftLowPolyEdges", "BEVEL")
    bevel.width = 0.006
    bevel.segments = 2
    return obj


def make_uv_part(name: str, location, scale, mat, segments=16, rings=8):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments,
        ring_count=rings,
        location=location,
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def make_mouth(mat):
    curve = bpy.data.curves.new("Mouth_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = 0.008
    curve.bevel_resolution = 2
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(2)
    points = ((-0.045, -0.318, 0.215), (0.0, -0.325, 0.200), (0.045, -0.318, 0.215))
    for bezier, coordinate in zip(spline.bezier_points, points):
        bezier.co = coordinate
        bezier.handle_left_type = "AUTO"
        bezier.handle_right_type = "AUTO"
    obj = bpy.data.objects.new("Mouth", curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    return obj


def create_armature():
    armature_data = bpy.data.armatures.new("SlimeRig")
    armature = bpy.data.objects.new("SlimeRig", armature_data)
    bpy.context.collection.objects.link(armature)
    armature.show_in_front = True
    armature.data.display_type = "STICK"
    armature["unity_rig_type"] = "Generic"
    armature["forward_axis"] = "+Z (after FBX conversion)"

    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    root = armature_data.edit_bones.new("Root")
    root.head = (0.0, 0.0, 0.0)
    root.tail = (0.0, 0.0, 0.08)
    body = armature_data.edit_bones.new("Body")
    body.head = (0.0, 0.0, 0.08)
    body.tail = (0.0, 0.0, 0.52)
    body.parent = root
    body.use_connect = True
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)
    return armature


def skin_body(obj, armature):
    group = obj.vertex_groups.new(name="Body")
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("SlimeRig", "ARMATURE")
    modifier.object = armature
    obj.parent = armature


def bone_parent(obj, armature):
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = "Body"
    obj.matrix_world = world


def key_pose(armature, frame, *, scale=(1.0, 1.0, 1.0), location=(0.0, 0.0, 0.0),
             rotation=(0.0, 0.0, 0.0)):
    bpy.context.scene.frame_set(frame)
    bone = armature.pose.bones["Body"]
    bone.rotation_mode = "XYZ"
    bone.scale = scale
    bone.location = location
    bone.rotation_euler = rotation
    bone.keyframe_insert(data_path="scale", frame=frame, group="Body")
    bone.keyframe_insert(data_path="location", frame=frame, group="Body")
    bone.keyframe_insert(data_path="rotation_euler", frame=frame, group="Body")


def create_action(armature, name, poses, fps=30):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    armature.animation_data_create()
    armature.animation_data.action = action
    for frame, values in poses:
        key_pose(armature, frame, **values)
    action.asset_mark()
    action["in_place"] = True
    action["fps"] = fps
    return action


def create_actions(armature):
    actions = []
    actions.append(create_action(armature, "Idle", (
        (1, {}),
        (16, {"scale": (1.035, 1.035, 0.965), "location": (0.0, 0.0, -0.003)}),
        (31, {}),
    )))
    actions.append(create_action(armature, "Move", (
        (1, {"scale": (1.08, 1.08, 0.89), "rotation": (0.0, 0.05, 0.0)}),
        (5, {"scale": (0.94, 0.94, 1.10), "location": (0.0, 0.0, 0.018), "rotation": (0.0, -0.04, 0.0)}),
        (9, {"scale": (1.08, 1.08, 0.89), "rotation": (0.0, 0.05, 0.0)}),
        (13, {"scale": (0.94, 0.94, 1.10), "location": (0.0, 0.0, 0.018), "rotation": (0.0, -0.04, 0.0)}),
        (17, {"scale": (1.08, 1.08, 0.89), "rotation": (0.0, 0.05, 0.0)}),
    )))
    actions.append(create_action(armature, "Windup", (
        (1, {}),
        (10, {"scale": (1.10, 1.10, 0.82), "location": (0.0, 0.0, -0.010)}),
        (20, {"scale": (1.19, 1.19, 0.68), "location": (0.0, 0.0, -0.018)}),
    )))
    actions.append(create_action(armature, "Attack", (
        (1, {"scale": (1.18, 1.18, 0.70), "location": (0.0, 0.0, -0.015)}),
        (5, {"scale": (0.78, 0.78, 1.38), "location": (0.0, -0.035, 0.040)}),
        (9, {"scale": (1.25, 0.80, 0.78), "location": (0.0, -0.025, 0.005)}),
        (16, {}),
    )))
    actions.append(create_action(armature, "Hurt", (
        (1, {}),
        (4, {"scale": (0.82, 1.12, 0.93), "rotation": (0.0, 0.15, 0.0)}),
        (7, {"scale": (1.12, 0.86, 0.95), "rotation": (0.0, -0.10, 0.0)}),
        (12, {}),
    )))
    actions.append(create_action(armature, "Defeated", (
        (1, {}),
        (8, {"scale": (1.10, 1.10, 0.82), "rotation": (0.08, 0.0, 0.0)}),
        (16, {"scale": (1.30, 1.30, 0.38), "location": (0.0, 0.0, -0.025)}),
        (28, {"scale": (1.48, 1.48, 0.16), "location": (0.0, 0.0, -0.045)}),
    )))
    armature.animation_data.action = actions[0]
    return actions


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    scene.frame_start = 1
    scene.frame_end = 31
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.view_settings.look = "AgX - Medium High Contrast"

    cyan = material(
        "Slime_Cyan_Gel",
        (0.002, 0.18, 0.68, 1.0),
        metallic=0.03,
        roughness=0.12,
        alpha=0.93,
        transmission=0.11,
    )
    amber = material(
        "Slime_Amber_Eyes",
        (0.62, 0.075, 0.002, 1.0),
        metallic=0.10,
        roughness=0.24,
        emission=(1.0, 0.055, 0.0, 1.0),
        emission_strength=0.05,
    )
    dark = material("Slime_Eye_Core", (0.025, 0.012, 0.018, 1.0), roughness=0.27)
    highlight = material(
        "Slime_Eye_Highlight",
        (1.0, 0.72, 0.22, 1.0),
        roughness=0.15,
        emission=(1.0, 0.42, 0.04, 1.0),
        emission_strength=1.2,
    )

    body = make_body_mesh("SlimeBody")
    body.data.materials.append(cyan)
    body["dimensions_m"] = "0.75 wide x 0.55 tall"

    parts = []
    for side in (-1.0, 1.0):
        eye_x = side * 0.125
        parts.append(make_uv_part(
            f"Eye_{'L' if side < 0 else 'R'}",
            (eye_x, -0.296, 0.310),
            (0.068, 0.018, 0.086),
            amber,
        ))
        parts.append(make_uv_part(
            f"Pupil_{'L' if side < 0 else 'R'}",
            (eye_x, -0.313, 0.305),
            (0.028, 0.010, 0.054),
            dark,
            segments=12,
            rings=6,
        ))
        parts.append(make_uv_part(
            f"EyeHighlight_{'L' if side < 0 else 'R'}",
            (eye_x - 0.011, -0.324, 0.336),
            (0.010, 0.006, 0.014),
            highlight,
            segments=8,
            rings=4,
        ))
    armature = create_armature()
    skin_body(body, armature)
    for part in parts:
        bone_parent(part, armature)
    actions = create_actions(armature)

    # Keep export selection deterministic and avoid future helper objects leaking.
    for obj in bpy.context.scene.objects:
        obj.select_set(obj == armature or obj == body or obj in parts)
    bpy.context.view_layer.objects.active = armature
    return body, parts, armature, actions


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_preview():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.image_settings.color_depth = "8"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("SlimePreviewWorld")
    scene.world.color = (0.018, 0.025, 0.040)
    world_node = scene.world.node_tree.nodes.get("Background") if scene.world.use_nodes else None
    if world_node:
        world_node.inputs["Color"].default_value = (0.012, 0.020, 0.038, 1.0)
        world_node.inputs["Strength"].default_value = 0.28

    helpers = []
    bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, -0.008))
    floor = bpy.context.object
    floor.name = "PreviewFloor"
    floor_mat = material("PreviewFloorMaterial", (0.018, 0.035, 0.055, 1.0), roughness=0.24)
    floor.data.materials.append(floor_mat)
    helpers.append(floor)

    camera_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.15, -1.75, 0.92)
    camera.data.lens = 66
    camera.data.sensor_width = 36
    aim_at(camera, (0.0, 0.0, 0.265))
    scene.camera = camera
    helpers.append(camera)

    light_specs = (
        ("PreviewKey", (1.0, -1.4, 1.55), 390.0, (0.62, 0.90, 1.0), 1.25),
        ("PreviewRim", (-1.2, 0.45, 1.15), 480.0, (0.10, 0.55, 1.0), 0.85),
        ("PreviewWarm", (0.85, 0.55, 0.62), 190.0, (1.0, 0.34, 0.08), 0.65),
    )
    for name, location, energy, color, size in light_specs:
        data = bpy.data.lights.new(name, "AREA")
        data.energy = energy
        data.color = color
        data.shape = "DISK"
        data.size = size
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        aim_at(light, (0.0, 0.0, 0.25))
        helpers.append(light)

    scene.frame_set(1)
    bpy.ops.render.render(write_still=True)

    scene.camera = None
    for obj in helpers:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.materials.remove(floor_mat)


def collect_counts():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    # Blender's FBX importer qualifies takes as Rig|Rig|Action. Normalize that
    # only for reporting so the check matches the clip names Unity will expose.
    actions = [action for action in bpy.data.actions if action.name.split("|")[-1] in ACTION_NAMES]
    return {
        "objects": len(bpy.context.scene.objects),
        "meshObjects": len(meshes),
        "vertices": sum(len(obj.data.vertices) for obj in meshes),
        "polygons": sum(len(obj.data.polygons) for obj in meshes),
        "materials": len(bpy.data.materials),
        "armatures": len(armatures),
        "bones": sum(len(obj.data.bones) for obj in armatures),
        "actions": len(actions),
        "actionNames": sorted(action.name.split("|")[-1] for action in actions),
    }


def action_manifest(actions):
    clips = []
    for action in actions:
        start, end = action.frame_range
        clips.append({
            "name": action.name,
            "startFrame": int(round(start)),
            "endFrame": int(round(end)),
            "fps": 30,
            "loop": action.name in {"Idle", "Move"},
            "inPlace": True,
        })
    return sorted(clips, key=lambda item: ACTION_NAMES.index(item["name"]))


def generate():
    for path in (BLEND_PATH.parent, FBX_PATH.parent, MANIFEST_PATH.parent, PREVIEW_PATH.parent):
        path.mkdir(parents=True, exist_ok=True)
    _, _, armature, actions = build_scene()
    render_preview()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "CURVE", "ARMATURE"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        check_existing=False,
        use_selection=True,
        object_types={"ARMATURE", "MESH", "OTHER"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        primary_bone_axis="Y",
        secondary_bone_axis="X",
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO",
        embed_textures=False,
    )

    manifest = {
        "schemaVersion": 1,
        "assetId": "slime-v1",
        "displayName": "Cyan Slime",
        "generator": "tools/blender/generate_slime.py",
        "blenderVersion": bpy.app.version_string,
        "source": str(BLEND_PATH.relative_to(REPO_ROOT)).replace("\\", "/"),
        "unityFbx": str(FBX_PATH.relative_to(REPO_ROOT)).replace("\\", "/"),
        "preview": str(PREVIEW_PATH.relative_to(REPO_ROOT)).replace("\\", "/"),
        "orientation": {
            "unityForward": "+Z",
            "unityUp": "+Y",
            "origin": "ground center",
        },
        "dimensionsMeters": {"width": 0.75, "depth": 0.63, "height": 0.55},
        "rig": {"type": "Generic", "rootBone": "Root", "deformBone": "Body"},
        "clips": action_manifest(actions),
        "sourceCounts": collect_counts(),
        "files": {
            "blendBytes": BLEND_PATH.stat().st_size,
            "fbxBytes": FBX_PATH.stat().st_size,
            "previewBytes": PREVIEW_PATH.stat().st_size,
        },
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print("COFFEEGAME_GENERATED=" + json.dumps(manifest, ensure_ascii=False))


def validate():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    source_counts = collect_counts()
    source_actions = set(source_counts["actionNames"])
    missing = set(ACTION_NAMES) - source_actions
    if missing:
        raise RuntimeError(f"Blend validation failed; missing actions: {sorted(missing)}")
    if source_counts["bones"] != 2:
        raise RuntimeError(f"Blend validation failed; expected 2 bones: {source_counts}")

    preview = bpy.data.images.load(str(PREVIEW_PATH), check_existing=False)
    preview_size = [int(preview.size[0]), int(preview.size[1])]
    bpy.data.images.remove(preview)
    if preview_size != [768, 768]:
        raise RuntimeError(f"Preview validation failed; expected 768x768: {preview_size}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_anim=True)
    fbx_counts = collect_counts()
    imported_names = set(fbx_counts["actionNames"])
    missing_fbx = set(ACTION_NAMES) - imported_names
    if missing_fbx:
        raise RuntimeError(f"FBX validation failed; missing actions: {sorted(missing_fbx)}")

    result = {
        "source": source_counts,
        "fbxReimport": fbx_counts,
        "blendBytes": BLEND_PATH.stat().st_size,
        "fbxBytes": FBX_PATH.stat().st_size,
        "preview": {"size": preview_size, "bytes": PREVIEW_PATH.stat().st_size},
    }
    print("COFFEEGAME_VALIDATION=" + json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    if "--validate" in sys.argv:
        validate()
    else:
        generate()
