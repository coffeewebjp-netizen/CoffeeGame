"""Judgment preview using image empties so Solid viewport shows the girl."""

from __future__ import annotations

import math
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parent
LOOK = ROOT / "look-previews"
BLEND = ROOT / "girl1-look-preview.blend"
RENDERS = ROOT / "renders" / "look-turntable"


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 1280
    scene.frame_start = 1
    scene.frame_end = 48
    world = bpy.data.worlds.new("LookWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.18, 0.20, 1.0)


def image_card(name: str, path: Path, location, rotation, height: float):
    image = bpy.data.images.load(str(path))
    aspect = image.size[0] / max(1, image.size[1])
    width = height * aspect
    bpy.ops.object.empty_add(type="IMAGE", location=location, rotation=rotation)
    empty = bpy.context.object
    empty.name = name
    empty.data = image
    empty.empty_display_size = height
    empty.empty_image_side = "DOUBLE_SIDED"
    empty.show_in_front = True

    bpy.ops.mesh.primitive_plane_add(size=1.0, location=location, rotation=rotation)
    plane = bpy.context.object
    plane.name = name + ".Mesh"
    plane.scale = (width * 0.5, height * 0.5, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mat = bpy.data.materials.new(name + "Mat")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    principled = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    links.new(tex.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Roughness"].default_value = 1.0
    plane.data.materials.append(mat)
    return empty


def main():
    reset()
    image_card("Look.34", LOOK / "look-34.jpg", (0.0, 0.15, 0.81), (math.radians(90), 0, 0), 1.62)
    image_card(
        "Look.Right",
        LOOK / "look-right.jpg",
        (0.15, 0.0, 0.81),
        (math.radians(90), 0, math.radians(90)),
        1.62,
    )
    image_card("Look.Bust", LOOK / "look-bust.jpg", (1.8, 0.15, 1.15), (math.radians(90), 0, 0), 1.1)

    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0.81))
    pivot = bpy.context.object
    pivot.name = "TurntablePivot"
    bpy.ops.object.camera_add(location=(0.35, -2.8, 1.05), rotation=(math.radians(84), 0, math.radians(8)))
    cam = bpy.context.object
    cam.name = "LookCamera"
    cam.data.lens = 45
    cam.parent = pivot
    bpy.context.scene.camera = cam
    for area in bpy.context.screen.areas:
        if area.type == "VIEW_3D":
            area.spaces[0].region_3d.view_perspective = "CAMERA"
            area.spaces[0].shading.type = "SOLID"
            area.spaces[0].shading.color_type = "TEXTURE"

    pivot.rotation_euler = (0, 0, 0)
    pivot.keyframe_insert("rotation_euler", frame=1)
    pivot.rotation_euler = (0, 0, math.radians(180))
    pivot.keyframe_insert("rotation_euler", frame=48)
    if pivot.animation_data and pivot.animation_data.action:
        for fcu in pivot.animation_data.action.fcurves:
            for kp in fcu.keyframe_points:
                kp.interpolation = "LINEAR"

    bpy.ops.object.light_add(type="SUN", location=(1, -2, 3))
    bpy.context.object.data.energy = 2.0

    RENDERS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 92
    for frame, name in ((1, "turn-00"), (16, "turn-60"), (25, "turn-90"), (40, "turn-150")):
        scene.frame_set(frame)
        scene.render.filepath = str(RENDERS / f"{name}.jpg")
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND)


if __name__ == "__main__":
    main()
