"""Retint heroine-v4 toward the approved 120 look, and a projection ceiling.

Does not modify Unity HD-2D or the source FBX.
"""

from __future__ import annotations

import math
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parent
LOOK = ROOT / "look-previews"
FBX = Path(r"C:\work\CoffeeGAME\unity\CoffeeGame\Assets\CoffeeGame\Resources\Models\Hero\heroine-v4.fbx")
BLEND = ROOT / "girl1-lookmatch.blend"
RENDERS = ROOT / "renders"

# Picked from the approved look stills, not from background-contaminated averages.
LOOK_COLORS = {
    "CG_Hero_Hair_SkyCyan_URP": (0.22, 0.72, 0.88),
    "CG_Hero_Hair_Highlight_URP": (0.55, 0.88, 0.96),
    "CG_Hero_Hair_BlueShadow_URP": (0.06, 0.28, 0.48),
    "CG_Hero_Haori_Crimson_URP": (0.62, 0.12, 0.14),
    "CG_Hero_Haori_Highlight_URP": (0.78, 0.22, 0.22),
    "CG_Hero_Haori_InnerWine_URP": (0.32, 0.04, 0.06),
    "CG_Hero_Skirt_Apricot_URP": (0.86, 0.66, 0.16),
    "CG_Hero_Skirt_Highlight_URP": (0.94, 0.80, 0.32),
    "CG_Hero_Skirt_PleatShadow_URP": (0.52, 0.34, 0.06),
    "CG_Hero_Top_WarmWhite_URP": (0.90, 0.86, 0.80),
    "CG_Hero_Skin_Peach_URP": (0.86, 0.64, 0.56),
    "CG_Hero_Skin_Shadow_URP": (0.52, 0.28, 0.24),
    "CG_Hero_Boot_Rubber_URP": (0.28, 0.16, 0.08),
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    world = bpy.data.worlds.new("MatchWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.16, 0.18, 1.0)


def retint():
    for mat in bpy.data.materials:
        color = LOOK_COLORS.get(mat.name)
        if color is None or not mat.use_nodes:
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (*color, 1.0)


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


def make_projection_statue(body, camera, image_path: Path):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.duplicate()
    statue = bpy.context.object
    statue.name = "LookCeiling"
    statue.location.x = 1.45
    bpy.ops.object.make_single_user(object=True, obdata=True, material=True)
    mat = bpy.data.materials.new("LookProject")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(str(image_path))
    tex.extension = "CLIP"
    coord = nodes.new("ShaderNodeTexCoord")
    mapping = nodes.new("ShaderNodeMapping")
    links.new(coord.outputs["Window"], mapping.inputs["Vector"])
    links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.45
    statue.data.materials.clear()
    statue.data.materials.append(mat)
    return statue


def main():
    reset()
    bpy.ops.import_scene.fbx(filepath=str(FBX))
    if "Cube" in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects["Cube"], do_unlink=True)
    retint()

    image_empty("Look.34", LOOK / "look-34.jpg", (-1.45, 0.15, 0.81), (math.radians(90), 0, 0), 1.62)
    image_empty("Look.Bust", LOOK / "look-bust.jpg", (-2.35, 0.15, 1.20), (math.radians(90), 0, 0), 0.95)

    body = bpy.data.objects["HeroineBody"]
    bpy.ops.object.light_add(type="AREA", location=(1.6, -2.3, 2.3))
    bpy.context.object.data.energy = 380
    bpy.context.object.data.size = 2.0
    bpy.ops.object.light_add(type="AREA", location=(-1.4, 1.5, 1.7))
    bpy.context.object.data.energy = 110
    bpy.ops.object.camera_add(location=(0.15, -3.15, 1.12), rotation=(math.radians(80), 0, math.radians(4)))
    cam = bpy.context.object
    cam.name = "MatchCamera"
    cam.data.lens = 40
    bpy.context.scene.camera = cam
    make_projection_statue(body, cam, LOOK / "look-34.jpg")

    arm = bpy.data.objects.get("HeroineRigV4")
    run = next((a for a in bpy.data.actions if a.name.endswith("|Idle")), None)
    if arm is not None and run is not None:
        if arm.animation_data is None:
            arm.animation_data_create()
        arm.animation_data.action = run
        slots = list(getattr(arm.animation_data, "action_suitable_slots", []) or [])
        if slots:
            arm.animation_data.action_slot = slots[0]

    RENDERS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 92
    scene.render.filepath = str(RENDERS / "lookmatch-compare.jpg")
    bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND)


if __name__ == "__main__":
    main()
