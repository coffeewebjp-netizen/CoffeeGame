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
    walk = next((a for a in bpy.data.actions if a.name.endswith("|Run")), None)
    if walk is None:
        walk = next((a for a in bpy.data.actions if a.name.endswith("|Walk")), None)
    start = 1
    end = 32
    if arm is not None and walk is not None:
        if arm.animation_data is None:
            arm.animation_data_create()
        # Blender 4.5 slotted actions: assigning Walk without the Walk slot
        # leaves the FBX default (AirSlash) and the pose never changes.
        arm.animation_data.action = None
        for track in list(arm.animation_data.nla_tracks):
            arm.animation_data.nla_tracks.remove(track)
        arm.animation_data.action = walk
        suitable = list(getattr(arm.animation_data, "action_suitable_slots", []) or [])
        walk_slot = next((s for s in suitable if "Walk" in str(getattr(s, "identifier", s))), None)
        if walk_slot is None and suitable:
            walk_slot = suitable[0]
        if walk_slot is not None:
            arm.animation_data.action_slot = walk_slot
        track = arm.animation_data.nla_tracks.new()
        track.name = "Walk"
        strip = track.strips.new("Walk", 1, walk)
        if hasattr(strip, "action_slot") and walk_slot is not None:
            strip.action_slot = walk_slot
        strip.frame_end = walk.frame_range[1]
        strip.repeat = 8
        arm.animation_data.action = None
        start = int(walk.frame_range[0])
        end = int(walk.frame_range[1])
        bpy.context.scene.frame_start = start
        bpy.context.scene.frame_end = end
        bpy.context.scene.frame_current = start
        bpy.context.scene.sync_mode = "NONE"

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
    thigh = arm.pose.bones.get("Thigh.R") if arm is not None else None
    bpy.context.scene.frame_set(start if walk else 1)
    bpy.context.view_layer.update()
    r1 = tuple(round(x, 4) for x in thigh.rotation_quaternion) if thigh else None
    bpy.context.scene.render.filepath = str(RENDER)
    bpy.ops.render.render(write_still=True)
    mid = start + 12 if walk else 13
    bpy.context.scene.frame_set(mid)
    bpy.context.view_layer.update()
    r2 = tuple(round(x, 4) for x in thigh.rotation_quaternion) if thigh else None
    bpy.context.scene.render.filepath = str(RENDER.with_name("compare-walk-mid.jpg"))
    bpy.ops.render.render(write_still=True)
    bpy.context.scene.frame_set(start if walk else 1)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND, "walk", walk.name if walk else None, "thigh", r1, "->", r2)


if __name__ == "__main__":
    main()
