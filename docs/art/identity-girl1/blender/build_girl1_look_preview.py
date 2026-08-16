"""Judgment preview: approved 3D-look cards only. No clay mesh."""

from __future__ import annotations

import math
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parent
ISO = ROOT / "look-previews" / "isolated"
RENDERS = ROOT / "renders" / "look-turntable"
BLEND = ROOT / "girl1-look-preview.blend"


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.film_transparent = True
    scene.render.resolution_x = 960
    scene.render.resolution_y = 1280
    scene.render.fps = 24
    scene.frame_start = 1
    scene.frame_end = 36
    world = bpy.data.worlds.new("LookWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.12, 0.12, 0.14, 1.0)


def image_plane(name: str, path: Path, location, rotation, height: float):
    image = bpy.data.images.load(str(path))
    aspect = image.size[0] / max(1, image.size[1])
    width = height * aspect
    bpy.ops.mesh.primitive_plane_add(size=1.0, location=location, rotation=rotation)
    plane = bpy.context.object
    plane.name = name
    plane.scale = (width / 2.0, height / 2.0, 1.0)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mat = bpy.data.materials.new(name + "Mat")
    mat.use_nodes = True
    mat.blend_method = "BLEND"
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    mix = nodes.new("ShaderNodeMixShader")
    trans = nodes.new("ShaderNodeBsdfTransparent")
    emit = nodes.new("ShaderNodeEmission")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Smart"
    links.new(tex.outputs["Color"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    links.new(tex.outputs["Alpha"], mix.inputs["Fac"])
    links.new(trans.outputs["BSDF"], mix.inputs[1])
    links.new(emit.outputs["Emission"], mix.inputs[2])
    links.new(mix.outputs["Shader"], out.inputs["Surface"])
    plane.data.materials.append(mat)
    return plane


def main():
    reset()
    image_plane("Look.34", ISO / "look-34.png", (0.0, 0.0, 0.81), (math.radians(90), 0, 0), 1.62)
    image_plane(
        "Look.Right",
        ISO / "look-right.png",
        (0.0, 0.0, 0.81),
        (math.radians(90), 0, math.radians(90)),
        1.62,
    )
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0.81))
    pivot = bpy.context.object
    pivot.name = "TurntablePivot"
    bpy.ops.object.camera_add(location=(0, -2.7, 0.95), rotation=(math.radians(86), 0, 0))
    cam = bpy.context.object
    cam.name = "LookCamera"
    cam.data.lens = 50
    cam.parent = pivot
    bpy.context.scene.camera = cam
    pivot.rotation_euler = (0, 0, 0)
    pivot.keyframe_insert("rotation_euler", frame=1)
    pivot.rotation_euler = (0, 0, math.radians(180))
    pivot.keyframe_insert("rotation_euler", frame=36)
    for fcu in pivot.animation_data.action.fcurves:
        for kp in fcu.keyframe_points:
            kp.interpolation = "LINEAR"
    bpy.ops.object.light_add(type="AREA", location=(0, -2.2, 2.2))
    bpy.context.object.data.energy = 40

    RENDERS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    for frame, name in ((1, "turn-00"), (10, "turn-50"), (19, "turn-90"), (28, "turn-140")):
        scene.frame_set(frame)
        scene.render.filepath = str(RENDERS / f"{name}.jpg")
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND)


if __name__ == "__main__":
    main()
