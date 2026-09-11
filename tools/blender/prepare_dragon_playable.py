import bpy, math, json
from pathlib import Path
from mathutils import Quaternion, Vector
ROOT=Path(r'C:\work\CoffeeGAME'); ART=ROOT/'art/3d/trials/meshy-split-ink-v11'
OUT=ROOT/'unity/CoffeeGame/Assets/CoffeeGame/Resources/Models/Characters/DragonGirl'
OUT.mkdir(parents=True,exist_ok=True); (ART/'previews').mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ART/'source/dragon-triangle-rig.glb'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
skin=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.vertex_groups)
for o in list(bpy.context.scene.objects):
    if o not in (rig,skin):bpy.data.objects.remove(o,do_unlink=True)
rig.name='DragonRig'; skin.name='DragonGirl'
# Keep split UV vertices. Give the two surfaces of the thin kimono consistent weights:
# automatic body weights otherwise pull the lining through the outer fabric.
def distance_segment(p,a,b):
    delta=b-a;t=max(0,min(1,(p-a).dot(delta)/max(delta.length_squared,.00001)))
    return (p-(a+delta*t)).length
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
for v in skin.data.vertices:
    p=skin.matrix_world@v.co
    weights=None;blend=1
    if p.z>1.35:weights={'Head':1};blend=smooth(1.35,1.45,p.z)
    elif .30<p.z<1.04 and abs(p.x)<.38:
        weights={'Hips':1};blend=smooth(.30,.53,p.z)*(1-smooth(.94,1.04,p.z))*(1-smooth(.32,.38,abs(p.x)))
    if .76<p.z<1.40 and abs(p.x)>.18:
        side='Left' if p.x>0 else 'Right'
        candidates=[side+'Arm',side+'ForeArm',side+'Hand','Spine']
        weights={n:1/max(.025,distance_segment(p,rig.matrix_world@rig.data.bones[n].head_local,rig.matrix_world@rig.data.bones[n].tail_local))**4 for n in candidates}
        blend=smooth(.18,.26,abs(p.x))*smooth(.76,.92,p.z)*(1-smooth(1.30,1.40,p.z))
    if weights:
        total=sum(weights.values());weights={n:w/total*blend for n,w in weights.items()}
        for g in v.groups:
            n=skin.vertex_groups[g.group].name;weights[n]=weights.get(n,0)+g.weight*(1-blend)
        indices=[g.group for g in v.groups]
        for i in indices:skin.vertex_groups[i].remove([v.index])
        total=sum(weights.values())
        for name,w in weights.items():skin.vertex_groups[name].add([v.index],w/total,'REPLACE')
bpy.context.view_layer.objects.active=skin;skin.select_set(True)
# The source is already ~102k triangles. Preserve it for the PC trial.
for im in bpy.data.images:
    if im.name=='texture_0':im.filepath_raw=str(OUT/'dragon-basecolor.png');im.file_format='PNG';im.save()
    if im.name=='texture_0_normal':im.filepath_raw=str(OUT/'dragon-normal.png');im.file_format='PNG';im.save()
for mat in skin.data.materials:
    bsdf=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bsdf:
        for inp in ['Metallic','Roughness']:
            for link in list(bsdf.inputs[inp].links):mat.node_tree.links.remove(link)
        bsdf.inputs['Metallic'].default_value=.08;bsdf.inputs['Roughness'].default_value=.55

# Retain Meshy's animation as a source take for comparison, then author actions on this exact rest rig.
existing=set(bpy.data.objects);bpy.ops.import_scene.gltf(filepath=str(ART/'source/dragon-run.glb'))
source_actions=list(bpy.data.actions)
for a in source_actions:a.name='MeshyRunSource';a.use_fake_user=True
for o in list(bpy.data.objects):
    if o not in existing:bpy.data.objects.remove(o,do_unlink=True)
rig.animation_data_create();clips={}
def qworld(rx=0,ry=0,rz=0):
    return Quaternion((0,0,1),math.radians(rz))@Quaternion((0,1,0),math.radians(ry))@Quaternion((1,0,0),math.radians(rx))
def pose(rotations,drop=0):
    for b in rig.pose.bones:b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion();b.location=Vector((0,0,0))
    bpy.context.view_layer.update()
    for bone in rig.pose.bones:
        target=qworld(*rotations.get(bone.name,(0,0,0))) @ bone.bone.matrix_local.to_quaternion()
        basis=bone.bone.matrix_local.to_quaternion()
        if bone.parent:basis=bone.parent.matrix.to_quaternion()@bone.parent.bone.matrix_local.to_quaternion().inverted()@basis
        bone.rotation_quaternion=basis.inverted()@target
        bpy.context.view_layer.update()
    rig.pose.bones['Hips'].location=rig.data.bones['Hips'].matrix_local.to_3x3().inverted()@Vector((0,0,drop))
def sample(name,t):
    d={};drop=0; pulse=math.sin(t*math.pi)
    if name in ['Idle','Walk','Run']:
        speed={'Idle':0,'Walk':25,'Run':52}[name];s=math.sin(t*math.tau);lean=22 if name=='Run' else 4
        for b in ['Hips','Spine02','Spine01','Spine','neck','Head']:d[b]=(lean,0,0)
        for side,sign in [('Left',1),('Right',-1)]:
            stride=s*sign;d[side+'UpLeg']=(-stride*speed,0,sign*3);d[side+'Leg']=(-stride*speed+max(0,-stride)*65,0,sign*3)
            d[side+'Foot']=(-stride*speed*.25,0,0);d[side+'Arm']=(stride*speed*.65,0,sign*-12);d[side+'ForeArm']=(stride*speed*.65-48 if name=='Run' else -10,0,sign*-12)
        drop=abs(s)*.025 if speed else math.sin(t*math.tau)*.006
    elif name in ['Jump','Fall','AirSlash','AirClaw','Plunge','Land','StompLand','Dodge','Hurt','Defeated']:
        crouch=.8 if name in ['Land','StompLand'] else .45
        d={'Hips':(20,0,0),'Spine02':(28,0,0),'Spine01':(32,0,0),'Spine':(32,0,0),'Head':(20,0,0)}
        for side,sign in [('Left',1),('Right',-1)]:
            d[side+'UpLeg']=(-65*crouch,0,sign*12);d[side+'Leg']=(65*crouch,0,sign*12);d[side+'Foot']=(0,0,0)
            d[side+'Arm']=(-65,0,sign*20);d[side+'ForeArm']=(-105,0,sign*10)
        drop=-.18*crouch
        if name=='Plunge':d['RightUpLeg']=(15,0,-5);d['RightLeg']=(5,0,-5);d['RightFoot']=(5,0,0)
        if name=='Dodge':d['Hips']=(t*360,0,0);drop=-.3
        if name=='Defeated':d={b.name:(85,0,0) for b in rig.pose.bones};drop=-.68
    else:
        # An open-hand martial guard; the heavy strike travels above the head then across the torso.
        active='Left' if name=='ClawLeft' else 'Right';sign=1 if active=='Left' else -1
        strike=max(0,min(1,(t-.24)/.22));relax=max(0,min(1,(t-.72)/.28))
        arc=(1-relax)*(-65+155*strike)
        heavy=name in ['ClawHeavy','DragonChop','SpinRelease'];gate=name in ['DragonGate','MagicRelease','MagicCharge','SpinCharge','DragonBreath']
        d={'Hips':(8,0,sign*arc*.18),'Spine02':(12,0,sign*arc*.25),'Spine01':(16,0,sign*arc*.35),'Spine':(18,0,sign*arc*.4),'Head':(5,0,sign*arc*.12)}
        for side,s in [('Left',1),('Right',-1)]:
            d[side+'UpLeg']=(-15,0,s*8);d[side+'Leg']=(15,0,s*8);d[side+'Foot']=(0,0,0)
            d[side+'Arm']=(-50,0,s*-8);d[side+'ForeArm']=(-90,0,s*-20)
        if gate:
            for side,s in [('Left',1),('Right',-1)]:d[side+'Arm']=(-85*pulse,0,s*10);d[side+'ForeArm']=(-110*pulse,0,s*5)
        elif heavy:
            d[active+'Arm']=(-150+170*strike,0,sign*20);d[active+'ForeArm']=(-170+200*strike,0,sign*10);d['Spine']=(10+28*strike,0,sign*arc*.3)
        else:
            d[active+'Arm']=(-80,0,sign*arc);d[active+'ForeArm']=(-115,0,sign*(arc-15));d[active+'Hand']=(-110,0,sign*arc)
        drop=-.05-(.08*strike if heavy else 0)
    return d,drop
names={'Idle':2,'Walk':.9,'Run':.65,'Jump':.5,'Fall':.7,'Land':.6,'Dodge':.7,'Hurt':.3,'Defeated':1,'Sword':.42,'AirSlash':.6,'Plunge':.6,'SpinCharge':.7,'SpinRelease':.85,'MagicCharge':.75,'MagicRelease':.75,'ClawRight':.42,'ClawLeft':.42,'ClawHeavy':.85,'DragonGate':.75,'DragonChop':1.45,'DragonBreath':.7,'AirClaw':.6,'StompLand':.6}
for name,duration in names.items():
    action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
    frames=max(3,round(duration*30));clips[name]=frames
    for frame in range(frames+1):
        bpy.context.scene.frame_set(frame+1);d,drop=sample(name,frame/frames);pose(d,drop)
        for b in rig.pose.bones:b.keyframe_insert('rotation_quaternion',frame=frame+1,group=b.name)
        rig.pose.bones['Hips'].keyframe_insert('location',frame=frame+1,group='Hips')
    track=rig.animation_data.nla_tracks.new();track.name=name;strip=track.strips.new(name,1,action);strip.action_frame_end=frames+1;track.mute=True
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.context.scene.render.fps=30
# Export only the character. Blender source retains all named animation takes.
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);skin.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'exports/dragon-playable.blend'))
bpy.ops.export_scene.fbx(filepath=str(OUT/'dragon-girl.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=.1,path_mode='COPY',embed_textures=False,axis_forward='-Z',axis_up='Y')
(ART/'exports/playable-report.json').write_text(json.dumps({'vertices':len(skin.data.vertices),'triangles':len(skin.data.polygons),'clips':clips},indent=2))
# Silent preview renders.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.render.resolution_x=700;scene.render.resolution_y=850;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.11,.15,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.7
for position,power,size in [((3,-4,5),650,5),((-3,-1,3),450,4),((0,3,4),850,3)]:
    bpy.ops.object.light_add(type='AREA',location=position);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(2,-4,2.1));camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.ortho_scale=2.15;camera.rotation_euler=(Vector((0,0,.87))-camera.location).to_track_quat('-Z','Y').to_euler()
for name,frame in [('Idle',1),('Run',5),('ClawHeavy',13),('DragonChop',20),('StompLand',8)]:
    rig.animation_data.action=bpy.data.actions[name];scene.frame_set(frame);scene.render.filepath=str(ART/'previews'/f'playable-{name}.png');bpy.ops.render.render(write_still=True)
