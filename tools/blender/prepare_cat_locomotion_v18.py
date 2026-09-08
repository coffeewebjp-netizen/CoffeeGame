"""Export Meshy cat locomotion to skeletal FBX without replacing the game skin.
Blender 4.5: --background --python this.py -- sprint.glb jump.glb output_dir
"""
import bpy,os,sys,json,hashlib,math
args=sys.argv[sys.argv.index('--')+1:]
out=args[2];os.makedirs(out,exist_ok=True)
report=[]
for name,path in zip(['MeshySprint','MeshyJump'],args[:2]):
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.scene.render.fps=60
 bpy.ops.import_scene.gltf(filepath=os.path.abspath(path))
 arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 action=arm.animation_data.action;action.name=name;action.use_fake_user=True
 first,last=map(float,action.frame_range)
 samples=[]
 for f in range(math.ceil(first),math.floor(last)+1,3):
  bpy.context.scene.frame_set(f)
  samples.append({'frame':f,'hips':list(arm.matrix_world@arm.pose.bones['Hips'].head),'head':list(arm.matrix_world@arm.pose.bones['Head'].head),'leftFoot':list(arm.matrix_world@arm.pose.bones['LeftFoot'].head),'rightFoot':list(arm.matrix_world@arm.pose.bones['RightFoot'].head)})
 arm.animation_data.action=None
 bind=bpy.data.actions.new('BindPose');bind.use_fake_user=True
 arm.animation_data.action=bind
 for b in arm.pose.bones:
  b.matrix_basis.identity();b.rotation_mode='QUATERNION'
  for f in [0,1]:
   b.keyframe_insert('location',frame=f);b.keyframe_insert('rotation_quaternion',frame=f);b.keyframe_insert('scale',frame=f)
 bpy.context.view_layer.update()
 for obj in bpy.context.scene.objects: obj.select_set(obj==arm)
 bpy.context.view_layer.objects.active=arm
 bpy.context.scene.frame_start=math.floor(first);bpy.context.scene.frame_end=math.ceil(last)
 bpy.ops.export_scene.fbx(filepath=os.path.join(out,name+'.fbx'),use_selection=True,object_types={'ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_step=1,bake_anim_simplify_factor=0)
 report.append({'name':name,'source_sha256':hashlib.sha256(open(path,'rb').read()).hexdigest(),'first':first,'last':last,'fps':60,'samples':samples})
json.dump(report,open(os.path.join(out,'motion-source.json'),'w'),indent=2)
