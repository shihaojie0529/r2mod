"""Shared Blender exporter and preview stage for the game sculpt format."""
import json
import sys
from pathlib import Path
from tempfile import TemporaryDirectory

import bmesh
import bpy
from mathutils import Vector

here=Path(__file__).resolve().parent
sys.path.insert(0,str(here))


def bake(name, pivots, build, stance, views, floor_height=.004, matte_shadows=False):
    """The portrait, presentation render and game runtime all derive from this mesh."""
    dist=here.parent/"dist"
    gen=here/"generated"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene
    def xyz(v):
        return Vector((-v[0],-v[2],v[1]))
    def linear(c):
        return c/12.92 if c<.04045 else ((c+.055)/1.055)**2.4

    materials={}
    def mat(rgb):
        key=tuple(rgb)
        if key in materials:
            return materials[key]
        swatch="Pigment "+"".join(f"{round(c*255):02X}" for c in rgb)
        m=bpy.data.materials.new(swatch)
        m.diffuse_color=tuple(linear(c) for c in rgb)+(1,)
        m.use_nodes=True
        shader=m.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value=m.diffuse_color
        shader.inputs["Roughness"].default_value=.7
        shader.inputs["Specular IOR Level"].default_value=.21
        if matte_shadows and max(rgb)<.18 and max(rgb)-min(rgb)<.05:
            shader.inputs["Specular IOR Level"].default_value=.10
            shader.inputs["Roughness"].default_value=.85
        if rgb[1]>.84 and rgb[0]<.62:
            shader.inputs["Emission Color"].default_value=m.diffuse_color
            shader.inputs["Emission Strength"].default_value=.6
        materials[key]=m
        return m

    parents={"Chest":"Hips","Head":"Chest"}
    for side in ("Right","Left"):
        for bone,parent in (("UpperArm","Chest"),("LowerArm",side+"UpperArm"),("Hand",side+"LowerArm"),
                            ("UpperLeg","Hips"),("LowerLeg",side+"UpperLeg"),("Foot",side+"LowerLeg")):
            parents[side+bone]=parent
    PIVOTS={joint:stance(point,joint) for joint,point in pivots.items()}
    armature=bpy.data.armatures.new(name+" skeleton")
    rig=bpy.data.objects.new(name,armature)
    scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for joint,position in PIVOTS.items():
        bone=armature.edit_bones.new(joint)
        bone.head=xyz(position)
        child=next((name for name in PIVOTS if parents.get(name)==joint),None)
        tail=xyz(PIVOTS[child]) if child else bone.head+Vector((0,-.08,0))
        if joint=="Chest": tail=xyz(PIVOTS["Head"])
        if joint=="Head": tail=bone.head+Vector((0,0,.29))
        bone.tail=tail
        if joint in parents:
            bone.parent=armature.edit_bones[parents[joint]]
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    armature.display_type="STICK"
    parts=[]
    objects=[]
    def add(name,vs,fs,rgb,joint):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata([xyz(stance(p,joint)) for p in vs],[],[list(reversed(f)) for f in fs])
        mesh.validate()
        bm=bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=0.000002)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        # Open patches have an intended exterior even when they don't enclose a volume.
        if any(not edge.is_manifold for edge in bm.edges):
            mid=xyz((0,1.66,-.004)) if joint=="Head" else xyz(PIVOTS[joint])
            if "sleeve stripe" in name:
                side="Left" if joint.startswith("Left") else "Right"
                mid=xyz(PIVOTS[side+"UpperArm"]).lerp(xyz(PIVOTS[side+"LowerArm"]),.25)
            if name.startswith(("Eye","Brow","Mouth","Nose")):
                score=sum(f.normal.dot(Vector((0,-1,0)))*f.calc_area() for f in bm.faces)
            else:
                score=sum(f.normal.dot(f.calc_center_median()-mid)*f.calc_area() for f in bm.faces)
            if score<0:
                bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bm.to_mesh(mesh); bm.free()
        mesh.update()
        smooth=not name.startswith(("Light pistol","Replay pack","Belt / clasp","Hair / swept","Hair / lock","Blade /","Sword /","Case /"))
        for polygon in mesh.polygons:
            polygon.use_smooth=smooth
        obj=bpy.data.objects.new(name,mesh)
        scene.collection.objects.link(obj)
        obj.parent=rig
        group=obj.vertex_groups.new(name=joint)
        group.add(list(range(len(mesh.vertices))),1,"REPLACE")
        modifier=obj.modifiers.new("Survivor rig","ARMATURE")
        modifier.object=rig
        mesh.materials.append(mat(rgb))
        objects.append(obj)
        # Unity reverses the handedness of this basis. Keep authored normals, including
        # flat machined corners on the pistol and soft shared normals on the face.
        mesh.calc_loop_triangles()
        verts=[]; norms=[]; indices=[]; unique={}
        for tri in mesh.loop_triangles:
            for loop in reversed(tri.loops):
                v=mesh.vertices[mesh.loops[loop].vertex_index]
                normal=v.normal if smooth else tri.normal
                point=tuple(round(q,5) for q in (-v.co.x,v.co.z,-v.co.y))
                direction=tuple(round(q,4) for q in (-normal.x,normal.z,-normal.y))
                key=(point,direction)
                if key not in unique:
                    unique[key]=len(verts)//3; verts.extend(point); norms.extend(direction)
                indices.append(unique[key])
        parts.append(dict(bone=joint,tint=[round(c*255) for c in rgb],vertices=verts,normals=norms,triangles=indices))

    build(add)
    assert set(part["bone"] for part in parts)==set(PIVOTS)
    for p in parts:
        assert len(p["vertices"])==len(p["normals"]) and max(p["triangles"])<len(p["vertices"])//3
    (gen/(name.lower()+"-model.json")).write_text(json.dumps(dict(joints=[dict(name=k,pivot=v) for k,v in PIVOTS.items()],
                                      parts=parts),separators=(",",":")),encoding="utf-8")
    # One skinned mesh, keeping named color swatches and joint weights in the editable GLB.
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.active_object; obj.name=name+" / skinned model"
    rig.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(dist/(name+".glb")),export_format="GLB",use_selection=True,
                              export_animations=False)

    scene.render.engine="CYCLES"
    scene.cycles.device="CPU"
    scene.cycles.samples=14
    scene.cycles.use_denoising=True
    scene.render.threads_mode="FIXED"
    scene.render.threads=6
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.world=bpy.data.worlds.new("Teal studio")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value=(.19,.24,.32,1); bg.inputs[1].default_value=.56
    def look(obj,at):
        obj.rotation_euler=(Vector(at)-obj.location).to_track_quat("-Z","Y").to_euler()
    def softbox(name,pos,at,power,rgb,size):
        data=bpy.data.lights.new(name,"AREA")
        data.energy=power; data.color=rgb; data.shape="DISK"; data.size=size
        obj=bpy.data.objects.new(name,data)
        scene.collection.objects.link(obj); obj.location=pos; look(obj,at)
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,floor_height))
    floor=bpy.context.object; floor.data.materials.append(mat((.157,.221,.263)))
    softbox("Large key",(-2.1,-3.6,4.7),(0,0,1.2),390,(1,.89,.80),3.1)
    softbox("Face bounce",(2,-3,2.2),(0,0,1.5),200,(.77,.87,1),2.9)
    softbox("Turquoise rim",(-1.7,2.7,3.2),(0,0,1.4),700,(.42,.91,1),2.1)
    softbox("Hair rim",(1.9,1.3,3.5),(0,0,1.4),390,(.94,.78,.62),2.0)
    cam=bpy.data.objects.new("Preview camera",bpy.data.cameras.new("Preview camera"))
    scene.collection.objects.link(cam); scene.camera=cam; cam.data.type="ORTHO"
    def render(pos,at,zoom,width,height,path,fmt):
        cam.location=pos; look(cam,at); cam.data.ortho_scale=zoom
        scene.render.resolution_x=width; scene.render.resolution_y=height
        scene.render.image_settings.file_format=fmt
        scene.render.image_settings.quality=94
        scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
    render(*views[0],256,256,gen/(name.lower()+"-portrait.png"),"PNG")
    with TemporaryDirectory() as temp:
        paths=[]
        for i,(position,target,zoom,width) in enumerate(views[1:]):
            path=Path(temp)/f"panel{i}.png"
            render(position,target,zoom,width,740,path,"PNG")
            paths.append(path)
        import numpy as np
        def pixels(im):
            result=np.zeros(im.size[0]*im.size[1]*4,dtype=np.float32)
            im.pixels.foreach_get(result)
            return result.reshape(im.size[1],im.size[0],4)
        divider=np.zeros((740,4,4),dtype=np.float32)
        divider[:]=(.07,.21,.22,1)
        frames=[]
        for path in paths:
            if frames: frames.append(divider)
            frames.append(pixels(bpy.data.images.load(str(path))))
        sheet=np.concatenate(frames,axis=1)
        out=bpy.data.images.new(name+" preview",sheet.shape[1],sheet.shape[0],alpha=False)
        out.pixels.foreach_set(sheet.reshape(-1))
        out.filepath_raw=str(dist/(name+"-preview.jpg"))
        out.file_format="JPEG"; out.save()
    print(name,len(parts),"parts,",sum(len(p["triangles"])//3 for p in parts),"triangles")
