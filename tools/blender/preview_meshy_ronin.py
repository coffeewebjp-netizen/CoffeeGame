"""Preview rest, run, and attack poses from the Ronin pack."""

from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\drop\ronin\Meshy_AI_Blue_Haired_Ronin_biped")
OUT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\previews\ronin")
TEX = ROOT / "Meshy_AI_Blue_Haired_Ronin_biped_texture_0.png"
SHOTS = [
    ("rest", "Meshy_AI_Blue_Haired_Ronin_biped_Character_output.fbx", 1),
    ("attack-mid", "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Attack_withSkin.fbx", 43),
    ("combo-mid", "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Double_Combo_Attack_withSkin.fbx", 43),
    ("judgment-mid", "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Sword_Judgment_withSkin.fbx", 66),
    ("run-mid", "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Running_withSkin.fbx", 10),
    ("run-side", "Meshy_AI_Blue_Haired_Ronin_biped_Animation_Running_withSkin.fbx", 10),
]


def assign_action(arm, action):
    if arm.animation_data is None:
        arm.animation_data_create()
    ad = arm.animation_data
    ad.action = action
    if hasattr(ad, "action_slot") and hasattr(action, "slots") and len(action.slots) > 0:
        ad.action_slot = action.slots[0]
    bpy.context.view_layer.update()


def render(path, cam_loc, look=(0.0, 0.0, 0.82)):
    scene = bpy.context.scene
    cam_data = bpy.data.cameras.new("Cam")
    cam_data.lens = 40
    cam = bpy.data.objects.new("Cam", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = cam_loc
    cam.rotation_euler = (Vector(look) - cam.location).to_track_quat("-Z", "Y").to_euler()
    path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(path)
    scene.render.image_settings.file_format = "JPEG"
    scene.render.image_settings.quality = 90
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def ground_scale(arm, mesh):
    bpy.context.view_layer.update()
    pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
    zs = [p.z for p in pts]
    xs = [p.x for p in pts]
    ys = [p.y for p in pts]
    height = max(0.001, max(zs) - min(zs))
    scale = 1.62 / height
    arm.scale = (arm.scale.x * scale, arm.scale.y * scale, arm.scale.z * scale)
    bpy.context.view_layer.update()
    pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
    xs = [p.x for p in pts]
    ys = [p.y for p in pts]
    zs = [p.z for p in pts]
    arm.location.x -= (min(xs) + max(xs)) * 0.5
    arm.location.y -= (min(ys) + max(ys)) * 0.5
    arm.location.z -= min(zs)
    bpy.context.view_layer.update()


def main():
    for name, filename, frame in SHOTS:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE_NEXT"
        scene.render.resolution_x = 768
        scene.render.resolution_y = 1152
        world = bpy.data.worlds.new("W")
        scene.world = world
        world.use_nodes = True
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.62, 0.68, 0.74, 1.0)
        bpy.ops.import_scene.fbx(filepath=str(ROOT / filename), automatic_bone_orientation=True)
        arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
        mesh = next(o for o in bpy.data.objects if o.type == "MESH")
        if TEX.exists():
            mat = mesh.active_material or bpy.data.materials.new("P")
            mat.use_nodes = True
            tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
            tex.image = bpy.data.images.load(str(TEX), check_existing=True)
            principled = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            mat.node_tree.links.new(tex.outputs["Color"], principled.inputs["Base Color"])
            if mesh.data.materials:
                mesh.data.materials[0] = mat
            else:
                mesh.data.materials.append(mat)
        ground_scale(arm, mesh)
        if bpy.data.actions:
            assign_action(arm, bpy.data.actions[0])
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        cam = (3.35, 0.0, 1.05) if name.endswith("side") else (2.15, -2.75, 1.15)
        render(OUT / f"{name}.jpg", cam)
        print("wrote", name)


if __name__ == "__main__":
    main()
