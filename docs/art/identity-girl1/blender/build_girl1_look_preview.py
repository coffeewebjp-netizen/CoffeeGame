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
    scene.frame_end = 24
    scene.render.fps = 12
    scene.use_preview_range = False
    world = bpy.data.worlds.new("LookWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.18, 0.20, 1.0)


def load_image(path: Path, sequence_frames: int = 0):
    image = bpy.data.images.load(str(path))
    if sequence_frames > 1:
        image.source = "SEQUENCE"
        image.reload()
    return image


def apply_sequence(image, frames: int):
    if frames <= 1:
        return
    image.source = "SEQUENCE"


def image_card(name: str, path: Path, location, rotation, height: float, sequence_frames: int = 0):
    image = load_image(path, sequence_frames)
    aspect = image.size[0] / max(1, image.size[1])
    width = height * aspect
    bpy.ops.object.empty_add(type="IMAGE", location=location, rotation=rotation)
    empty = bpy.context.object
    empty.name = name
    empty.data = image
    empty.empty_display_size = height
    empty.empty_image_side = "DOUBLE_SIDED"
    empty.show_in_front = True
    if sequence_frames > 1 and hasattr(empty, "image_user"):
        empty.image_user.frame_duration = 240
        empty.image_user.use_auto_refresh = True
        empty.image_user.use_cyclic = True

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
    if sequence_frames > 1:
        tex.image_user.frame_duration = 240
        tex.image_user.frame_start = 1
        tex.image_user.frame_offset = 0
        tex.image_user.use_auto_refresh = True
        tex.image_user.use_cyclic = True
    links.new(tex.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Roughness"].default_value = 1.0
    plane.data.materials.append(mat)
    return empty


def key_hide(obj, frame: int, hidden: bool) -> None:
    obj.hide_viewport = hidden
    obj.hide_render = hidden
    obj.keyframe_insert("hide_viewport", frame=frame)
    obj.keyframe_insert("hide_render", frame=frame)
    if obj.animation_data and obj.animation_data.action:
        for fcu in obj.animation_data.action.fcurves:
            if fcu.data_path.startswith("hide_"):
                for kp in fcu.keyframe_points:
                    kp.interpolation = "CONSTANT"


def flipbook(entries) -> None:
    for obj, keys in entries:
        mesh = bpy.data.objects.get(obj.name + ".Mesh")
        for frame, visible in keys:
            key_hide(obj, frame, not visible)
            if mesh is not None:
                key_hide(mesh, frame, not visible)


def main():
    reset()
    def card(name, filename, loc, rot):
        return image_card(name, LOOK / "move" / filename, loc, rot, 1.62)

    front = (0.0, 0.15, 0.81)
    side = (0.15, 0.0, 0.81)
    frot = (math.radians(90), 0, 0)
    srot = (math.radians(90), 0, math.radians(90))
    f1 = card("Look.34A", "walk3d_34_01.jpg", front, frot)
    f2 = card("Look.34M", "walk3d_34_midA.jpg", front, frot)
    f3 = card("Look.34B", "walk3d_34_02.jpg", front, frot)
    f4 = card("Look.34N", "walk3d_34_midB.jpg", front, frot)
    r1 = card("Look.RightA", "walk3d_right_01.jpg", side, srot)
    r2 = card("Look.RightM12", "walk3d_right_mid12.jpg", side, srot)
    r3 = card("Look.RightB", "walk3d_right_02.jpg", side, srot)
    r4 = card("Look.RightM23", "walk3d_right_mid23.jpg", side, srot)
    r5 = card("Look.RightC", "walk3d_right_03.jpg", side, srot)
    r6 = card("Look.RightM31", "walk3d_right_mid31.jpg", side, srot)

    def cycle(objs, hold):
        keys = []
        total = len(objs) * hold
        for i, obj in enumerate(objs):
            on = 1 + i * hold
            off = on + hold
            k = []
            if on > 1:
                k.append((1, False))
            k.append((on, True))
            if off <= total:
                k.append((off, False))
            keys.append((obj, k))
        return keys

    flipbook(cycle([f1, f2, f3, f4], 6) + cycle([r1, r2, r3, r4, r5, r6], 4))
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

    # Playback is the walk flipbook. Orbit by hand; do not auto-spin.

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
