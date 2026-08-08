"""Generate CoffeeGAME's flattened, URP-safe cyan slime v2.

Run:
  blender -b --python tools/blender/generate_slime_v2.py
  blender -b --python tools/blender/generate_slime_v2.py -- --validate

Source orientation is Z-up / -Y-forward. FBX conversion produces Unity
+Y-up / +Z-forward. The eyes are authored on source -Y, never on a side.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_slime as v1  # noqa: E402


BLEND_PATH = ROOT / "art" / "3d" / "source" / "slime-v2.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Slime" / "slime-v2.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "slime-v2.json"
VALIDATION_PATH = ROOT / "art" / "3d" / "manifests" / "slime-v2-fbx-validation.json"
FRONT_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "slime-v2-front.png"
GAME_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "slime-v2-game-camera.png"
ACTION_NAMES = v1.ACTION_NAMES


def set_input(material, name, value):
    node = material.node_tree.nodes.get("Principled BSDF") if material.use_nodes else None
    if node and name in node.inputs:
        node.inputs[name].default_value = value


def material(name, color, hex_color, role, *, roughness=0.4, metallic=0.0, emission=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = color
    set_input(mat, "Base Color", color)
    set_input(mat, "Roughness", roughness)
    set_input(mat, "Metallic", metallic)
    if emission is not None:
        set_input(mat, "Emission Color", emission)
        set_input(mat, "Emission Strength", emission_strength)
    mat["unity_shader_hint"] = "Universal Render Pipeline/Lit"
    mat["unity_base_color_srgb"] = hex_color
    mat["coffee_material_role"] = role
    mat["unity_surface"] = "Opaque"
    return mat


def make_body_mesh():
    # Wide base + low crown: unmistakably a gel puddle, not a sphere.
    rings = (
        (0.000, 0.235, 0.70),
        (0.024, 0.360, 0.72),
        (0.080, 0.390, 0.72),
        (0.165, 0.382, 0.73),
        (0.265, 0.342, 0.75),
        (0.355, 0.270, 0.77),
        (0.425, 0.160, 0.80),
        (0.465, 0.000, 1.00),
    )
    segments = 28
    vertices = []
    ring_starts = []
    for ring_index, (z, radius, depth_ratio) in enumerate(rings):
        ring_starts.append(len(vertices))
        if radius == 0.0:
            vertices.append((0.0, 0.0, z))
            continue
        for segment in range(segments):
            angle = math.tau * segment / segments + (ring_index % 2) * 0.018
            # A restrained wobble keeps the silhouette organic without facets.
            wobble = 1.0 + 0.012 * math.sin(segment * 3.0 + ring_index * 0.7)
            vertices.append((radius * wobble * math.cos(angle),
                             radius * depth_ratio * (1.0 + 0.008 * math.cos(segment * 2.0)) * math.sin(angle), z))

    faces = []
    for ring_index in range(len(rings) - 2):
        first, second = ring_starts[ring_index], ring_starts[ring_index + 1]
        for segment in range(segments):
            nxt = (segment + 1) % segments
            faces.append((first + segment, first + nxt, second + nxt, second + segment))
    top = ring_starts[-1]
    final_ring = ring_starts[-2]
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((final_ring + segment, final_ring + nxt, top))
    bottom_center = len(vertices)
    vertices.append((0.0, 0.0, 0.0))
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((bottom_center, nxt, segment))

    mesh = bpy.data.meshes.new("SlimeBodyV2Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("SlimeBodyV2", mesh)
    bpy.context.collection.objects.link(obj)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("GelSoftness", "BEVEL")
    bevel.width = 0.004
    bevel.segments = 2
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    subdivision = obj.modifiers.new("GelSurface", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 1
    bpy.ops.object.modifier_apply(modifier=subdivision.name)
    obj.select_set(False)
    return obj


def uv_part(name, location, scale, mat, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    return obj


def recalc_outside(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    obj["outside_normals_recalculated"] = True


def signed_volume(obj):
    obj.data.calc_loop_triangles()
    result = 0.0
    for triangle in obj.data.loop_triangles:
        a, b, c = (obj.data.vertices[index].co for index in triangle.vertices)
        result += a.dot(b.cross(c)) / 6.0
    return result


def skin_piece(obj, armature):
    group = obj.vertex_groups.new(name="Body")
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("SlimeRig", "ARMATURE")
    modifier.object = armature
    modifier.use_deform_preserve_volume = True


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    scene.frame_start, scene.frame_end = 1, 31
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0

    mats = {
        # Opaque by design: FBX/URP does not reproduce Blender transmission.
        # A modest emission floor prevents the cobalt-black v1 failure.
        "body": material("CG_Slime_Body_BrightCyan_URP", (0.008, 0.25, 0.68, 1), "#42CFFF", "body-cyan",
                         roughness=0.30, emission=(0.005, 0.08, 0.22, 1), emission_strength=0.05),
        "amber": material("CG_Slime_Eye_Amber_URP", (0.72, 0.095, 0.004, 1), "#FF941C", "eye-amber", roughness=0.26),
        "pupil": material("CG_Slime_Eye_Core_URP", (0.016, 0.008, 0.022, 1), "#1C1223", "eye-core", roughness=0.38),
        "spark": material("CG_Slime_Eye_Spark_URP", (1.00, 0.94, 0.70, 1), "#FFF0B5", "eye-spark", roughness=0.18,
                          emission=(1.0, 0.78, 0.30, 1), emission_strength=0.40),
    }

    body = make_body_mesh()
    body.data.materials.append(mats["body"])
    parts = []
    # Face is source -Y, which FBX maps to Unity +Z.
    for side, sign in (("L", -1), ("R", 1)):
        eye_x = sign * 0.125
        parts.append(uv_part(f"EyeAmber.{side}", (eye_x, -0.274, 0.267), (0.064, 0.017, 0.080), mats["amber"]))
        parts.append(uv_part(f"EyeCore.{side}", (eye_x, -0.291, 0.264), (0.025, 0.009, 0.049), mats["pupil"], 16, 10))
        parts.append(uv_part(f"EyeSpark.{side}", (eye_x - 0.010, -0.301, 0.294), (0.009, 0.004, 0.012), mats["spark"], 12, 8))
    # The body uses ordinary URP specular response; no separate forehead oval,
    # which read as a third eye in the first v2 review render.

    armature = v1.create_armature()
    armature.name = "SlimeRigV2"
    armature.data.name = "SlimeRigV2"
    armature["source_forward"] = "-Y"
    armature["unity_forward"] = "+Z"
    armature["front_feature"] = "amber eyes on source -Y"
    for obj in [body, *parts]:
        recalc_outside(obj)
        volume = signed_volume(obj)
        obj["signed_volume_after_recalc"] = float(volume)
        if volume < -1e-8:
            raise RuntimeError(f"Outside-normal QA failed for {obj.name}: {volume}")
        skin_piece(obj, armature)
    actions = v1.create_actions(armature)
    armature.animation_data.action = None
    bpy.context.scene.frame_set(1)
    return body, parts, armature, actions, mats


def aim_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_preview(path, camera_location, target, resolution, lens):
    scene = bpy.context.scene
    helpers = []
    floor_mat = material("PreviewOnly_SlimeFloor", (0.06, 0.075, 0.095, 1), "#334050", "preview", roughness=0.90)
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, -0.006))
    floor = bpy.context.object
    floor.name = "PreviewOnly_Floor"
    floor.data.materials.append(floor_mat)
    helpers.append(floor)

    camera_data = bpy.data.cameras.new("PreviewOnly_Camera")
    camera = bpy.data.objects.new("PreviewOnly_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_location
    camera.data.lens = lens
    aim_at(camera, target)
    scene.camera = camera
    helpers.append(camera)

    world = scene.world or bpy.data.worlds.new("SlimeV2PreviewWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.055, 0.080, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.52
    for name, location, energy, size, color in (
        ("PreviewOnly_Key", (-1.4, -1.8, 1.65), 440, 1.5, (0.55, 0.88, 1.0)),
        ("PreviewOnly_Fill", (1.4, -1.0, 1.0), 300, 1.3, (1.0, 0.58, 0.28)),
        ("PreviewOnly_Rim", (0.0, 1.3, 1.2), 500, 1.2, (0.15, 0.70, 1.0)),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        aim_at(light, target)
        helpers.append(light)

    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(path)
    scene.frame_set(1)
    bpy.ops.render.render(write_still=True)
    scene.camera = None
    for helper in helpers:
        bpy.data.objects.remove(helper, do_unlink=True)
    bpy.data.materials.remove(floor_mat)


def collect_counts():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    normalized_actions = sorted(action.name.split("|")[-1] for action in bpy.data.actions if action.name.split("|")[-1] in ACTION_NAMES)
    return {
        "objects": len(bpy.context.scene.objects),
        "meshObjects": len(meshes),
        "vertices": sum(len(obj.data.vertices) for obj in meshes),
        "triangles": sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes),
        "materials": len({mat for obj in meshes for mat in obj.data.materials if mat}),
        "armatures": len(rigs),
        "bones": sum(len(rig.data.bones) for rig in rigs),
        "actions": len(normalized_actions),
        "actionNames": normalized_actions,
    }


def action_manifest(actions):
    result = []
    for action in actions:
        start, end = action.frame_range
        result.append({
            "name": action.name,
            "startFrame": int(round(start)),
            "endFrame": int(round(end)),
            "fps": 30,
            "loop": action.name in {"Idle", "Move"},
            "inPlace": True,
        })
    return sorted(result, key=lambda clip: ACTION_NAMES.index(clip["name"]))


def generate():
    for path in (BLEND_PATH.parent, FBX_PATH.parent, MANIFEST_PATH.parent, FRONT_PREVIEW_PATH.parent):
        path.mkdir(parents=True, exist_ok=True)
    _, _, armature, actions, mats = build_scene()
    render_preview(FRONT_PREVIEW_PATH, (0.0, -2.25, 0.78), (0, 0, 0.23), (900, 760), 65)
    render_preview(GAME_PREVIEW_PATH, (1.25, -1.80, 1.38), (0, 0, 0.20), (900, 760), 63)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False, compress=True)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH), check_existing=False, use_selection=True,
        object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        use_space_transform=True, use_mesh_modifiers=True, add_leaf_bones=False,
        primary_bone_axis="Y", secondary_bone_axis="X",
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
        path_mode="AUTO", embed_textures=False,
    )
    palette = {
        mat.name: {"baseColor": mat["unity_base_color_srgb"], "role": mat["coffee_material_role"], "shader": mat["unity_shader_hint"]}
        for mat in mats.values()
    }
    manifest = {
        "schemaVersion": 2,
        "assetId": "slime-v2",
        "displayName": "Bright Cyan Gel Slime",
        "generator": "tools/blender/generate_slime_v2.py",
        "blenderVersion": bpy.app.version_string,
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "unityFbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "previews": {
            "front": str(FRONT_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
            "gameCamera": str(GAME_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
        },
        "fbxValidationReport": str(VALIDATION_PATH.relative_to(ROOT)).replace("\\", "/"),
        "orientation": {
            "sourceForward": "-Y (eyes lie on this face)",
            "unityForward": "+Z",
            "sourceUp": "+Z",
            "unityUp": "+Y",
            "origin": "ground center",
        },
        "dimensionsMeters": {"width": 0.78, "depth": 0.56, "height": 0.465},
        "shapeContract": "squashed gel; width/height ratio 1.68; never spherical",
        "rig": {"type": "Generic", "rootBone": "Root", "deformBone": "Body"},
        "clips": action_manifest(actions),
        "counts": collect_counts(),
        "materialPalette": palette,
        "normalQA": {"recalculatedOutsideBeforeExport": True, "backfaceCullSafe": True},
        "validation": {"fbxReimportPassed": False},
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("SLIME_V2_GENERATED=" + json.dumps(manifest, ensure_ascii=False))


def validate():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    source_counts = collect_counts()
    missing_source = sorted(set(ACTION_NAMES) - set(source_counts["actionNames"]))
    if source_counts["bones"] != 2 or missing_source:
        raise RuntimeError("Slime v2 source validation failed: " + json.dumps({"counts": source_counts, "missing": missing_source}))
    source_normal_failures = [obj.name for obj in bpy.context.scene.objects if obj.type == "MESH" and signed_volume(obj) < -1e-8]
    if source_normal_failures:
        raise RuntimeError("Slime v2 source backface QA failed: " + json.dumps(source_normal_failures))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_anim=True)
    fbx_counts = collect_counts()
    missing_fbx = sorted(set(ACTION_NAMES) - set(fbx_counts["actionNames"]))
    fbx_normal_failures = [obj.name for obj in bpy.context.scene.objects if obj.type == "MESH" and signed_volume(obj) < -1e-8]
    passed = not missing_fbx and fbx_counts["bones"] == 2 and not fbx_normal_failures
    result = {
        "passed": passed,
        "source": source_counts,
        "fbxReimport": fbx_counts,
        "missingActions": missing_fbx,
        "outsideNormalQA": {
            "sourceNegativeMeshes": source_normal_failures,
            "fbxNegativeMeshes": fbx_normal_failures,
            "backfaceCullSafe": not source_normal_failures and not fbx_normal_failures,
        },
    }
    VALIDATION_PATH.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest["validation"] = {"fbxReimportPassed": passed, "report": str(VALIDATION_PATH.relative_to(ROOT)).replace("\\", "/")}
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("SLIME_V2_VALIDATION=" + json.dumps(result, ensure_ascii=False))
    if not passed:
        raise RuntimeError("Slime v2 FBX reimport validation failed")


if __name__ == "__main__":
    if "--validate" in sys.argv:
        validate()
    else:
        generate()
