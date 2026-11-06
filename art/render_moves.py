"""Bake motion.json into editable Blender actions and render a short move reel.

blender -b dist/TheRegular.blend --python-exit-code 1 --python art/render_moves.py -- --frames /tmp/cuige-motion
python art/encode_moves.py /tmp/cuige-motion
"""
import json
import os
import sys
from bisect import bisect_right
from math import ceil, pi
from pathlib import Path
import bpy
from mathutils import Quaternion, Vector

root=Path(__file__).resolve().parents[1]
clips={c["name"]:c for c in json.loads((root/"art/generated/motion.json").read_text())["clips"]}
objects=json.loads((root/"art/generated/props.json").read_text())["parts"]
scene=bpy.context.scene
rig=bpy.data.objects["Training rig"]
fps=18
scene.render.fps=fps
bpy.context.preferences.filepaths.save_version=0
args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
frames=Path(args[args.index("--frames")+1]) if "--frames" in args else None
review=Path(args[args.index("--inspect")+1]) if "--inspect" in args else None
if frames: frames.mkdir(parents=True, exist_ok=True)
if review: review.mkdir(parents=True, exist_ok=True)

def basis(v):
    x,y,z=v
    return Vector((-x,-z,y))

def mapped(q):
    x,y,z,w=q
    return Quaternion((w,x,z,-y)).normalized()

def aslinear(c): return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4

bpy.context.view_layer.objects.active=rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for kind in ("Plate","Shaker"):
    ed=rig.data.edit_bones.new(kind)
    ed.head=(0,0,0); ed.tail=(0,0,.1); ed.use_deform=True
bpy.ops.object.mode_set(mode="OBJECT")
# Keep the medial rim of the deltoid attached to the chest while the outer
# shoulder tracks the arm. The same bind-space function is used in-game.
for ob in rig.children:
    if ob.type!="MESH":
        continue
    arm=next((g for g in ob.vertex_groups if g.name in ("LeftUpperArm","RightUpperArm")),None)
    if arm is None:
        continue
    chest=ob.vertex_groups.get("Chest") or ob.vertex_groups.new(name="Chest")
    for vertex in ob.data.vertices:
        x=abs(vertex.co.x)
        t=max(0,min(1,(x-.158)/.126))
        w=.78*(1-t*t*(3-2*t))
        if w>0:
            chest.add([vertex.index],w,"REPLACE")
            arm.add([vertex.index],1-w,"REPLACE")
prop_collection=bpy.data.collections.new("Equipment • animated with each action")
scene.collection.children.link(prop_collection)
mats={}
for pi_,part in enumerate(objects):
    raw=part["vertices"]; tris=part["triangles"]; key=tuple(part["color"])
    verts=[basis(raw[i:i+3]) for i in range(0,len(raw),3)]
    faces=[tuple(reversed(tris[i:i+3])) for i in range(0,len(tris),3)]
    m=bpy.data.meshes.new(part["name"]); m.from_pydata(verts,[],faces); m.update()
    ob=bpy.data.objects.new(part["name"],m); prop_collection.objects.link(ob)
    if key not in mats:
        ma=bpy.data.materials.new("Equipment / "+"".join('%02x'%round(255*v) for v in key))
        ma.diffuse_color=tuple(aslinear(v) for v in key)+(1,)
        ma.use_nodes=True; nodes=ma.node_tree.nodes
        shader=next((n for n in nodes if n.type=="BSDF_PRINCIPLED"),None)
        if shader is None:
            shader=nodes.new("ShaderNodeBsdfPrincipled")
            output=next((n for n in nodes if n.type=="OUTPUT_MATERIAL"),None) or nodes.new("ShaderNodeOutputMaterial")
            ma.node_tree.links.new(shader.outputs[0],output.inputs["Surface"])
        shader.inputs["Base Color"].default_value=ma.diffuse_color
        shader.inputs["Roughness"].default_value=.45
        shader.inputs["Metallic"].default_value=.45 if part["kind"]=="plate" else .08
        mats[key]=ma
    m.materials.append(mats[key]); ob.parent=rig
    name=part["kind"].title(); group=ob.vertex_groups.new(name=name); group.add(list(range(len(verts))),1,"REPLACE")
    mod=ob.modifiers.new("Prop control","ARMATURE"); mod.object=rig

rb={b.name:b.matrix_local.to_quaternion().normalized() for b in rig.data.bones}
bindpos={b.name:b.head_local.copy() for b in rig.data.bones}
parents={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
grip={"LeftHand":basis((-.035,-.032,.015)), "RightHand":basis((.035,-.032,.015))}
idata=rig.animation_data_create()
actions={}
def make_action(name):
    action=bpy.data.actions.new(name);action.use_fake_user=True
    idata.action=action
    return action

def evaluate(clip,t):
    qt={};p=Vector((0,0,0))
    for tr in clip["tracks"]:
        b=tr["bone"]; keys=tr["keys"]
        i=max(0,min(len(keys)-2,bisect_right([k["t"] for k in keys],t)-1))
        ka,kb=keys[i:i+2];frac=max(0.,min(1.,(t-ka["t"])/(kb["t"]-ka["t"])))
        qt[b]=mapped(ka["r"]).slerp(mapped(kb["r"]),frac)
        if b=="Hips":p=basis(ka["p"]).lerp(basis(kb["p"]),frac)
    wpos={};wrot={}
    def fk(b):
        if b not in wpos:
            par=parents[b]
            if par:
                fk(par)
                wpos[b]=wpos[par]+wrot[par]@(bindpos[b]-bindpos[par]);wrot[b]=wrot[par]@qt[b]
            else: wpos[b]=bindpos[b]+p;wrot[b]=qt[b]
        return wpos[b]
    for b in grip: fk(b)
    palms={b:wpos[b]+wrot[b]@offset for b,offset in grip.items()}
    return qt,p,palms

def bake_pose(clip,t,frame,prev):
    qt,p,palm=evaluate(clip,t)
    for b in rb:
        ctrl=rig.pose.bones[b]
        ctrl.rotation_mode="QUATERNION"
        q=rb[b].inverted()@qt.get(b,Quaternion())@rb[b]
        if b in prev:q.make_compatible(prev[b])
        prev[b]=q.copy()
        ctrl.rotation_quaternion=q
        ctrl.keyframe_insert("rotation_quaternion",frame=frame,group=b)
        if b=="Hips":
            ctrl.location=rb[b].inverted()@p
            ctrl.keyframe_insert("location",frame=frame,group=b)
        if b in ("Plate","Shaker"):
            v=(palm["LeftHand"]+palm["RightHand"])*.5 if b=="Plate" else palm["RightHand"]
            visible=b in qt
            if b=="Shaker" and clip["name"]=="throw" and t>=.42:
                # Equipment leaves the right hand at the in-game release frame.
                # In the reel, follow a short ballistic demonstration of the pass.
                _,_,release=evaluate(clip,.42)
                sec=(t-.42)*clip["duration"]
                v=release["RightHand"]+basis((-.16*sec,1.28*sec-3.7*sec**2,2.7*sec))
                visible=t<.89
            ctrl.location=rb[b].inverted()@v
            ctrl.scale=(1,1,1) if visible else (.00001,.00001,.00001)
            ctrl.keyframe_insert("location",frame=frame,group=b)
            ctrl.keyframe_insert("scale",frame=frame,group=b)

def interpolation(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for k in fc.keyframe_points:
                        k.interpolation="CONSTANT" if fc.data_path.endswith(".scale") else "LINEAR"

# Editable individual actions use authored keys for the body plus a 30 fps
# equipment solve, so both palms stay on the disc throughout the entire stroke.
for name,c in clips.items():
    actions[name]=action=make_action("Cui Ge / "+name)
    samples=ceil(30*c["duration"])
    times={i/samples for i in range(samples+1)}
    times.update(k["t"] for tr in c["tracks"] for k in tr["keys"])
    prev={}
    for t in sorted(times):
        bake_pose(c,t,1+t*c["duration"]*fps,prev)
    interpolation(action)
    action["duration_seconds"]=c["duration"];action["looping"]=c["loop"]

# A single continuous selectable action makes the file immediately playable.
# Each attack receives a little extra screen time so the contacts can be read.
sequence=[("spawn",.76,0,1),("idle",.46,0,.23),("jab",.54,0,1),("cross",.52,0,1),
          ("uppercut",.73,0,1),("ready",.98,0,1),("slam",.96,0,1),
          ("slamHeavy",1.04,0,1),("charge",.85,0,1),("drink",1.12,0,1),
          ("aim",.64,0,.39),("throw",.84,0,1),("run",.94,0,1),
          ("air",.48,0,1),("land",.39,0,1),("lobby",1.2,.12,.72),("hit",.41,0,1),("death",1.22,0,1)]
show=make_action("00 • 崔哥 / Move reel"); frame=1; shots=[];prev={}
for name,length,t0,t1 in sequence:
    count=max(3,round(fps*length)); first=frame
    scene.timeline_markers.new(name,frame=first)
    for i in range(count):
        t=t0+(t1-t0)*i/(count-1)
        bake_pose(clips[name],t,frame,prev)
        frame+=1
    shots.append(dict(id=name,first=first,last=frame-1))
interpolation(show)
scene.frame_start=1;scene.frame_end=frame-1;scene.render.fps=fps

# Simple, bright studio footage preserves the actual material colors and lets
# the opaque plate and white shaker stand out against the dark tank top.
cam=scene.camera
cam.location=(2.8,-3.4,1.76)
cam.rotation_euler=(Vector((0,-.111,.99))-cam.location).to_track_quat("-Z","Y").to_euler()
cam.data.type="ORTHO";cam.data.ortho_scale=2.23
scene.render.resolution_x=450;scene.render.resolution_y=424
scene.render.resolution_percentage=100
# The character falls across the floor on death. Follow the fall with the
# framing so the resting head, hands and boots remain visible in the reel.
death_shot=next(shot for shot in shots if shot["id"]=="death")
home=cam.location.copy()
cam.keyframe_insert("location",frame=1)
cam.keyframe_insert("location",frame=death_shot["first"])
cam.location=home+Vector((.33,-.005,-.36))
cam.keyframe_insert("location",frame=round(death_shot["first"]+.78*(death_shot["last"]-death_shot["first"])))
cam.keyframe_insert("location",frame=death_shot["last"])
scene.frame_set(1)
for area in bpy.context.screen.areas:
    if area.type=="VIEW_3D":
        area.spaces.active.region_3d.view_perspective="CAMERA"
        area.spaces.active.overlay.show_overlays=False
        area.spaces.active.shading.type="SOLID"
        area.spaces.active.shading.color_type="MATERIAL"
bpy.ops.wm.save_as_mainfile(filepath=str(root/"dist/CuiGe-Moves.blend"),compress=True)
if frames or review:
    scene.render.engine="CYCLES";scene.cycles.device="CPU";scene.cycles.samples=3
    scene.cycles.use_denoising=True;scene.render.threads_mode="FIXED";scene.render.threads=7
    scene.render.image_settings.file_format="PNG"
if review:
    # Pose contacts at the player's normal viewing distance.
    for shot in shots:
        if shot["id"] not in ("cross","ready","spawn","hit","death","slamHeavy","charge","idle"):continue
        t={"cross":.281,"ready":.31,"spawn":0,"hit":.28,"death":.89,"slamHeavy":.2,
           "charge":.27,"idle":.0}[shot["id"]]
        f=round(shot["first"]+(shot["last"]-shot["first"])*t)
        scene.frame_set(f);scene.render.filepath=str(review/(shot["id"]+".png"))
        bpy.ops.render.render(write_still=True)
if frames:
    (frames/"sequence.json").write_text(json.dumps({"fps":fps,"shots":shots},ensure_ascii=False))
    scene.render.use_persistent_data=True
    scene.render.filepath=str(frames/"frame-")
    if "--only" in args:
        which=args[args.index("--only")+1]
        shot=next(s for s in shots if s["id"]==which)
        scene.frame_start,scene.frame_end=shot["first"],shot["last"]
    bpy.ops.render.render(animation=True)
print("Baked",len(actions),"actions;",scene.frame_end,"reel frames")
