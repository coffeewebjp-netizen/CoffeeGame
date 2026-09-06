"""Render two evidence views from a locally downloaded Meshy motion GLB."""

import os
import sys

import bpy
from mathutils import Vector


args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
if len(args) != 2:
    raise SystemExit("Expected: input.glb output-directory")
input_path, output_dir = map(os.path.abspath, args)
os.makedirs(output_dir, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=input_path, import_pack_images=True)
meshes = [
    obj for obj in bpy.context.scene.objects
    if obj.type == "MESH" and any(modifier.type == "ARMATURE" for modifier in obj.modifiers)
]
armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
action = next(iter(bpy.data.actions))
armature.animation_data_create()
armature.animation_data.action = action
bpy.context.scene.frame_set(int(sum(action.frame_range) * 0.5))
bpy.context.view_layer.update()

depsgraph = bpy.context.evaluated_depsgraph_get()
points = []
for obj in meshes:
    evaluated = obj.evaluated_get(depsgraph)
    points.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
mins = [min(point[i] for point in points) for i in range(3)]
maxs = [max(point[i] for point in points) for i in range(3)]
dims = [maxs[i] - mins[i] for i in range(3)]
center = Vector(((mins[0] + maxs[0]) * 0.5, (mins[1] + maxs[1]) * 0.5, (mins[2] + maxs[2]) * 0.5))

scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x = 640
scene.render.resolution_y = 640
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
if scene.world is None:
    scene.world = bpy.data.worlds.new("Silver Cat Evidence World")
scene.world.color = (0.012, 0.018, 0.035)

def point_at(obj, target):
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()

bpy.ops.object.camera_add()
camera = bpy.context.object
camera.data.lens = 55
scene.camera = camera
distance = max(dims) * 1.55

bpy.ops.object.light_add(type="AREA", location=(2.8, -2.8, maxs[2] + 0.8))
key = bpy.context.object
key.data.energy = 1250
key.data.size = 3.2
point_at(key, center)
bpy.ops.object.light_add(type="AREA", location=(-2.0, 2.2, center.z + 0.8))
fill = bpy.context.object
fill.data.energy = 850
fill.data.size = 2.8
point_at(fill, center)

for label, y_sign in (("front", -1.0), ("back", 1.0)):
    camera.location = center + Vector((0, y_sign * distance, max(dims) * 0.05))
    point_at(camera, center)
    scene.render.filepath = os.path.join(output_dir, f"cat-running-{label}.png")
    bpy.ops.render.render(write_still=True)
