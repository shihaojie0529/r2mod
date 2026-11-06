"""Create the game mesh, editable Blender scene, GLB, and two renders from one mesh definition.

blender -b --factory-startup --python-exit-code 1 --python art/make_model.py
"""
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector

here = Path(__file__).resolve().parent
root = here.parent
sys.path.insert(0, str(here))
from build_body import build_body
from build_head import build_head

gen = here / "generated"
gen.mkdir(exist_ok=True)
dist = root / "dist"
dist.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 20
scene.cycles.use_denoising = True
scene.render.threads_mode = "FIXED"
scene.render.threads = 6
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"
scene.view_settings.exposure = -0.2
scene.render.resolution_percentage = 100
scene.world = bpy.data.worlds.new("Studio")
scene.world.use_nodes = True
scene.world.node_tree.nodes.get("Background").inputs[0].default_value = (0.30, 0.36, 0.43, 1)
scene.world.node_tree.nodes.get("Background").inputs[1].default_value = 0.45

# Source coordinates: meters, X right, Y up, Z forward. The conversion reflects X
# so the body in Blender faces -Y and its anatomical right faces the viewer's left.
def xyz(v):
    return Vector((-v[0], -v[2], v[1]))

def srgb(s):
    return s / 12.92 if s < 0.04045 else ((s + 0.055) / 1.055) ** 2.4

materials = {}
def material(rgb, hair=False):
    rgb = tuple(round(float(c), 4) for c in rgb[:3])
    key = (rgb, hair)
    if key not in materials:
        mat = bpy.data.materials.new("Swatch / " + "".join(f"{round(c*255):02x}" for c in rgb))
        mat.diffuse_color = tuple(srgb(c) for c in rgb) + (1,)
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = mat.diffuse_color
        shader.inputs["Roughness"].default_value = 0.72
        if max(rgb) < 0.30:
            shader.inputs["Specular IOR Level"].default_value = 0.14
            shader.inputs["Roughness"].default_value = 0.92
        if hair:
            shader.inputs["Specular IOR Level"].default_value = 0.045
            shader.inputs["Roughness"].default_value = 0.95
        if rgb[0] > 0.65 and rgb[1] > 0.43 and rgb[2] < rgb[1] * 0.65:
            shader.inputs["Metallic"].default_value = 0.45
            shader.inputs["Roughness"].default_value = 0.33
        materials[key] = mat
    return materials[key]

pivots = {"Hips": (0,.92,0), "Chest": (0,1.25,0), "Head": (0,1.52,0)}
parents = {"Chest":"Hips", "Head":"Chest"}
for side, sign in (("Left",-1),("Right",1)):
    for name, (x,y,z), parent in (
            ("UpperArm",(.235,1.435,0),"Chest"), ("LowerArm",(.455,1.235,0),side+"UpperArm"),
            ("Hand",(.64,1.075,0),side+"LowerArm"), ("UpperLeg",(.112,.92,0),"Hips"),
            ("LowerLeg",(.115,.50,.012),side+"UpperLeg"), ("Foot",(.115,.105,0),side+"LowerLeg")):
        pivots[side+name] = (x*sign,y,z)
        parents[side+name] = parent

armature = bpy.data.armatures.new("Athlete / skeleton")
rig = bpy.data.objects.new("Training rig", armature)
scene.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode="EDIT")
for name, origin in pivots.items():
    bone = armature.edit_bones.new(name)
    bone.head = xyz(origin)
    tail = next((pivots[child] for child, par in parents.items() if par == name), None)
    if name == "Chest": tail = pivots["Head"]
    if tail is None:
        x,y,z = origin
        tail = (x,y+.28,z) if name=="Head" else ((x,y,z+.12) if "Foot" in name else (x+(.075 if x>0 else -.075),y-.06,z))
    bone.tail = xyz(tail)
    if name in parents:
        bone.parent = armature.edit_bones[parents[name]]
bpy.ops.object.mode_set(mode="OBJECT")
rig.show_in_front = False
armature.display_type = "STICK"
model_objects, parts = [], []

def add(name, verts, faces, color, bone, shade=None):
    if bone not in pivots:
        raise KeyError(f"{name}: unknown bone {bone}")
    mesh = bpy.data.meshes.new(name)
    # Reflected basis: reverse vertex order for Blender.
    mesh.from_pydata([xyz(v) for v in verts], [], [list(reversed(f)) for f in faces])
    mesh.validate(verbose=False)
    if shade is not None:
        end_color, density = shade
        if len(density) != len(mesh.vertices) or not all(0 <= d <= 1 for d in density):
            raise ValueError(f"{name}: expected one density between zero and one per vertex")
        attribute = mesh.attributes.new("clipper_density", "FLOAT", "POINT")
        attribute.data.foreach_set("value", density)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    # Keep authored normals on open facial patches; make closed volumes point outward.
    if bm.edges and all(e.is_manifold for e in bm.edges):
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    # Soft shading over the large skin planes; the silhouette, seams and cut
    # hair keep the faceted shape of the sculpt. Game vertex normals are smooth too.
    is_hair = name.startswith("Hair |")
    if (is_hair or (.55 < color[0] < .81 and .37 < color[1] < .64 and .28 < color[2] < .53)
            or "training tank" in name or "tapered" in name or "yoke" in name):
        for face in mesh.polygons:
            face.use_smooth = True
    mat = material(color, hair=is_hair)
    gradient = {}
    if shade is not None:
        # Store continuous scalp coverage in the game mesh. The Blender render
        # and GLB use the same two sRGB endpoints blended in linear light.
        density = [v.value for v in mesh.attributes["clipper_density"].data]
        gradient = dict(shade=[round(d,5) for d in density], colorEnd=list(end_color))
        attr = mesh.color_attributes.new("Scalp", "FLOAT_COLOR", "POINT")
        light = tuple(srgb(c) for c in color)
        dark = tuple(srgb(c) for c in end_color)
        for v, d in zip(attr.data, density):
            v.color = tuple(a + d*(b-a) for a,b in zip(light,dark)) + (1,)
        mesh.color_attributes.active_color = attr
        mat = mat.copy()
        mat.name = "Clipper fade"
        node = mat.node_tree.nodes.new("ShaderNodeVertexColor")
        node.layer_name = "Scalp"
        mat.node_tree.links.new(node.outputs["Color"],
                                mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"])
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    obj.parent = rig
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("Skin", "ARMATURE")
    mod.object = rig
    model_objects.append(obj)
    positions = []
    for vert in mesh.vertices:
        x,y,z = vert.co
        positions.extend([round(-x,6),round(z,6),round(-y,6)])
    parts.append(dict(name=name, bone=bone, pivot=pivots[bone], color=list(color[:3]), **gradient,
                      vertices=positions, triangles=[i for f in mesh.polygons for i in reversed(f.vertices[:])]))

build_body(add)
build_head(add)
assert set(p["bone"] for p in parts) == set(pivots)
assert all(len(p["triangles"]) >= 3 and len(p["vertices"]) >= 9 for p in parts)
(gen / "model.json").write_text(json.dumps({"parts":parts},separators=(",",":")),encoding="utf-8")
for obj in model_objects + [rig]:
    obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(dist / "TheRegular.glb"), export_format="GLB",
                          use_selection=True, export_animations=False)

def aim(obj, target):
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat("-Z","Y").to_euler()
def area(name, pos, power, color, size, target):
    light=bpy.data.lights.new(name,"AREA")
    light.energy,light.color,light.shape,light.size=power,color,"DISK",size
    ob=bpy.data.objects.new(name,light); scene.collection.objects.link(ob)
    ob.location=pos; aim(ob,target)

bpy.ops.mesh.primitive_plane_add(size=200, location=(0,0,.0075))
plane=bpy.context.object
plane.name="Studio / floor"
plane.data.materials.append(material((.13,.17,.19)))
area("Softbox",(-3,-4.5,5),440,(1,.86,.72),4,(0,0,1.12))
area("Cool fill",(3,-2,3),265,(.71,.84,1),3,(0,0,1.18))
area("Rim",(1.3,2.4,3.4),530,(.47,.82,1),2.1,(0,0,1.28))
cam=bpy.data.objects.new("Camera",bpy.data.cameras.new("Camera"))
scene.collection.objects.link(cam)
scene.camera=cam
cam.data.type="ORTHO"
def frame(pos, target, scale, width, height, output):
    cam.location=pos; aim(cam,target); cam.data.ortho_scale=scale
    scene.render.resolution_x,scene.render.resolution_y=width,height
    scene.render.filepath=str(output)
    bpy.ops.render.render(write_still=True)

# Editable rig and a clean standing pose. GLB excludes camera, floor and lights.
for obj in bpy.context.selected_objects:
    obj.select_set(False)
bpy.context.view_layer.objects.active=rig
cam.location=(1,-5,2.0); aim(cam,(0,0,.96)); cam.data.ortho_scale=2.02
bpy.ops.wm.save_as_mainfile(filepath=str(dist / "TheRegular.blend"))
# A relaxed stance for the two model previews. The editable files retain
# their neutral rest pose and ROM for retargeting.
for side,angle in (("Right",-24),("Left",24)):
    arm = rig.pose.bones[side + "UpperArm"]
    m = arm.matrix.copy()
    arm.matrix = (Matrix.Translation(m.translation) @ Matrix.Rotation(math.radians(angle),4,'Y')
                  @ Matrix.Translation(-m.translation) @ m)
    bpy.context.view_layer.update()
frame((1.7,-5.6,2.10),(0,0,.95),2.04,620,840,gen/"full.png")
frame((-.90,-4.3,1.98),(0,0,1.445),.85,670,840,gen/"bust.png")
frame((-.85,-4.4,1.97),(0,0,1.585),.54,256,256,gen/"cui-portrait.png")
print("Built",len(parts),"parts with",sum(len(p["triangles"])//3 for p in parts),"triangles")
