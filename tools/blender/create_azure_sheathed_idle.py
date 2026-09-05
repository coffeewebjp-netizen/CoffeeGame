"""Add a fitted rigid saya and a four-second breathing idle to the accepted V5 model."""
import argparse, hashlib, importlib.util, json, math, shutil, sys
from pathlib import Path
import bpy
from mathutils import Matrix, Quaternion, Vector


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-blend',type=Path,required=True)
    parser.add_argument('--out-dir',type=Path,required=True)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=args.out_dir.resolve()
    if out.exists() and any(out.iterdir()): raise RuntimeError('Use a new empty output directory')
    out.mkdir(parents=True,exist_ok=True)
    spec=importlib.util.spec_from_file_location('clean',Path(__file__).with_name('prepare_azure_maiden_clean.py'))
    clean=importlib.util.module_from_spec(spec);spec.loader.exec_module(clean)
    bpy.ops.wm.open_mainfile(filepath=str(args.source_blend.resolve()))
    rig=next(o for o in bpy.data.objects if o.type=='ARMATURE')
    world=rig.matrix_world.copy();inv=world.inverted();rig_rotation=world.to_quaternion()
    rest={b.name:world@b.matrix_local for b in rig.data.bones}
    def action_digest(action):
        return hashlib.sha256(repr([(f.data_path,f.array_index,[(tuple(k.co),k.interpolation,tuple(k.handle_left),tuple(k.handle_right)) for k in f.keyframe_points])
          for layer in action.layers for strip in layer.strips for bag in strip.channelbags for f in bag.fcurves]).encode()).hexdigest()
    before={a.name:action_digest(a) for a in bpy.data.actions if a.name!='Idle'}
    original_body=next(o for o in bpy.data.objects if o.type=='MESH' and o.name=='AzureMaidenBody') if bpy.data.objects.get('AzureMaidenBody') else next(o for o in bpy.data.objects if o.type=='MESH' and not o.name.startswith(('PreviewOnly','AzureMaidenKatana')))
    body_hash=hashlib.sha256(repr([tuple(v.co) for v in original_body.data.vertices]).encode()).hexdigest()
    clean.assign_action(rig,None)
    bpy.data.actions.remove(bpy.data.actions['Idle'])
    idle=bpy.data.actions.new('Idle');idle.use_fake_user=True
    clean.assign_action(rig,idle)
    right_target=Vector((0.11,-0.19,1.03))
    left_target=Vector((0.26,-0.04,0.91))
    blade_axis=Vector((0.72,0.60,-0.34)).normalized()
    right_rotation=blade_axis.to_track_quat('Y','Z')
    def set_world(name,position,rotation):
        rig.pose.bones[name].matrix=Matrix.LocRotScale(inv@position,rig_rotation.inverted()@rotation,Vector((1,1,1)))
        bpy.context.view_layer.update()
    def arm_ik(side,target,pole,hand_rotation=None):
        names=[side+'Arm',side+'ForeArm',side+'Hand']
        root=(world@rig.pose.bones[names[0]].matrix).translation
        a=(rest[names[1]].translation-rest[names[0]].translation).length
        b=(rest[names[2]].translation-rest[names[1]].translation).length
        offset=target-root;distance=offset.length;axis=offset.normalized()
        if distance>a+b-0.001: raise RuntimeError(f'{side} hand cannot reach hilt: {distance} vs {a+b}')
        along=(a*a-b*b+distance*distance)/(2*distance)
        sideways=(pole-root)-axis*(pole-root).dot(axis)
        elbow=root+axis*along+sideways.normalized()*math.sqrt(max(0,a*a-along*along))
        upper_rotation=(rest[names[1]].translation-rest[names[0]].translation).rotation_difference(elbow-root)@rest[names[0]].to_quaternion()
        lower_rotation=(rest[names[2]].translation-rest[names[1]].translation).rotation_difference(target-elbow)@rest[names[1]].to_quaternion()
        set_world(names[0],root,upper_rotation)
        set_world(names[1],elbow,lower_rotation)
        if hand_rotation is None:
            hand_rotation=lower_rotation@rest[names[1]].to_quaternion().inverted()@rest[names[2]].to_quaternion()
        set_world(names[2],target,hand_rotation)
    previous={};samples=[]
    for frame in range(1,122):
        clean.clear_pose(rig)
        phase=(frame-1)/120*math.tau
        breath=0.5-0.5*math.cos(phase)
        rig.pose.bones['Spine02'].rotation_quaternion=Quaternion((1,0,0),math.radians(-0.5-0.4*breath))
        rig.pose.bones['Spine01'].rotation_quaternion=Quaternion((1,0,0),math.radians(0.8*breath))
        rig.pose.bones['Spine'].rotation_quaternion=Quaternion((1,0,0),math.radians(1.0*breath))
        rig.pose.bones['Spine'].scale=(1+0.012*breath,1+0.006*breath,1+0.009*breath)
        rig.pose.bones['neck'].rotation_quaternion=Quaternion((0,1,0),math.radians(-3))@Quaternion((1,0,0),math.radians(-0.35*breath))
        bpy.context.view_layer.update()
        arm_ik('Right',right_target,Vector((-0.27,-0.22,1.08)),right_rotation)
        arm_ik('Left',left_target,Vector((0.29,-0.10,1.10)))
        for bone in rig.pose.bones:
            q=bone.rotation_quaternion.copy()
            if bone.name in previous:q.make_compatible(previous[bone.name])
            bone.rotation_quaternion=q;previous[bone.name]=q.copy()
        clean.key_pose(rig,frame)
        if frame in (1,31,61,91,121):
            samples.append({'frame':frame,'rightHand':list((world@rig.pose.bones['RightHand'].matrix).translation),'head':list((world@rig.pose.bones['Head'].matrix).translation)})
    bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
    # This uses the same canonical blade coordinates and hand offset as V4.
    # The blade fits inside the saya; only the hilt and guard remain exposed.
    sword_world=Matrix.LocRotScale(right_target,right_rotation,Vector((1,1,1)))@Matrix.Translation((0,-0.015,-0.015))
    vertices=[];faces=[];material_indices=[]
    sections=[]
    for i in range(14):
        t=i/13
        y=0.112+0.811*t
        x=-0.036*(min(1,(y-0.108)/0.742)**1.65)
        width=0.026*(1-0.18*t)
        thickness=0.019*(1-0.22*t)
        sections.append((y,x,width,thickness))
    for y,x,width,thickness in sections:
        for i in range(12):
            angle=i*math.tau/12
            vertices.append((x+width*math.cos(angle),y,thickness*math.sin(angle)))
    for ring in range(len(sections)-1):
        for i in range(12):
            a=ring*12+i;b=ring*12+(i+1)%12
            faces.append((a,a+12,b+12,b));material_indices.append(1 if ring in (0,12) else 0)
    faces.append(tuple(reversed(range((len(sections)-1)*12,len(sections)*12))));material_indices.append(1)
    # Recessed mouth lip, with an open blade slot.
    outer=vertices[:12];inner_start=len(vertices)
    for v in outer:vertices.append((v[0]*0.77,v[1]+0.002,v[2]*0.72))
    for i in range(12):
        faces.append((i,(i+1)%12,inner_start+(i+1)%12,inner_start+i));material_indices.append(1)
    mesh=bpy.data.meshes.new('AzureMaidenSayaMesh')
    mesh.from_pydata([inv@sword_world@Vector(v) for v in vertices],[],faces);mesh.update()
    saya=bpy.data.objects.new('AzureMaidenSaya',mesh);bpy.context.collection.objects.link(saya)
    saya.parent=rig;saya.matrix_parent_inverse=Matrix.Identity(4);saya.matrix_basis=Matrix.Identity(4)
    for name,color,metallic,roughness in [('SayaBlackLacquer',(0.014,0.009,0.02,1),0.2,0.28),('SayaBrassFittings',(0.28,0.135,0.035,1),0.75,0.3)]:
        m=bpy.data.materials.new(name);m.diffuse_color=color;m.metallic=metallic;m.roughness=roughness;mesh.materials.append(m)
    for polygon,index in zip(mesh.polygons,material_indices):polygon.material_index=index;polygon.use_smooth=True
    vg=saya.vertex_groups.new(name='Hips');vg.add(range(len(vertices)),1.0,'REPLACE')
    saya.modifiers.new('RigidWaistSkin','ARMATURE').object=rig
    assert before=={a.name:action_digest(a) for a in bpy.data.actions if a.name!='Idle'}
    assert body_hash==hashlib.sha256(repr([tuple(v.co) for v in original_body.data.vertices]).encode()).hexdigest()
    (out/'textures').mkdir()
    for image in bpy.data.images:
        if image.name=='AzureMaidenDirectRetexture':
            target=out/'textures/azure-maiden-base.png';shutil.copyfile(Path(bpy.path.abspath(image.filepath)),target);image.filepath=image.filepath_raw=str(target)
    blend=out/'azure-maiden-clean-runtime.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend),compress=True)
    exports=clean.export_runtime(rig,out)
    report={'taskId':'ORC-20260905-001','workPackage':'WP20','sourceSha256':clean.sha256(args.source_blend),'changedActions':['Idle'],'preservedActions':15,'bodyVerticesUnchanged':True,'idleSeconds':4,'sayaVertices':len(vertices),'sayaTriangles':sum(len(p.vertices)-2 for p in mesh.polygons),'sayaSkin':'100% Hips','samples':samples,'blendSha256':clean.sha256(blend),**exports}
    (out/'sheathed-idle.json').write_text(json.dumps(report,indent=2)+'\n')
    bpy.context.scene.render.resolution_x=bpy.context.scene.render.resolution_y=768
    for side in ('front','side'):
        clean.render_action(rig,'Idle',1,out/f'idle-{side}.png',side)
    clean.render_action(rig,'Idle',61,out/'idle-inhale.png')
    print('SHEATHED_IDLE_OK',json.dumps(report))


if __name__=='__main__':main()
