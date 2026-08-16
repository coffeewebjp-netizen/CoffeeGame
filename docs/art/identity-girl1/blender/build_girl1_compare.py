"""Compare approved 120-look cards with the existing 1.6m heroine mesh.

Does not modify Unity HD-2D sprites or the source FBX on disk.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parent
LOOK = ROOT / "look-previews"
FBX = Path(r"C:\work\CoffeeGAME\unity\CoffeeGame\Assets\CoffeeGame\Resources\Models\Hero\heroine-v4.fbx")
BLEND = ROOT / "girl1-compare.blend"
RENDER = ROOT / "renders" / "compare-preview.jpg"


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 960
    scene.render.fps = 24
    world = bpy.data.worlds.new("CompareWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.16, 0.18, 1.0)


def image_empty(name, path, location, rotation, height):
    image = bpy.data.images.load(str(path))
    bpy.ops.object.empty_add(type="IMAGE", location=location, rotation=rotation)
    empty = bpy.context.object
    empty.name = name
    empty.data = image
    empty.empty_display_size = height
    empty.empty_image_side = "DOUBLE_SIDED"
    empty.show_in_front = True
    return empty


def main():
    reset()
    bpy.ops.import_scene.fbx(filepath=str(FBX))
    if "Cube" in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)

    image_empty("Look.34", LOOK / "look-34.jpg", (-1.15, 0.2, 0.81), (math.radians(90), 0, 0), 1.62)
    image_empty("Look.Right", LOOK / "look-right.jpg", (1.25, 0.0, 0.81), (math.radians(90), 0, math.radians(90)), 1.62)

    arm = bpy.data.objects.get("HeroineRigV4")
    walk = next((a for a in bpy.data.actions if a.name.endswith("|Walk")), None)
    if arm is not None and walk is not None:
        if arm.animation_data is None:
            arm.animation_data_create()
        arm.animation_data.action = walk
        bpy.context.scene.frame_start = int(walk.frame_range[0])
        bpy.context.scene.frame_end = int(walk.frame_range[1])
        bpy.context.scene.frame_current = int(walk.frame_range[0])

    bpy.ops.object.light_add(type="AREA", location=(1.8, -2.4, 2.4))
    bpy.context.object.data.energy = 400
    bpy.context.object.data.size = 2.0
    bpy.ops.object.light_add(type="AREA", location=(-1.6, 1.6, 1.8))
    bpy.context.object.data.energy = 120
    bpy.ops.object.camera_add(location=(1.35, -2.55, 1.15), rotation=(math.radians(78), 0, math.radians(24)))
    cam = bpy.context.object
    cam.name = "CompareCamera"
    cam.data.lens = 50
    bpy.context.scene.camera = cam

    RENDER.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    bpy.context.scene.render.image_settings.file_format = "JPEG"
    bpy.context.scene.render.image_settings.quality = 92
    bpy.context.scene.render.filepath = str(RENDER)
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND, "walk", walk.name if walk else None)


if __name__ == "__main__":
    main()
