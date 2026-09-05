"""Retarget saved Meshy sideways spin onto the accepted V4 rig, changing only Dodge."""
import argparse
import hashlib
import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path
import bpy
from mathutils import Matrix, Vector


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-blend', type=Path, required=True)
    parser.add_argument('--donor-fbx', type=Path, required=True)
    parser.add_argument('--out-dir', type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
    out = args.out_dir.resolve()
    if out.exists() and any(out.iterdir()): raise RuntimeError('Use a new empty output directory')
    out.mkdir(parents=True, exist_ok=True)
    spec = importlib.util.spec_from_file_location('clean', Path(__file__).with_name('prepare_azure_maiden_clean.py'))
    clean = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(clean)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(args.donor_fbx.resolve()), automatic_bone_orientation=False)
    donor = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    action = next(a for a in bpy.data.actions if '360_Power_Spin_Jump' in a.name)
    clean.assign_action(donor, action)
    donor_rest = {b.name: (donor.matrix_world @ b.matrix_local).to_quaternion() for b in donor.data.bones}
    samples = []
    # Trim standing wind-up but retain spin and upright recovery. Preserve the
    # 1..24 take range and the Unity importer/controller clip identities.
    donor_frames = [24 + 50 * i / 23 for i in range(24)]
    for frame in donor_frames:
        bpy.context.scene.frame_set(int(frame), subframe=frame % 1)
        bpy.context.view_layer.update()
        samples.append({b.name: (donor.matrix_world @ b.matrix).to_quaternion() @ donor_rest[b.name].inverted()
                        for b in donor.pose.bones})
    bpy.ops.wm.open_mainfile(filepath=str(args.source_blend.resolve()))
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    before_actions = {a.name: list(a.frame_range) for a in bpy.data.actions}
    meshes = [o for o in bpy.data.objects if o.type == 'MESH']
    def geometry_hash():
        return hashlib.sha256(repr([(o.name, [tuple(v.co) for v in o.data.vertices],
            [tuple(p.vertices) for p in o.data.polygons]) for o in meshes]).encode()).hexdigest()
    before_geometry = geometry_hash()
    old = bpy.data.actions['Dodge']
    clean.assign_action(rig, None)
    bpy.data.actions.remove(old)
    dodge = bpy.data.actions.new('Dodge')
    dodge.use_fake_user = True
    clean.assign_action(rig, dodge)
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    rig_rotation = rig.matrix_world.to_quaternion()
    evidence = []
    previous_rotations = {}
    for index, sample in enumerate(samples):
        clean.clear_pose(rig)
        bpy.context.view_layer.update()
        for bone in clean.parent_order(rig):
            target_rest = rest[bone.name]
            world_rotation = sample[bone.name] @ rig_rotation @ target_rest.to_quaternion()
            rotation = rig_rotation.inverted() @ world_rotation
            if bone.parent:
                local_head = rest[bone.parent.name].inverted() @ target_rest.translation
                position = bone.parent.matrix @ local_head
            else:
                # The motor owns displacement and airborne lift.
                position = target_rest.translation.copy()
            bone.matrix = Matrix.LocRotScale(position, rotation, Vector((1, 1, 1)))
            bpy.context.view_layer.update()
        for bone in rig.pose.bones:
            rotation = bone.rotation_quaternion.copy()
            if bone.name in previous_rotations: rotation.make_compatible(previous_rotations[bone.name])
            bone.rotation_quaternion = rotation
            previous_rotations[bone.name] = rotation.copy()
        clean.key_pose(rig, index + 1)
        hips = rig.pose.bones['Hips'].matrix.translation
        head = rig.pose.bones['Head'].matrix.translation
        tilt = math.degrees((head - hips).angle(Vector((0, 0, 1))))
        evidence.append({'frame': index + 1, 'donorFrame': round(donor_frames[index], 3),
            'tiltDegrees': round(tilt, 3), 'hips': list(hips)})
    assert before_actions == {a.name: list(a.frame_range) for a in bpy.data.actions}
    assert geometry_hash() == before_geometry
    assert max(s['tiltDegrees'] for s in evidence) > 80, evidence
    clean.assign_action(rig, bpy.data.actions['Idle'])
    bpy.context.scene.frame_set(1)
    (out / 'textures').mkdir()
    atlas = out / 'textures/azure-maiden-base.png'
    source_atlas = next(Path(bpy.path.abspath(image.filepath)) for image in bpy.data.images
                        if image.name == 'AzureMaidenDirectRetexture')
    shutil.copyfile(source_atlas, atlas)
    for image in bpy.data.images:
        if image.name == 'AzureMaidenDirectRetexture': image.filepath = image.filepath_raw = str(atlas)
    blend = out / 'azure-maiden-clean-runtime.blend'
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    exports = clean.export_runtime(rig, out)
    report = {'taskId': 'ORC-20260905-001', 'workPackage': 'WP19',
        'sourceSha256': clean.sha256(args.source_blend), 'donorSha256': clean.sha256(args.donor_fbx),
        'donorAction': '360_Power_Spin_Jump',
        'method': 'world rotation delta onto target rest; parent-chain target lengths; fixed Hips translation',
        'geometryUnchanged': True, 'allTakeRangesUnchanged': True, 'samples': evidence,
        'blendSha256': clean.sha256(blend), **exports}
    (out / 'dodge-retarget.json').write_text(json.dumps(report, indent=2) + '\n')
    bpy.context.scene.render.resolution_x = bpy.context.scene.render.resolution_y = 640
    bpy.context.scene.render.resolution_percentage = 100
    for frame in (1, 7, 12, 17, 24):
        clean.render_action(rig, 'Dodge', frame, out / f'dodge-{frame:02}.png')
    print('DODGE_RETARGET_OK', json.dumps(report))


if __name__ == '__main__': main()
