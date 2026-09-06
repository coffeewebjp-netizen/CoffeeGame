"""Prepare the private Meshy goblin rig and motions for CoffeeGAME V8.

Inputs: goblin-running.glb, goblin-orc-walk.glb, goblin-axe-chop.glb,
goblin-idle.glb, goblin-dead.glb, all from the same Meshy rig.
"""
import argparse, hashlib, json, math, pathlib, sys
import bpy
from mathutils import Vector, Matrix, Quaternion

parser=argparse.ArgumentParser()
parser.add_argument('--source-dir',required=True)
parser.add_argument('--output-dir',required=True)
parser.add_argument('--unity-dir',required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
source=pathlib.Path(args.source_dir); output=pathlib.Path(args.output_dir)
unity=pathlib.Path(args.unity_dir)
output.mkdir(parents=True,exist_ok=True);(output/'previews').mkdir(exist_ok=True)
unity.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.render.fps=30

def imported(name):
    before=set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(source/name))
    objects=set(bpy.data.objects)-before
    rig=next(o for o in objects if o.type=='ARMATURE')
    return rig,objects

rig,objects=imported('goblin-running.glb')
body=next(o for o in objects if o.type=='MESH' and o.name.startswith('char1'))
rig.name='GoblinRig';body.name='ForestGoblinBody'
for o in objects-{rig,body}:bpy.data.objects.remove(o,do_unlink=True)
rig.animation_data_clear()
rig.data.pose_position='REST'
for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
for material in body.data.materials:
    for node in material.node_tree.nodes:
        if node.type=='TEX_IMAGE' and node.image:
            img=node.image
            if img.name.startswith('texture_0'):
                img.filepath_raw=str(unity/'forest-goblin-base.png');img.file_format='PNG';img.save()
    bsdf=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bsdf:bsdf.inputs['Roughness'].default_value=.78

bind={b.name:rig.matrix_world@b.matrix_local for b in rig.data.bones}
root_rest=rig.data.bones['Hips'].head_local.copy()
donors={};provenance=[]
for key,file in [('Walk','goblin-orc-walk.glb'),('Chop','goblin-axe-chop.glb'),('Idle','goblin-idle.glb'),('Dead','goblin-dead.glb')]:
    donor,obs=imported(file)
    error=max(max(abs(v)for row in (bind[b.name]-donor.matrix_world@b.matrix_local)for v in row)for b in donor.data.bones)
    if error>1e-4:raise RuntimeError('Incompatible donor bind: '+file+' '+str(error))
    action=donor.animation_data.action
    donor.hide_render=True
    for o in obs:
        if o!=donor:bpy.data.objects.remove(o,do_unlink=True)
    donors[key]=(donor,action)
    provenance.append({'file':file,'sha256':hashlib.sha256((source/file).read_bytes()).hexdigest(),'bindError':error,'frames':list(action.frame_range)})

def sample(key,frame):
    donor,action=donors[key]
    donor.animation_data.action=action
    scene.frame_set(int(frame),subframe=frame%1)
    result={b.name:b.matrix.copy() for b in donor.pose.bones}
    # Plant the entire rig in X/Y; gameplay, not a mocap root, owns translation.
    offset=result['Hips'].translation-root_rest;offset.z=0
    shift=Matrix.Translation(-offset)
    return {n:shift@m for n,m in result.items()}

def blend(a,b,t):
    out={}
    for n in a:
        la,ra,sa=a[n].decompose();lb,rb,sb=b[n].decompose()
        out[n]=Matrix.LocRotScale(la.lerp(lb,t),ra.slerp(rb,t),sa.lerp(sb,t))
    return out

idle_start,idle_end=donors['Idle'][1].frame_range
walk_start,walk_end=donors['Walk'][1].frame_range
dead_start,dead_end=donors['Dead'][1].frame_range
idle_pose=sample('Idle',idle_start)
rig.data.pose_position='POSE'
actions={}

def bake(name,seconds,get_pose,loop=False):
    action=bpy.data.actions.new(name);action.use_fake_user=True
    rig.animation_data_create();rig.animation_data.action=action
    frames=round(seconds*30)
    first=None
    for f in range(frames+1):
        pose=get_pose(f/frames)
        if f==0:first=pose
        if loop and f>=frames-6:pose=blend(pose,first,(f-(frames-6))/6)
        # Set in parent order because assigning pose matrices computes local channels.
        for bone in rig.pose.bones:
            bone.matrix=pose[bone.name]
            bpy.context.view_layer.update()
        for bone in rig.pose.bones:
            bone.rotation_mode='QUATERNION'
            bone.keyframe_insert(data_path='location',frame=f)
            bone.keyframe_insert(data_path='rotation_quaternion',frame=f)
            bone.keyframe_insert(data_path='scale',frame=f)
    for curve in action.fcurves:
        for k in curve.keyframe_points:k.interpolation='LINEAR'
    actions[name]=action

bake('Idle',(idle_end-idle_start)/30,lambda t:sample('Idle',idle_start+(idle_end-idle_start)*t),True)
bake('Walk',2.75,lambda t:sample('Walk',walk_start+(walk_end-walk_start)*t),True)
def windup(t):
    if t<.22:return blend(idle_pose,sample('Chop',82.5),t/.22)
    return sample('Chop',82.5+(123.75-82.5)*(t-.22)/.78)
bake('AttackWindup',.72,windup)
def attack(t):
    impact_fraction=.16/.38
    frame=123.75+(136.25-123.75)*t/impact_fraction if t<=impact_fraction else 136.25+(147.5-136.25)*(t-impact_fraction)/(1-impact_fraction)
    return sample('Chop',frame)
bake('Attack',.38,attack)
def hurt(t):
    pose={n:m.copy()for n,m in idle_pose.items()}
    strength=math.sin(math.pi*t)
    pivot=pose['Hips'].translation
    rot=Matrix.Rotation(math.radians(-15)*strength,4,'X')
    shift=Matrix.Translation(pivot)@rot@Matrix.Translation(-pivot)
    for n in pose:
        if n not in ['Hips','LeftUpLeg','LeftLeg','LeftFoot','LeftToeBase','RightUpLeg','RightLeg','RightFoot','RightToeBase']:
            pose[n]=shift@pose[n]
    return pose
bake('Hurt',.32,hurt)
bake('Defeated',.9,lambda t:sample('Dead',dead_start+(dead_end-dead_start)*t))

# Replace the splayed right fingers with a leather glove closed around a separate club.
rig.animation_data.action=None
rig.data.pose_position='REST';bpy.context.view_layer.update()
wrist=rig.matrix_world@rig.data.bones['RightHand'].head_local
elbow=rig.matrix_world@rig.data.bones['RightForeArm'].head_local
hand_dir=(wrist-elbow).normalized();grip=wrist+hand_dir*.047
group=body.vertex_groups.get('RightHand').index
weights={v.index:next((g.weight for g in v.groups if g.group==group),0)for v in body.data.vertices}
remove={v.index for v in body.data.vertices if weights[v.index]>.65 and (body.matrix_world@v.co-wrist).dot(hand_dir)>.025}
import bmesh
bm=bmesh.new();bm.from_mesh(body.data);bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[bm.verts[i]for i in remove],context='VERTS');bm.to_mesh(body.data);bm.free()

mat=bpy.data.materials.new('Goblin Club Wood');mat.diffuse_color=(.23,.095,.028,1);mat.use_nodes=True
bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=(.23,.095,.028,1);bsdf.inputs['Roughness'].default_value=.86

def skinned(name,vertices,faces):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    obj.parent=rig;obj.matrix_parent_inverse=Matrix.Identity(4);obj.matrix_basis=Matrix.Identity(4)
    vg=obj.vertex_groups.new(name='RightHand');vg.add(list(range(len(vertices))),1,'REPLACE')
    modifier=obj.modifiers.new('Goblin rig','ARMATURE');modifier.object=rig
    obj.data.materials.append(mat)
    for p in mesh.polygons:p.use_smooth=True
    return obj

inv=rig.matrix_world.inverted()
up=Vector((0,0,1));side=hand_dir.cross(up).normalized();up=side.cross(hand_dir).normalized()
vertices=[];faces=[]
for i in range(9):
    theta=math.pi*i/8
    for j in range(16):
        a=2*math.pi*j/16
        v=grip+hand_dir*(math.cos(theta)*.064)+side*(math.sin(theta)*math.cos(a)*.041)+up*(math.sin(theta)*math.sin(a)*.044)
        vertices.append(inv@v)
for i in range(8):
    for j in range(16):
        a=i*16+j;b=i*16+(j+1)%16;faces.append((a,b,b+16,a+16))
glove=skinned('ClubGripGlove',vertices,faces)
glove.data.materials[0]=mat.copy()
glove.data.materials[0].name='Goblin Leather Grip'

# Set the club's grip orientation using the actual mocap impact pose.
impact=sample('Chop',136.25)['RightHand']
rest=rig.data.bones['RightHand'].matrix_local
posed_grip=rig.matrix_world@impact@rest.inverted()@(inv@grip)
desired=(Vector((0,-1.0,.12))-posed_grip).normalized()
axis=(rest.to_3x3()@impact.to_3x3().inverted()@desired).normalized()
axis_world=axis
u=axis_world.cross(Vector((1,0,0))).normalized();v=axis_world.cross(u).normalized()
vertices=[];faces=[];rings=[(-.12,.029),(0,.029),(.14,.034),(.22,.070),(.36,.10),(.51,.105),(.63,.080),(.68,.02)]
for i,(z,radius)in enumerate(rings):
    for j in range(14):
        a=2*math.pi*j/14;rough=1+.09*math.sin(j*2.3+i*.8)
        p=grip+axis_world*z+(u*math.cos(a)+v*math.sin(a))*radius*rough
        vertices.append(inv@p)
for i in range(len(rings)-1):
    for j in range(14):a=i*14+j;b=i*14+(j+1)%14;faces.append((a,b,b+14,a+14))
faces.extend([tuple(reversed(range(14))),tuple(range((len(rings)-1)*14,len(rings)*14))])
club=skinned('GoblinClub',vertices,faces)

# Bake procedural bark into a portable albedo; no Blender-only shader is needed in game.
uv=club.data.uv_layers.new(name='ClubUV')
for poly in club.data.polygons:
    js=[club.data.loops[l].vertex_index%14 for l in poly.loop_indices]
    seam=0 in js and 13 in js
    for l in poly.loop_indices:
        index=club.data.loops[l].vertex_index;i,j=divmod(index,14)
        uv.data[l].uv=((14 if seam and j==0 else j)/14,(rings[i][0]+.12)/.8)
nodes=mat.node_tree.nodes;links=mat.node_tree.links
coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeVectorMath');mapping.operation='MULTIPLY';mapping.inputs[1].default_value=(12,12,1.2)
links.new(coord.outputs['Generated'],mapping.inputs[0])
noise=nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=3;noise.inputs['Detail'].default_value=3
links.new(mapping.outputs[0],noise.inputs['Vector'])
ramp=nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.25;ramp.color_ramp.elements[0].color=(.035,.009,.002,1)
ramp.color_ramp.elements[1].position=.76;ramp.color_ramp.elements[1].color=(.3,.12,.027,1)
links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],bsdf.inputs['Base Color'])
baked=bpy.data.images.new('Goblin Club Bark',width=1024,height=1024)
image_node=nodes.new('ShaderNodeTexImage');image_node.image=baked;nodes.active=image_node
bpy.ops.object.select_all(action='DESELECT');club.select_set(True);bpy.context.view_layer.objects.active=club
scene.render.engine='CYCLES';scene.cycles.samples=4;scene.render.bake.use_pass_direct=False;scene.render.bake.use_pass_indirect=False;scene.render.bake.use_pass_color=True
bpy.ops.object.bake(type='DIFFUSE')
baked.filepath_raw=str(unity/'goblin-club-base.png');baked.file_format='PNG';baked.save()
links.new(image_node.outputs['Color'],bsdf.inputs['Base Color'])

for donor,action in donors.values():bpy.data.objects.remove(donor,do_unlink=True)
for action in list(bpy.data.actions):
    if action not in actions.values():bpy.data.actions.remove(action)
rig.data.pose_position='POSE';rig.animation_data.action=actions['Idle'];scene.frame_set(0)
for img in bpy.data.images:
    if img.source=='FILE':
        try:img.pack()
        except RuntimeError:pass

bpy.ops.object.select_all(action='DESELECT')
for obj in [rig,body,glove,club]:obj.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(unity/'forest-goblin.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,
    bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
bpy.ops.wm.save_as_mainfile(filepath=str(output/'forest-goblin.blend'))

def point(obj,target):obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.world.color=(.075,.085,.095)
scene.render.resolution_x=960;scene.render.resolution_y=960;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX'
bpy.ops.object.camera_add(location=(2.2,-4,1.8));cam=bpy.context.object;point(cam,(0,0,.65));scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2
for pos,power,size in [((2,-3,4),300,4),((-3,-1,2),140,3),((1,3,3),220,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos);light=bpy.context.object;light.data.energy=power;light.data.shape='DISK';light.data.size=size;point(light,(0,0,.6))
for name,frame in [('Idle',25),('Walk',12),('AttackWindup',20),('Attack',5),('Hurt',5),('Defeated',27)]:
    rig.animation_data.action=actions[name];scene.frame_set(frame)
    scene.render.filepath=str(output/'previews'/f'{name}.png');bpy.ops.render.render(write_still=True)
report={'rigTask':'01a0749d-0706-763e-b6f9-a494f6f9c256','source':'Meshy private asset, same-rig motions',
    'baseSource':{'file':'goblin-running.glb','sha256':hashlib.sha256((source/'goblin-running.glb').read_bytes()).hexdigest()},
    'donors':provenance,'actions':{n:list(a.frame_range) for n,a in actions.items()},
    'bodyTriangles':sum(len(p.vertices)-2 for p in body.data.polygons),'removedOpenHandVertices':len(remove),
    'clubTriangles':sum(len(p.vertices)-2 for p in club.data.polygons),'gloveTriangles':sum(len(p.vertices)-2 for p in glove.data.polygons),
    'notes':['Root X/Y planted; game owns translation.','AttackWindup and Attack retimed from Meshy Charged Axe Chop.','Hurt is a local Blender additive recoil.','Open right fingers replaced with a closed leather glove; club skinned to RightHand.']}
(output/'preparation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
