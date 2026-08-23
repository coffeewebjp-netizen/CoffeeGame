"""Try larger hole-fill on the already-baked Ronin blend and re-export."""

from pathlib import Path
import bpy

BLEND = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\source\girl1-meshy-ronin.blend")
EXPORT = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\export\girl1-meshy-ronin.fbx")
UNITY = Path(
    r"C:\work\CoffeeGAME\unity\CoffeeGame\Assets\CoffeeGame\Resources\Models\Hero\trial-anime-girl.fbx"
)
PREVIEWS = Path(r"C:\work\CoffeeGAME\art\3d\trials\meshy-girl1\previews\ronin")


def fill(obj, sides):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.normals_make_consistent(inside=False)
    bpy.ops.mesh.fill_holes(sides=sides)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.data.update()
    print("after fill", sides, "verts", len(obj.data.vertices), "polys", len(obj.data.polygons))


def render_front(obj, path):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    cam_data = bpy.data.cameras.new("C")
    cam = bpy.data.objects.new("C", cam_data)
    bpy.context.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.0, -3.35, 1.05)
    from mathutils import Vector
    cam.rotation_euler = (Vector((0.0, 0.0, 0.82)) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = str(path)
    scene.render.image_settings.file_format = "JPEG"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def export():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"} and not obj.name.startswith("PreviewOnly"):
            obj.select_set(True)
    arm = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT),
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        axis_forward="-Z",
        axis_up="Y",
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        path_mode="COPY",
        embed_textures=False,
    )


def main():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND))
    mesh = next(obj for obj in bpy.data.objects if obj.type == "MESH" and obj.name == "MeshyGirl1")
    fill(mesh, 24)
    render_front(mesh, PREVIEWS / "idle-front-fill24.jpg")
    export()
    import shutil
    shutil.copy2(EXPORT, UNITY)


if __name__ == "__main__":
    main()
