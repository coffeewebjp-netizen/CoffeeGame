"""Build a Blender 4.5 prototype for girl1. Does not touch the Unity heroine."""

from __future__ import annotations

import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
REFS = ROOT / "refs"
RENDERS = ROOT / "renders"
BLEND = ROOT / "girl1-prototype.blend"
GLB = ROOT / "girl1-prototype.glb"


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    scene.render.fps = 24
    scene.frame_start = 1
    scene.frame_end = 24
    scene.frame_current = 1
    world = bpy.data.worlds.new("Girl1World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.16, 0.16, 0.18, 1.0)
    bg.inputs[1].default_value = 1.0


def add_ref(name: str, filename: str, location, rotation, size: float) -> None:
    path = REFS / filename
    image = bpy.data.images.load(str(path))
    bpy.ops.object.empty_add(type="IMAGE", location=location, rotation=rotation)
    empty = bpy.context.object
    empty.name = name
    empty.data = image
    empty.empty_display_size = size
    empty.empty_image_side = "FRONT"
    empty.show_in_front = False
    empty.hide_render = True


def mat(name: str, color) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes["Principled BSDF"]
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = 0.55
    return material


def add_mesh(name: str, primitive: str, location, scale, material, **kwargs):
    op = getattr(bpy.ops.mesh, f"primitive_{primitive}_add")
    op(location=location, **kwargs)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    return obj


def build_blockout():
    skin = mat("Skin", (0.90, 0.74, 0.66))
    hair = mat("Hair", (0.35, 0.78, 0.88))
    kimono = mat("Kimono", (0.62, 0.14, 0.16))
    skirt = mat("Skirt", (0.86, 0.68, 0.18))
    wood = mat("Wood", (0.28, 0.18, 0.10))
    steel = mat("Steel", (0.72, 0.74, 0.78))
    parts = [
        add_mesh("Head", "uv_sphere", (0, 0, 1.50), (0.11, 0.12, 0.13), skin),
        add_mesh("HairCap", "uv_sphere", (0, -0.01, 1.54), (0.125, 0.14, 0.12), hair),
        add_mesh("HairBack", "uv_sphere", (0, -0.06, 1.44), (0.11, 0.12, 0.14), hair),
        add_mesh("Neck", "cylinder", (0, 0, 1.36), (0.04, 0.04, 0.05), skin, vertices=12),
        add_mesh("Chest", "cube", (0, 0, 1.18), (0.17, 0.10, 0.14), kimono),
        add_mesh("Sleeve.L", "cube", (0.20, 0.02, 1.12), (0.08, 0.10, 0.10), kimono),
        add_mesh("Sleeve.R", "cube", (-0.20, 0.02, 1.12), (0.08, 0.10, 0.10), kimono),
        add_mesh("Waist", "cube", (0, 0, 1.00), (0.14, 0.09, 0.08), kimono),
        add_mesh("Obi", "cube", (0, 0.02, 0.94), (0.15, 0.08, 0.04), wood),
        add_mesh("Hips", "cube", (0, 0, 0.86), (0.16, 0.10, 0.08), skirt),
        add_mesh("Skirt", "cone", (0, 0, 0.66), (0.22, 0.16, 0.20), skirt, vertices=16),
        add_mesh("Thigh.L", "cylinder", (0.06, 0, 0.58), (0.055, 0.06, 0.16), skin, vertices=12),
        add_mesh("Thigh.R", "cylinder", (-0.06, 0, 0.58), (0.055, 0.06, 0.16), skin, vertices=12),
        add_mesh("Shin.L", "cylinder", (0.06, 0, 0.28), (0.045, 0.05, 0.14), skin, vertices=12),
        add_mesh("Shin.R", "cylinder", (-0.06, 0, 0.28), (0.045, 0.05, 0.14), skin, vertices=12),
        add_mesh("Foot.L", "cube", (0.06, 0.05, 0.06), (0.05, 0.12, 0.03), wood),
        add_mesh("Foot.R", "cube", (-0.06, 0.05, 0.06), (0.05, 0.12, 0.03), wood),
        add_mesh("UpperArm.L", "cylinder", (0.28, 0, 1.20), (0.035, 0.035, 0.13), skin, vertices=10),
        add_mesh("UpperArm.R", "cylinder", (-0.28, 0, 1.20), (0.035, 0.035, 0.13), skin, vertices=10),
        add_mesh("Forearm.L", "cylinder", (0.48, 0, 1.20), (0.03, 0.03, 0.11), skin, vertices=10),
        add_mesh("Forearm.R", "cylinder", (-0.48, 0, 1.20), (0.03, 0.03, 0.11), skin, vertices=10),
        add_mesh("Hand.L", "uv_sphere", (0.60, 0, 1.20), (0.035, 0.025, 0.04), skin),
        add_mesh("Hand.R", "uv_sphere", (-0.60, 0, 1.20), (0.035, 0.025, 0.04), skin),
        add_mesh("Saya", "cylinder", (0.16, 0.08, 0.92), (0.018, 0.018, 0.28), wood, vertices=10),
        add_mesh("Blade", "cube", (0.16, 0.08, 1.18), (0.012, 0.004, 0.16), steel),
    ]
    for arm in ("UpperArm.L", "UpperArm.R", "Forearm.L", "Forearm.R"):
        obj = bpy.data.objects[arm]
        obj.rotation_euler = (0, math.radians(90), 0)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        obj.select_set(False)

    for name, rot in (("Saya", (0, math.radians(70), math.radians(20))), ("Blade", (0, math.radians(70), math.radians(20)))):
        obj = bpy.data.objects[name]
        obj.rotation_euler = rot
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        obj.select_set(False)

    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "Girl1Body"
    bpy.ops.object.shade_smooth()
    return body


def bone(arm, name, head, tail, parent=None):
    bone = arm.edit_bones.new(name)
    bone.head = Vector(head)
    bone.tail = Vector(tail)
    if parent is not None:
        bone.parent = arm.edit_bones[parent]
        bone.use_connect = False
    return bone


def build_armature():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm_obj = bpy.context.object
    arm_obj.name = "Girl1Rig"
    arm = arm_obj.data
    arm.name = "Girl1RigData"
    for existing in list(arm.edit_bones):
        arm.edit_bones.remove(existing)
    bone(arm, "Hips", (0, 0, 0.86), (0, 0, 1.00))
    bone(arm, "Spine", (0, 0, 1.00), (0, 0, 1.14), "Hips")
    bone(arm, "Chest", (0, 0, 1.14), (0, 0, 1.32), "Spine")
    bone(arm, "Neck", (0, 0, 1.32), (0, 0, 1.40), "Chest")
    bone(arm, "Head", (0, 0, 1.40), (0, 0, 1.62), "Neck")
    bone(arm, "Thigh.L", (0.06, 0, 0.86), (0.06, 0, 0.50), "Hips")
    bone(arm, "Shin.L", (0.06, 0, 0.50), (0.06, 0, 0.16), "Thigh.L")
    bone(arm, "Foot.L", (0.06, 0, 0.16), (0.06, 0.10, 0.04), "Shin.L")
    bone(arm, "Thigh.R", (-0.06, 0, 0.86), (-0.06, 0, 0.50), "Hips")
    bone(arm, "Shin.R", (-0.06, 0, 0.50), (-0.06, 0, 0.16), "Thigh.R")
    bone(arm, "Foot.R", (-0.06, 0, 0.16), (-0.06, 0.10, 0.04), "Shin.R")
    bone(arm, "UpperArm.L", (0.16, 0, 1.24), (0.40, 0, 1.20), "Chest")
    bone(arm, "Forearm.L", (0.40, 0, 1.20), (0.58, 0, 1.20), "UpperArm.L")
    bone(arm, "Hand.L", (0.58, 0, 1.20), (0.66, 0, 1.20), "Forearm.L")
    bone(arm, "UpperArm.R", (-0.16, 0, 1.24), (-0.40, 0, 1.20), "Chest")
    bone(arm, "Forearm.R", (-0.40, 0, 1.20), (-0.58, 0, 1.20), "UpperArm.R")
    bone(arm, "Hand.R", (-0.58, 0, 1.20), (-0.66, 0, 1.20), "Forearm.R")
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def bind(body, armature):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def key_euler(pose_bone, frame, euler):
    pose_bone.rotation_mode = "XYZ"
    pose_bone.rotation_euler = euler
    pose_bone.keyframe_insert(data_path="rotation_euler", frame=frame)


def add_walk(armature):
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.mode_set(mode="POSE")
    pose = armature.pose
    deg = math.radians
    cycle = {
        "Thigh.L": [(1, 28), (7, 8), (13, -28), (19, -8), (25, 28)],
        "Thigh.R": [(1, -28), (7, -8), (13, 28), (19, 8), (25, -28)],
        "Shin.L": [(1, 8), (7, 42), (13, 10), (19, 6), (25, 8)],
        "Shin.R": [(1, 10), (7, 6), (13, 8), (19, 42), (25, 10)],
        "UpperArm.L": [(1, -18), (13, 18), (25, -18)],
        "UpperArm.R": [(1, 18), (13, -18), (25, 18)],
        "Forearm.L": [(1, 12), (13, 28), (25, 12)],
        "Forearm.R": [(1, 28), (13, 12), (25, 28)],
    }
    for name, keys in cycle.items():
        bone = pose.bones[name]
        for frame, angle in keys:
            if name.startswith("Shin") or name.startswith("Forearm"):
                key_euler(bone, frame, (deg(angle), 0, 0))
            elif name.startswith("UpperArm"):
                key_euler(bone, frame, (0, 0, deg(angle)))
            else:
                key_euler(bone, frame, (deg(angle), 0, 0))
    hips = pose.bones["Hips"]
    for frame, z in ((1, 0.86), (7, 0.82), (13, 0.86), (19, 0.82), (25, 0.86)):
        hips.location = (0, 0, z - 0.86)
        hips.keyframe_insert(data_path="location", frame=frame)
    action = armature.animation_data.action
    action.name = "WalkInPlace"
    for fcurve in action.fcurves:
        for kp in fcurve.keyframe_points:
            kp.interpolation = "BEZIER"
    bpy.ops.object.mode_set(mode="OBJECT")


def add_camera_light():
    bpy.ops.object.light_add(type="AREA", location=(1.6, -2.2, 2.2))
    light = bpy.context.object
    light.data.energy = 250
    light.data.size = 1.6
    bpy.ops.object.light_add(type="AREA", location=(-1.4, 1.8, 1.6))
    fill = bpy.context.object
    fill.data.energy = 80
    fill.data.size = 2.0
    bpy.ops.object.camera_add(location=(0, -3.2, 0.95), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object
    cam.name = "PreviewCamera"
    cam.data.lens = 50
    bpy.context.scene.camera = cam


def render_preview():
    RENDERS.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    scene.render.filepath = str(RENDERS / "preview-front.jpg")
    bpy.ops.render.render(write_still=True)


def export_glb():
    bpy.ops.export_scene.gltf(
        filepath=str(GLB),
        export_format="GLB",
        export_animations=True,
        export_nla_strips=False,
        export_apply=False,
    )


def main() -> None:
    reset_scene()
    add_ref("Ref.Front", "tpose-front.jpg", (0.0, -1.15, 0.81), (math.radians(90), 0, 0), 1.62)
    add_ref("Ref.Right", "tpose-right.jpg", (1.15, 0.0, 0.81), (math.radians(90), 0, math.radians(90)), 1.62)
    add_ref("Ref.Back", "tpose-back.jpg", (0.0, 1.15, 0.81), (math.radians(90), 0, math.radians(180)), 1.62)
    body = build_blockout()
    armature = build_armature()
    bind(body, armature)
    add_walk(armature)
    add_camera_light()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_preview()
    export_glb()
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND)
    print("Wrote", GLB)


if __name__ == "__main__":
    main()
