"""Generate CoffeeGAME's provisional stylized 3D heroine.

Run:
  blender -b --python tools/blender/generate_hero.py

Validate an already generated file:
  blender -b art/3d/source/heroine-v1.blend \
    --python tools/blender/generate_hero.py -- --validate-only

The Blender source is Z-up and faces -Y.  FBX export converts this to Unity's
Y-up, +Z-forward convention.  Geometry is intentionally simple and readable:
it is a game-ready rigged blockout designed to be refined without changing the
bone or action contract used by Unity.
"""

from __future__ import annotations

import json
import math
import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v1.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v1.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v1.json"
REFERENCE_PATH = ROOT / "art" / "3d" / "reference" / "hero-turnaround-v1.png"
PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v1.png"

ACTION_NAMES = [
    "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
    "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
    "Hurt", "Defeated",
]


def reset_scene() -> None:
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in (bpy.data.meshes, bpy.data.curves, bpy.data.armatures,
                       bpy.data.materials, bpy.data.actions, bpy.data.cameras,
                       bpy.data.lights):
        for datablock in list(collection):
            collection.remove(datablock)


def material(name: str, color: tuple[float, float, float, float], metallic=0.0, roughness=0.55):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return mat


def apply_transform(obj) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)


def finish_mesh(obj, mat, bone=None, smooth=True, bevel=0.0):
    obj.data.materials.append(mat)
    if smooth:
        for poly in obj.data.polygons:
            poly.use_smooth = True
    if bevel > 0:
        mod = obj.modifiers.new("SoftEdges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
    apply_transform(obj)
    if bone:
        bind_rigid(obj, bone)
    return obj


def bind_rigid(obj, bone_name: str) -> None:
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("HeroRig", "ARMATURE")
    mod.object = RIG
    mod.use_deform_preserve_volume = True


def uv(name, loc, scale, mat, bone, segments=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_mesh(obj, mat, bone)


def cube(name, loc, scale, mat, bone, rotation=(0, 0, 0), bevel=0.02):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish_mesh(obj, mat, bone, smooth=False, bevel=bevel)


def cylinder_between(name, start, end, radius, mat, bone, vertices=12, radius2=None):
    start, end = Vector(start), Vector(end)
    vec = end - start
    mid = (start + end) * 0.5
    if radius2 is None or abs(radius2 - radius) < 1e-6:
        bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=vec.length, location=mid)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius2, radius2=radius, depth=vec.length, location=mid)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(vec.normalized())
    obj.rotation_mode = "XYZ"
    return finish_mesh(obj, mat, bone)


def cone(name, loc, radius1, radius2, depth, mat, bone, vertices=12, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius1, radius2=radius2,
                                    depth=depth, location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return finish_mesh(obj, mat, bone)


def curve_tube(name, points, radius, mat, bone):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 2
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, co in zip(spline.bezier_points, points):
        point.co = co
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    return finish_mesh(obj, mat, bone)


def create_rig():
    arm = bpy.data.armatures.new("HeroineRig")
    rig = bpy.data.objects.new("HeroineRig", arm)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    rig["unity_forward"] = "+Z"
    rig["source_forward"] = "-Y"
    rig["character_height_m"] = 1.62
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None, connected=False, deform=True):
        b = arm.edit_bones.new(name)
        b.head, b.tail = head, tail
        b.use_deform = deform
        if parent:
            b.parent = arm.edit_bones[parent]
            b.use_connect = connected
        return b

    bone("Root", (0, 0, 0), (0, 0, 0.08), deform=False)
    bone("Pelvis", (0, 0, 0.76), (0, 0, 0.90), "Root")
    bone("Spine", (0, 0, 0.88), (0, 0, 1.08), "Pelvis")
    bone("Chest", (0, 0, 1.06), (0, 0, 1.25), "Spine")
    bone("Neck", (0, 0, 1.24), (0, 0, 1.32), "Chest")
    bone("Head", (0, 0, 1.31), (0, 0, 1.57), "Neck")
    for side, x in (("L", 1), ("R", -1)):
        bone(f"Thigh.{side}", (0.085*x, 0, 0.81), (0.095*x, 0, 0.48), "Pelvis")
        bone(f"Shin.{side}", (0.095*x, 0, 0.48), (0.105*x, 0, 0.16), f"Thigh.{side}", True)
        bone(f"Foot.{side}", (0.105*x, 0, 0.16), (0.105*x, -0.16, 0.075), f"Shin.{side}", True)
        bone(f"UpperArm.{side}", (0.145*x, 0, 1.21), (0.32*x, 0, 1.04), "Chest")
        bone(f"Forearm.{side}", (0.32*x, 0, 1.04), (0.41*x, 0, 0.83), f"UpperArm.{side}", True)
        bone(f"Hand.{side}", (0.41*x, 0, 0.83), (0.43*x, -0.005, 0.75), f"Forearm.{side}", True)
    bone("Weapon", (0.18, -0.015, 0.88), (0.35, -0.015, 1.08), "Pelvis", deform=True)
    bone("Sheath", (0.18, 0.035, 0.83), (0.30, 0.035, 0.36), "Pelvis", deform=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def build_model(m):
    skin, hair, hair_shadow, amber, pupil = m["skin"], m["hair"], m["hair_shadow"], m["amber"], m["pupil"]
    red, red_dark, white, orange = m["red"], m["red_dark"], m["white"], m["orange"]
    black, sole, steel, accent = m["black"], m["sole"], m["steel"], m["accent"]

    # Legs and shoes.  Limbs are tapered solids, not crossed planes.
    for side, x in (("L", 1), ("R", -1)):
        cylinder_between(f"Leg_Upper.{side}", (0.09*x, 0, 0.82), (0.095*x, 0, 0.49), 0.055, skin, f"Thigh.{side}", 12, 0.062)
        cylinder_between(f"Leg_Lower.{side}", (0.095*x, 0, 0.50), (0.105*x, 0, 0.16), 0.045, skin, f"Shin.{side}", 12, 0.055)
        cylinder_between(f"Sock.{side}", (0.105*x, 0, 0.205), (0.105*x, 0, 0.12), 0.052, black, f"Shin.{side}", 12)
        uv(f"Boot.{side}", (0.105*x, -0.055, 0.075), (0.073, 0.125, 0.06), black, f"Foot.{side}", 16, 8)
        cube(f"BootSole.{side}", (0.105*x, -0.06, 0.025), (0.075, 0.13, 0.018), sole, f"Foot.{side}", bevel=0.012)

    # Torso underlayers, obi, and skirt.
    uv("Torso", (0, 0, 1.08), (0.16, 0.095, 0.245), white, "Spine", 20, 12)
    uv("ChestShape", (0, -0.055, 1.17), (0.14, 0.055, 0.11), white, "Chest", 16, 10)
    cube("Obi", (0, 0, 0.91), (0.18, 0.095, 0.045), black, "Pelvis", bevel=0.012)
    cone("Skirt", (0, 0, 0.78), 0.265, 0.165, 0.31, orange, "Pelvis", vertices=16)
    for i in range(12):
        angle = 2 * math.pi * i / 12
        x, y = math.sin(angle)*0.222, math.cos(angle)*0.222
        cube(f"SkirtPleat.{i:02d}", (x, y, 0.76), (0.008, 0.018, 0.14), m["orange_dark"], "Pelvis",
             rotation=(0, 0, -angle), bevel=0.004)
    cube("ObiKnot", (0, -0.11, 0.90), (0.045, 0.025, 0.045), black, "Pelvis", rotation=(0, 0, math.radians(45)), bevel=0.01)
    for i, x in enumerate((-0.035, 0.035)):
        cube(f"ObiTail.{i}", (x, -0.112, 0.80), (0.027, 0.012, 0.105), black, "Pelvis", rotation=(0, 0, math.radians(7*(-1 if i else 1))), bevel=0.006)

    # Haori body and split fronts keep the kimono silhouette readable from all sides.
    cube("HaoriBack", (0, 0.065, 1.08), (0.205, 0.035, 0.29), red, "Spine", bevel=0.035)
    cube("HaoriFront.L", (0.115, -0.075, 1.08), (0.09, 0.025, 0.29), red, "Spine", rotation=(0, 0, math.radians(-2)), bevel=0.022)
    cube("HaoriFront.R", (-0.115, -0.075, 1.08), (0.09, 0.025, 0.29), red, "Spine", rotation=(0, 0, math.radians(2)), bevel=0.022)
    cube("HaoriCollar.L", (0.062, -0.105, 1.21), (0.025, 0.018, 0.18), red_dark, "Chest", rotation=(0, math.radians(-7), math.radians(-18)), bevel=0.008)
    cube("HaoriCollar.R", (-0.062, -0.105, 1.21), (0.025, 0.018, 0.18), red_dark, "Chest", rotation=(0, math.radians(7), math.radians(18)), bevel=0.008)

    # Arms, broad sleeves, gloves, and simple fingers.
    for side, x in (("L", 1), ("R", -1)):
        cylinder_between(f"UpperArm.{side}", (0.15*x, 0, 1.21), (0.32*x, 0, 1.04), 0.048, skin, f"UpperArm.{side}", 12, 0.06)
        cylinder_between(f"Forearm.{side}", (0.32*x, 0, 1.04), (0.41*x, 0, 0.83), 0.04, skin, f"Forearm.{side}", 12, 0.048)
        cylinder_between(f"HaoriSleeve.{side}", (0.16*x, 0.025, 1.20), (0.34*x, 0.025, 0.93), 0.105, red, f"UpperArm.{side}", 12, 0.16)
        uv(f"Hand.{side}", (0.42*x, -0.005, 0.79), (0.047, 0.035, 0.075), skin, f"Hand.{side}", 14, 8)
        cube(f"Glove.{side}", (0.405*x, -0.006, 0.84), (0.052, 0.042, 0.055), black, f"Hand.{side}", bevel=0.014)
        for finger in range(3):
            cylinder_between(f"Finger.{side}.{finger}", ((0.415+finger*0.009)*x, -0.012, 0.78),
                             ((0.425+finger*0.009)*x, -0.02, 0.73), 0.007, skin, f"Hand.{side}", 8)

    # Anime head, eyes, brows and small mouth.
    uv("Neck", (0, 0, 1.30), (0.055, 0.052, 0.09), skin, "Neck", 16, 10)
    uv("Head", (0, 0, 1.45), (0.145, 0.118, 0.17), skin, "Head", 24, 16)
    for side, x in (("L", 1), ("R", -1)):
        uv(f"Ear.{side}", (0.143*x, 0, 1.45), (0.022, 0.014, 0.035), skin, "Head", 12, 8)
        uv(f"EyeOutline.{side}", (0.055*x, -0.125, 1.475), (0.044, 0.009, 0.027), pupil, "Head", 16, 8)
        uv(f"EyeWhite.{side}", (0.055*x, -0.135, 1.475), (0.038, 0.007, 0.021), white, "Head", 16, 8)
        uv(f"Iris.{side}", (0.055*x, -0.143, 1.474), (0.016, 0.004, 0.017), amber, "Head", 14, 8)
        uv(f"Pupil.{side}", (0.055*x, -0.148, 1.474), (0.006, 0.0025, 0.011), pupil, "Head", 12, 6)
        curve_tube(f"UpperLash.{side}", [(0.014*x, -0.146, 1.486), (0.054*x, -0.151, 1.496), (0.097*x, -0.142, 1.486)], 0.0045, pupil, "Head")
        curve_tube(f"Brow.{side}", [(0.025*x, -0.132, 1.52), (0.058*x, -0.137, 1.525), (0.088*x, -0.130, 1.518)], 0.005, hair_shadow, "Head")
    uv("Nose", (0, -0.127, 1.44), (0.009, 0.008, 0.012), skin, "Head", 10, 6)
    curve_tube("Mouth", [(-0.025, -0.130, 1.405), (0, -0.135, 1.400), (0.025, -0.130, 1.405)], 0.0035, accent, "Head")

    # Blue cap, layered bob, tapered bangs, side locks, and ahoge.
    uv("HairCap", (0, 0.018, 1.48), (0.158, 0.125, 0.14), hair, "Head", 24, 14)
    for i in range(9):
        angle = math.radians(-110 + i*27.5)
        x = math.sin(angle)*0.13
        y = math.cos(angle)*0.095 + 0.025
        cylinder_between(f"BobLock.{i:02d}", (x, y, 1.50), (x*1.10, y*1.12, 1.34 + 0.025*(i%2)),
                         0.035, hair_shadow if i%3 == 0 else hair, "Head", 10, 0.052)
    for i, x in enumerate((-0.105, -0.070, -0.035, 0, 0.035, 0.070, 0.105)):
        end_z = 1.515 + 0.008*abs(i-3)
        cylinder_between(f"Bang.{i:02d}", (x*0.82, -0.105, 1.57), (x, -0.135, end_z),
                         0.010, hair, "Head", 8, 0.022)
    curve_tube("Ahoge", [(0, 0.01, 1.595), (0.015, -0.005, 1.635), (0.06, 0.00, 1.64), (0.075, 0.015, 1.61)], 0.007, hair, "Head")

    # Katana and sheath are separate, rigged objects.  At rest the blade overlaps
    # the sheath so the silhouette reads as a sheathed sword; animations move Weapon.
    sheath_start, sheath_end = (0.21, 0.09, 0.84), (0.33, 0.09, 0.37)
    cylinder_between("Sheath", sheath_start, sheath_end, 0.026, black, "Sheath", 12, 0.031)
    cylinder_between("SheathMouth", (0.205, 0.09, 0.86), (0.22, 0.09, 0.81), 0.034, sole, "Sheath", 12)
    cylinder_between("KatanaBlade", (0.21, 0.025, 0.83), (0.325, 0.025, 0.39), 0.010, steel, "Weapon", 8, 0.016)
    cylinder_between("KatanaGrip", (0.20, -0.055, 0.90), (0.14, -0.055, 1.08), 0.021, black, "Weapon", 10, 0.025)
    cylinder_between("GripWrapA", (0.185, -0.07, 0.945), (0.17, -0.07, 0.99), 0.004, accent, "Weapon", 6)
    cylinder_between("GripWrapB", (0.17, -0.07, 0.99), (0.155, -0.07, 1.035), 0.004, accent, "Weapon", 6)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.038, minor_radius=0.007, major_segments=12, minor_segments=6,
                                     location=(0.21, -0.03, 0.875), rotation=(math.radians(75), 0, math.radians(-15)))
    guard = bpy.context.object
    guard.name = "KatanaGuard"
    finish_mesh(guard, steel, "Weapon")


def set_pose_defaults():
    for pb in RIG.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.location = (0, 0, 0)
        pb.rotation_euler = (0, 0, 0)
        pb.scale = (1, 1, 1)


def insert(frame: int, values: dict[str, dict[str, tuple]]) -> None:
    for bone_name, channels in values.items():
        pb = RIG.pose.bones[bone_name]
        for channel, value in channels.items():
            setattr(pb, channel, value)
            pb.keyframe_insert(channel, frame=frame, group=bone_name)


def pose(rot=None, loc=None, scale=None):
    result = {}
    if rot is not None:
        result["rotation_euler"] = tuple(math.radians(v) for v in rot)
    if loc is not None:
        result["location"] = loc
    if scale is not None:
        result["scale"] = scale
    return result


def create_action(name: str, length: int, keys: list[tuple[int, dict]], cyclic=False):
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    RIG.animation_data_create()
    RIG.animation_data.action = action
    set_pose_defaults()
    for frame, values in keys:
        insert(frame, values)
    action.frame_range = (1, length)
    if cyclic:
        # Explicitly duplicate the first pose at the final frame; this survives FBX
        # export more reliably than modifier-only cycles in third-party importers.
        for fc in action.fcurves:
            fc.modifiers.new("CYCLES")
    for fc in action.fcurves:
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
    RIG.animation_data.action = None
    return action


def build_actions():
    arms_neutral = {"UpperArm.L": pose(rot=(0, -4, -3)), "UpperArm.R": pose(rot=(0, 4, 3))}
    create_action("Idle", 48, [
        (1, {**arms_neutral, "Chest": pose(loc=(0, 0, 0)), "Head": pose(rot=(0, 0, 0))}),
        (24, {**arms_neutral, "Chest": pose(loc=(0, 0, 0.008)), "Head": pose(rot=(1, 0, 1))}),
        (48, {**arms_neutral, "Chest": pose(loc=(0, 0, 0)), "Head": pose(rot=(0, 0, 0))}),
    ], True)
    create_action("Walk", 32, [
        (1, {"Thigh.L": pose(rot=(26, 0, 0)), "Thigh.R": pose(rot=(-26, 0, 0)), "UpperArm.L": pose(rot=(-18, 0, 0)), "UpperArm.R": pose(rot=(18, 0, 0)), "Pelvis": pose(loc=(0, 0, 0))}),
        (9, {"Thigh.L": pose(rot=(0, 0, 0)), "Thigh.R": pose(rot=(0, 0, 0)), "Pelvis": pose(loc=(0, 0, 0.018))}),
        (17, {"Thigh.L": pose(rot=(-26, 0, 0)), "Thigh.R": pose(rot=(26, 0, 0)), "UpperArm.L": pose(rot=(18, 0, 0)), "UpperArm.R": pose(rot=(-18, 0, 0)), "Pelvis": pose(loc=(0, 0, 0))}),
        (25, {"Thigh.L": pose(rot=(0, 0, 0)), "Thigh.R": pose(rot=(0, 0, 0)), "Pelvis": pose(loc=(0, 0, 0.018))}),
        (32, {"Thigh.L": pose(rot=(26, 0, 0)), "Thigh.R": pose(rot=(-26, 0, 0)), "UpperArm.L": pose(rot=(-18, 0, 0)), "UpperArm.R": pose(rot=(18, 0, 0)), "Pelvis": pose(loc=(0, 0, 0))}),
    ], True)
    create_action("Run", 24, [
        (1, {"Chest": pose(rot=(10, 0, 0)), "Thigh.L": pose(rot=(44, 0, 0)), "Thigh.R": pose(rot=(-38, 0, 0)), "UpperArm.L": pose(rot=(-38, 0, 0)), "UpperArm.R": pose(rot=(38, 0, 0))}),
        (7, {"Pelvis": pose(loc=(0, 0, 0.025)), "Thigh.L": pose(rot=(0, 0, 0)), "Thigh.R": pose(rot=(0, 0, 0))}),
        (13, {"Chest": pose(rot=(10, 0, 0)), "Thigh.L": pose(rot=(-38, 0, 0)), "Thigh.R": pose(rot=(44, 0, 0)), "UpperArm.L": pose(rot=(38, 0, 0)), "UpperArm.R": pose(rot=(-38, 0, 0))}),
        (19, {"Pelvis": pose(loc=(0, 0, 0.025)), "Thigh.L": pose(rot=(0, 0, 0)), "Thigh.R": pose(rot=(0, 0, 0))}),
        (24, {"Chest": pose(rot=(10, 0, 0)), "Thigh.L": pose(rot=(44, 0, 0)), "Thigh.R": pose(rot=(-38, 0, 0)), "UpperArm.L": pose(rot=(-38, 0, 0)), "UpperArm.R": pose(rot=(38, 0, 0))}),
    ], True)
    create_action("Jump", 22, [(1, {"Pelvis": pose(loc=(0, 0, -0.05)), "Thigh.L": pose(rot=(25, 0, 0)), "Thigh.R": pose(rot=(25, 0, 0)), "Shin.L": pose(rot=(-40, 0, 0)), "Shin.R": pose(rot=(-40, 0, 0))}), (10, {"Pelvis": pose(loc=(0, 0, 0.04)), "UpperArm.L": pose(rot=(-35, 0, 0)), "UpperArm.R": pose(rot=(-35, 0, 0))}), (22, {"Pelvis": pose(loc=(0, 0, 0.02),), "Thigh.L": pose(rot=(-10, 0, 0)), "Thigh.R": pose(rot=(14, 0, 0))})])
    create_action("Fall", 20, [(1, {"Chest": pose(rot=(-5, 0, 0)), "UpperArm.L": pose(rot=(-15, 0, -28)), "UpperArm.R": pose(rot=(-15, 0, 28)), "Thigh.L": pose(rot=(12, 0, 0)), "Shin.R": pose(rot=(-25, 0, 0))}), (20, {"Chest": pose(rot=(-2, 0, 0)), "UpperArm.L": pose(rot=(-8, 0, -35)), "UpperArm.R": pose(rot=(-8, 0, 35)), "Thigh.R": pose(rot=(12, 0, 0)), "Shin.L": pose(rot=(-25, 0, 0))})], True)
    create_action("Land", 18, [(1, {"Pelvis": pose(loc=(0, 0, 0.03))}), (7, {"Pelvis": pose(loc=(0, 0, -0.085)), "Chest": pose(rot=(18, 0, 0)), "Thigh.L": pose(rot=(28, 0, 0)), "Thigh.R": pose(rot=(28, 0, 0)), "Shin.L": pose(rot=(-42, 0, 0)), "Shin.R": pose(rot=(-42, 0, 0))}), (18, {"Pelvis": pose(loc=(0, 0, 0)), "Chest": pose(rot=(0, 0, 0))})])
    create_action("Sword", 28, [(1, {"Chest": pose(rot=(0, 0, -20)), "UpperArm.R": pose(rot=(-25, 20, 55)), "Forearm.R": pose(rot=(0, 0, 75)), "Weapon": pose(rot=(0, 0, 0))}), (10, {"Chest": pose(rot=(0, 0, 25)), "UpperArm.R": pose(rot=(-70, 0, -55)), "Forearm.R": pose(rot=(0, 0, -30)), "Weapon": pose(rot=(-55, 0, -100))}), (18, {"Chest": pose(rot=(8, 0, 35)), "UpperArm.R": pose(rot=(-35, 0, -80)), "Weapon": pose(rot=(-20, 0, -140))}), (28, arms_neutral)])
    create_action("AirSlash", 28, [(1, {"Thigh.L": pose(rot=(30, 0, 0)), "Shin.L": pose(rot=(-55, 0, 0)), "UpperArm.R": pose(rot=(-35, 0, 70)), "Weapon": pose(rot=(0, 0, 40))}), (13, {"Chest": pose(rot=(15, 0, 30)), "UpperArm.R": pose(rot=(-75, 0, -70)), "Weapon": pose(rot=(-65, 0, -130))}), (28, {"Chest": pose(rot=(0, 0, 0)), "Weapon": pose(rot=(0, 0, 0))})])
    create_action("Plunge", 30, [(1, {"UpperArm.L": pose(rot=(-50, 0, -20)), "UpperArm.R": pose(rot=(-50, 0, 20)), "Forearm.L": pose(rot=(0, 0, 50)), "Forearm.R": pose(rot=(0, 0, -50)), "Weapon": pose(rot=(0, 0, 0))}), (12, {"Chest": pose(rot=(25, 0, 0)), "Weapon": pose(rot=(0, 0, 170)), "Thigh.L": pose(rot=(30, 0, 0)), "Thigh.R": pose(rot=(30, 0, 0)), "Shin.L": pose(rot=(-55, 0, 0)), "Shin.R": pose(rot=(-55, 0, 0))}), (30, {"Chest": pose(rot=(20, 0, 0)), "Weapon": pose(rot=(0, 0, 170))})])
    create_action("SpinCharge", 36, [(1, {"Chest": pose(rot=(0, 0, 0)), "UpperArm.R": pose(rot=(0, 0, 0))}), (18, {"Pelvis": pose(rot=(0, 0, -35)), "Chest": pose(rot=(0, 0, -30)), "UpperArm.R": pose(rot=(-20, 0, 75)), "Weapon": pose(rot=(0, 0, 35))}), (36, {"Pelvis": pose(rot=(0, 0, -45)), "Chest": pose(rot=(0, 0, -35)), "UpperArm.R": pose(rot=(-25, 0, 80)), "Weapon": pose(rot=(0, 0, 45))})])
    create_action("SpinRelease", 30, [(1, {"Pelvis": pose(rot=(0, 0, -45)), "Chest": pose(rot=(0, 0, -35)), "Weapon": pose(rot=(0, 0, 45))}), (10, {"Pelvis": pose(rot=(0, 0, 110)), "Chest": pose(rot=(0, 0, 70)), "UpperArm.R": pose(rot=(-45, 0, -70)), "Weapon": pose(rot=(-30, 0, -120))}), (20, {"Pelvis": pose(rot=(0, 0, 260)), "Chest": pose(rot=(0, 0, 160)), "Weapon": pose(rot=(-30, 0, -260))}), (30, {"Pelvis": pose(rot=(0, 0, 360)), "Chest": pose(rot=(0, 0, 0)), "Weapon": pose(rot=(0, 0, -360))})])
    create_action("MagicCharge", 40, [(1, arms_neutral), (20, {"UpperArm.L": pose(rot=(-70, 0, -45)), "Forearm.L": pose(rot=(0, 0, -70)), "Hand.L": pose(rot=(0, -20, 0)), "Chest": pose(rot=(0, 0, 8))}), (40, {"UpperArm.L": pose(rot=(-75, 0, -50)), "Forearm.L": pose(rot=(0, 0, -75)), "Hand.L": pose(rot=(0, -30, 0)), "Chest": pose(rot=(0, 0, 10))})])
    create_action("MagicRelease", 24, [(1, {"UpperArm.L": pose(rot=(-75, 0, -50)), "Forearm.L": pose(rot=(0, 0, -75))}), (8, {"Chest": pose(rot=(8, 0, -12)), "UpperArm.L": pose(rot=(-100, 0, -15)), "Forearm.L": pose(rot=(0, 0, -5)), "Hand.L": pose(rot=(0, -40, 0))}), (24, arms_neutral)])
    create_action("Hurt", 22, [(1, {}), (6, {"Chest": pose(rot=(-18, 0, -12)), "Head": pose(rot=(-12, 0, 8)), "UpperArm.L": pose(rot=(20, 0, -20)), "UpperArm.R": pose(rot=(20, 0, 20)), "Pelvis": pose(loc=(0, 0.035, 0))}), (14, {"Chest": pose(rot=(8, 0, 6)), "Pelvis": pose(loc=(0, -0.015, 0))}), (22, {})])
    create_action("Defeated", 48, [(1, {}), (16, {"Chest": pose(rot=(22, 0, 18)), "Head": pose(rot=(15, 0, -15)), "Thigh.L": pose(rot=(40, 0, 0)), "Shin.L": pose(rot=(-55, 0, 0))}), (32, {"Pelvis": pose(rot=(0, 55, 5), loc=(0, 0, -0.18)), "Chest": pose(rot=(55, 0, 20)), "Head": pose(rot=(28, 0, -20)), "UpperArm.L": pose(rot=(20, 0, -55)), "UpperArm.R": pose(rot=(20, 0, 65))}), (48, {"Pelvis": pose(rot=(0, 82, 5), loc=(0, 0, -0.32)), "Chest": pose(rot=(65, 0, 20)), "Head": pose(rot=(35, 0, -20))})])


def make_manifest(validation=None):
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    arm = armatures[0] if armatures else None
    data = {
        "asset": "heroine-v1",
        "status": "provisional-game-ready-blockout",
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "reference": str(REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "fbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "preview": str(PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
        "units": "meters",
        "heightMeters": 1.62,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "counts": {
            "objects": len(bpy.data.objects),
            "meshObjects": len(meshes),
            "vertices": sum(len(o.data.vertices) for o in meshes),
            "armatures": len(armatures),
            "bones": len(arm.data.bones) if arm else 0,
            "actions": len(bpy.data.actions),
        },
        "actions": sorted(a.name for a in bpy.data.actions),
        "requiredActions": ACTION_NAMES,
        "separateProps": ["KatanaBlade", "KatanaGrip", "KatanaGuard", "Sheath"],
        "validation": validation or {"reopened": False},
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return data


def export_assets():
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.frame_start = 1
    bpy.context.scene.frame_end = 48
    bpy.context.scene.render.fps = 30
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.scene["asset_status"] = "provisional-game-ready-blockout"
    bpy.context.scene["unity_forward"] = "+Z"
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = RIG
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH), use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True, add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="AUTO", embed_textures=False,
    )
    render_preview()
    make_manifest()


def point_camera(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_preview():
    """Render a disposable neutral Eevee studio around the exported asset."""
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    temporary = []
    ground_mat = material("PreviewGround", (0.16, 0.17, 0.19, 1), roughness=0.9)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.002))
    ground = bpy.context.object
    ground.name = "PreviewOnly_Ground"
    ground.data.materials.append(ground_mat)
    temporary.append(ground)

    cam_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", cam_data)
    bpy.context.collection.objects.link(camera)
    camera.location = (1.7, -4.7, 1.72)
    cam_data.lens = 62
    point_camera(camera, (0, 0, 0.84))
    bpy.context.scene.camera = camera
    temporary.append(camera)

    world = bpy.context.scene.world or bpy.data.worlds.new("PreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.065, 0.085, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5

    def area(name, loc, energy, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = loc
        point_camera(obj, (0, 0, 0.9))
        temporary.append(obj)

    area("PreviewKey", (-2.0, -3.0, 3.7), 900, 3.0, (1.0, 0.82, 0.72))
    area("PreviewFill", (2.8, -1.5, 2.2), 650, 2.5, (0.45, 0.7, 1.0))
    area("PreviewRim", (0.2, 2.4, 2.8), 850, 2.0, (0.35, 0.65, 1.0))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str(PREVIEW_PATH)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)

    for obj in temporary:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.materials.remove(ground_mat)


def validate_current():
    mesh_count = sum(1 for o in bpy.data.objects if o.type == "MESH")
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    bone_count = len(arms[0].data.bones) if arms else 0
    actions = sorted(a.name for a in bpy.data.actions)
    missing = sorted(set(ACTION_NAMES) - set(actions))
    validation = {
        "reopened": True,
        "blendReadable": bool(bpy.data.filepath),
        "fbxExists": FBX_PATH.exists(),
        "previewExists": PREVIEW_PATH.exists(),
        "meshObjectCount": mesh_count,
        "armatureCount": len(arms),
        "boneCount": bone_count,
        "actionCount": len(actions),
        "missingRequiredActions": missing,
        "passed": mesh_count > 0 and len(arms) == 1 and bone_count >= 20 and not missing and FBX_PATH.exists() and PREVIEW_PATH.exists(),
    }
    data = make_manifest(validation)
    print("HERO_VALIDATION=" + json.dumps(validation, sort_keys=True))
    if not validation["passed"]:
        raise RuntimeError("Hero asset validation failed: " + json.dumps(validation))
    return data


def generate():
    global RIG
    reset_scene()
    mats = {
        "skin": material("Skin", (0.74, 0.40, 0.29, 1), roughness=0.62),
        "hair": material("HairBlue", (0.035, 0.34, 0.68, 1), metallic=0.02, roughness=0.42),
        "hair_shadow": material("HairShadow", (0.01, 0.12, 0.34, 1), roughness=0.5),
        "amber": material("AmberEyes", (1.0, 0.22, 0.003, 1), metallic=0.1, roughness=0.25),
        "pupil": material("Pupil", (0.035, 0.012, 0.008, 1), roughness=0.3),
        "red": material("HaoriRed", (0.34, 0.012, 0.045, 1), roughness=0.58),
        "red_dark": material("HaoriTrim", (0.12, 0.003, 0.015, 1), roughness=0.6),
        "white": material("TopWhite", (0.72, 0.74, 0.78, 1), roughness=0.7),
        "orange": material("SkirtOrange", (0.55, 0.13, 0.018, 1), roughness=0.65),
        "orange_dark": material("PleatShadow", (0.24, 0.035, 0.004, 1), roughness=0.7),
        "black": material("ClothBlack", (0.018, 0.022, 0.03, 1), roughness=0.55),
        "sole": material("BootSole", (0.006, 0.008, 0.012, 1), roughness=0.82),
        "steel": material("KatanaSteel", (0.44, 0.58, 0.65, 1), metallic=0.8, roughness=0.22),
        "accent": material("Accent", (0.29, 0.08, 0.11, 1), roughness=0.5),
    }
    RIG = create_rig()
    build_model(mats)
    build_actions()
    # Action construction leaves the final keyed pose in memory even after the
    # action is detached.  Save and preview the asset in a deterministic neutral
    # stance so Unity's imported default pose is useful.
    RIG.animation_data.action = None
    set_pose_defaults()
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    export_assets()
    print(f"Generated {BLEND_PATH}")
    print(f"Generated {FBX_PATH}")
    print(f"Generated {MANIFEST_PATH}")


if __name__ == "__main__":
    if "--validate-only" in sys.argv:
        validate_current()
    else:
        generate()
