"""Sculpt-ready girl1 mesh. Does not touch the Unity HD-2D heroine."""

from __future__ import annotations

import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parent
REFS = ROOT / "refs"
LOOK = ROOT / "look-previews"
RENDERS = ROOT / "renders"
BLEND = ROOT / "girl1-sculpt.blend"
GLB = ROOT / "girl1-sculpt.glb"


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 1280
    scene.render.fps = 24
    world = bpy.data.worlds.new("SculptWorld")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.14, 0.14, 0.16, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8


def collection(name: str):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def link(obj, col):
    for existing in list(obj.users_collection):
        existing.objects.unlink(obj)
    col.objects.link(obj)


def mat_color(name: str, color, roughness: float = 0.5):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    principled = material.node_tree.nodes["Principled BSDF"]
    principled.inputs["Base Color"].default_value = (*color, 1.0)
    principled.inputs["Roughness"].default_value = roughness
    return material


def mat_guide(name: str, image_path: Path):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes["Principled BSDF"]
    tex = nodes.new("ShaderNodeTexImage")
    image = bpy.data.images.load(str(image_path))
    tex.image = image
    tex.interpolation = "Smart"
    links.new(tex.outputs["Color"], principled.inputs["Base Color"])
    principled.inputs["Roughness"].default_value = 0.45
    return material


def add_ref(name: str, path: Path, location, rotation, size: float, col):
    image = bpy.data.images.load(str(path))
    bpy.ops.object.empty_add(type="IMAGE", location=location, rotation=rotation)
    empty = bpy.context.object
    empty.name = name
    empty.data = image
    empty.empty_display_size = size
    empty.hide_render = True
    link(empty, col)
    return empty


def primitive(name, kind, location, scale, material, col, **kwargs):
    getattr(bpy.ops.mesh, f"primitive_{kind}_add")(location=location, **kwargs)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    link(obj, col)
    return obj


def apply_rot(obj, euler):
    obj.rotation_euler = euler
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    obj.select_set(False)


def box_body(skin, col):
    bpy.ops.mesh.primitive_cube_add(location=(0, 0, 0.90), size=1.0)
    body = bpy.context.object
    body.name = "Girl1Body"
    body.scale = (0.17, 0.11, 0.90)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.subdivide(number_cuts=8)
    bpy.ops.object.mode_set(mode="OBJECT")
    mesh = body.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    for vert in bm.verts:
        x, y, z = vert.co
        # stylized female silhouette
        if z > 1.38:
            vert.co.x *= 0.55
            vert.co.y *= 0.70
            vert.co.z = 1.38 + (z - 1.38) * 0.55
        elif z > 1.22:
            vert.co.x *= 0.72
            vert.co.y *= 0.78
        elif z > 1.08:
            vert.co.x *= 1.08
            vert.co.y *= 0.95
        elif z > 0.94:
            vert.co.x *= 0.82
            vert.co.y *= 0.88
        elif z > 0.78:
            vert.co.x *= 1.05
            vert.co.y *= 0.98
        elif z > 0.45:
            side = 1.0 if x >= 0 else -1.0
            vert.co.x = side * (0.055 + abs(x) * 0.15)
            vert.co.y *= 0.70
        else:
            side = 1.0 if x >= 0 else -1.0
            vert.co.x = side * (0.055 + abs(x) * 0.08)
            vert.co.y *= 0.55
            if z < 0.08:
                vert.co.y += 0.04
        vert.co.z = max(0.03, z)
    bm.to_mesh(mesh)
    bm.free()
    body.data.materials.append(skin)
    bpy.ops.object.shade_smooth()
    link(body, col)
    return body


def add_multires(obj, levels: int = 2):
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Multires", "MULTIRES")
    for _ in range(levels):
        bpy.ops.object.multires_subdivide(modifier="Multires", mode="CATMULL_CLARK")
    return mod


def project_front_uv(obj, image_path: Path):
    bpy.ops.object.camera_add(location=(0, -2.6, 0.90), rotation=(math.radians(90), 0, 0))
    cam = bpy.context.object
    cam.name = "UVFrontCamera"
    bpy.context.scene.camera = cam
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    guide = mat_guide("LookGuide", image_path)
    obj.data.materials.clear()
    obj.data.materials.append(guide)
    return cam


def build_parts(col_hair, col_cloth, col_weapon):
    hair = mat_color("Hair", (0.38, 0.80, 0.90), 0.35)
    kimono = mat_color("Kimono", (0.64, 0.13, 0.15), 0.48)
    skirt = mat_color("Skirt", (0.88, 0.70, 0.20), 0.48)
    wood = mat_color("Wood", (0.26, 0.16, 0.09), 0.6)
    steel = mat_color("Steel", (0.78, 0.80, 0.84), 0.25)
    skin = mat_color("Skin", (0.91, 0.75, 0.67), 0.45)

    hair_cap = primitive("HairCap", "uv_sphere", (0, -0.01, 1.50), (0.13, 0.15, 0.12), hair, col_hair)
    hair_back = primitive("HairBack", "uv_sphere", (0, -0.07, 1.40), (0.12, 0.13, 0.15), hair, col_hair)
    bang = primitive("HairBang", "uv_sphere", (0, 0.07, 1.52), (0.10, 0.05, 0.07), hair, col_hair)

    torso = primitive("KimonoTorso", "cube", (0, 0.01, 1.16), (0.18, 0.11, 0.16), kimono, col_cloth)
    sleeve_l = primitive("Sleeve.L", "cube", (0.20, 0.03, 1.10), (0.09, 0.11, 0.11), kimono, col_cloth)
    sleeve_r = primitive("Sleeve.R", "cube", (-0.20, 0.03, 1.10), (0.09, 0.11, 0.11), kimono, col_cloth)
    obi = primitive("Obi", "cube", (0, 0.03, 0.96), (0.16, 0.09, 0.04), wood, col_cloth)
    skirt_mesh = primitive("Skirt", "cone", (0, 0, 0.68), (0.24, 0.17, 0.20), skirt, col_cloth, vertices=20)
    geta_l = primitive("Geta.L", "cube", (0.06, 0.05, 0.045), (0.05, 0.12, 0.025), wood, col_cloth)
    geta_r = primitive("Geta.R", "cube", (-0.06, 0.05, 0.045), (0.05, 0.12, 0.025), wood, col_cloth)

    saya = primitive("Saya", "cylinder", (0.15, 0.09, 0.90), (0.016, 0.016, 0.30), wood, col_weapon, vertices=12)
    blade = primitive("Blade", "cube", (0.15, 0.09, 1.16), (0.010, 0.003, 0.18), steel, col_weapon)
    apply_rot(saya, (0, math.radians(68), math.radians(18)))
    apply_rot(blade, (0, math.radians(68), math.radians(18)))

    for obj in (hair_cap, hair_back, bang, torso, sleeve_l, sleeve_r, skirt_mesh):
        add_multires(obj, 1)

    return skin


def build_armature():
    bpy.ops.object.armature_add(enter_editmode=True, location=(0, 0, 0))
    arm_obj = bpy.context.object
    arm_obj.name = "Girl1Rig"
    arm = arm_obj.data
    for existing in list(arm.edit_bones):
        arm.edit_bones.remove(existing)

    def b(name, head, tail, parent=None):
        bone = arm.edit_bones.new(name)
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        if parent:
            bone.parent = arm.edit_bones[parent]
        return bone

    b("Hips", (0, 0, 0.86), (0, 0, 1.00))
    b("Spine", (0, 0, 1.00), (0, 0, 1.14), "Hips")
    b("Chest", (0, 0, 1.14), (0, 0, 1.32), "Spine")
    b("Neck", (0, 0, 1.32), (0, 0, 1.40), "Chest")
    b("Head", (0, 0, 1.40), (0, 0, 1.62), "Neck")
    b("Thigh.L", (0.06, 0, 0.86), (0.06, 0, 0.50), "Hips")
    b("Shin.L", (0.06, 0, 0.50), (0.06, 0, 0.16), "Thigh.L")
    b("Foot.L", (0.06, 0, 0.16), (0.06, 0.10, 0.04), "Shin.L")
    b("Thigh.R", (-0.06, 0, 0.86), (-0.06, 0, 0.50), "Hips")
    b("Shin.R", (-0.06, 0, 0.50), (-0.06, 0, 0.16), "Thigh.R")
    b("Foot.R", (-0.06, 0, 0.16), (-0.06, 0.10, 0.04), "Shin.R")
    b("UpperArm.L", (0.16, 0, 1.24), (0.38, 0, 1.18), "Chest")
    b("Forearm.L", (0.38, 0, 1.18), (0.56, 0, 1.16), "UpperArm.L")
    b("UpperArm.R", (-0.16, 0, 1.24), (-0.38, 0, 1.18), "Chest")
    b("Forearm.R", (-0.38, 0, 1.18), (-0.56, 0, 1.16), "UpperArm.R")
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm_obj


def bind(body, armature):
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    armature.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")


def lights_and_camera():
    bpy.ops.object.light_add(type="AREA", location=(1.5, -2.0, 2.3))
    bpy.context.object.data.energy = 280
    bpy.context.object.data.size = 1.8
    bpy.ops.object.light_add(type="AREA", location=(-1.6, 1.4, 1.7))
    bpy.context.object.data.energy = 90
    bpy.ops.object.camera_add(location=(1.15, -2.35, 1.15), rotation=(math.radians(78), 0, math.radians(22)))
    cam = bpy.context.object
    cam.name = "PreviewCamera"
    cam.data.lens = 50
    bpy.context.scene.camera = cam


def render_preview():
    RENDERS.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    scene.render.filepath = str(RENDERS / "sculpt-preview.jpg")
    bpy.ops.render.render(write_still=True)


def main() -> None:
    reset_scene()
    refs = collection("References")
    body_col = collection("Body")
    hair_col = collection("Hair")
    cloth_col = collection("Clothes")
    weapon_col = collection("Weapon")

    add_ref("Ref.Front", REFS / "tpose-front.jpg", (0.0, -1.25, 0.81), (math.radians(90), 0, 0), 1.62, refs)
    add_ref("Ref.Right", REFS / "tpose-right.jpg", (1.25, 0.0, 0.81), (math.radians(90), 0, math.radians(90)), 1.62, refs)
    add_ref("Ref.Back", REFS / "tpose-back.jpg", (0.0, 1.25, 0.81), (math.radians(90), 0, math.radians(180)), 1.62, refs)

    look = LOOK / "look-34.jpg"
    if not look.exists():
        look = REFS / "tpose-front.jpg"

    skin = build_parts(hair_col, cloth_col, weapon_col)
    body = box_body(skin, body_col)
    project_front_uv(body, look)
    add_multires(body, 2)
    armature = build_armature()
    link(armature, body_col)
    bind(body, armature)
    lights_and_camera()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND))
    render_preview()
    bpy.ops.export_scene.gltf(filepath=str(GLB), export_format="GLB", export_animations=False)
    bpy.ops.wm.save_mainfile()
    print("Wrote", BLEND)


if __name__ == "__main__":
    main()
