"""Render Xiaohei on Cui Ge's studio set and encode using the shared GIF layout.

blender -b dist/TheRegular.blend --python-exit-code 1 --python art/render_xiaohei_moves.py -- /tmp/xiaohei-reel
python3 art/encode_moves.py /tmp/xiaohei-reel --gif-only
"""
import argparse
import json
import sys
from math import atan2, sqrt
from pathlib import Path

import bpy
from mathutils import Vector

ART = Path(__file__).resolve().parent
sys.path.insert(0, str(ART))
from xiaohei_motion import apply_pose, muzzle_ray, replay_step_x
from xiaohei_reel_fx import Effects

SHOTS = [
    ("ready", "举枪瞄准", 22),
    ("pistol", "我架枪了 · 三连射", 24),
    ("seeker", "这波马了 · 侧滑追踪", 25),
    ("blink", "卡了卡了 · 闪现回位", 30),
    ("replay", "放段以前的 · 分身连射", 40),
    ("settle", "收枪", 18),
]
TARGET = Vector((-.06, -2.10, 1.39))


def ease(t):
    t = max(0, min(1, t))
    return t*t*(3 - 2*t)


def gunner():
    bpy.ops.import_scene.gltf(filepath=str(ART.parent / "dist" / "Xiaohei.glb"))
    rig = next(obj for obj in bpy.context.selected_objects if obj.type == "ARMATURE")
    rig.rotation_mode = "XYZ"
    mesh, = (obj for obj in rig.children if obj.type == "MESH")
    return rig, mesh


def new_surface(name):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(shader.outputs[0], output.inputs["Surface"])
    return material, shader, output


def spectral_material(source, opacity):
    bsdf = next((node for node in source.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None)
    pigment = bsdf.inputs["Base Color"].default_value if bsdf else source.diffuse_color
    value = sqrt(.213 * pigment[0] + .715 * pigment[1] + .072 * pigment[2])
    # The GLB swatches are linear. Map their luminance into the game's
    # teal-to-mint echo palette, then linearize for the studio shader.
    rgb = (.025 + .74*value, .32 + .68*value, .40 + .51*value)
    tint = tuple(((c + .055) / 1.055)**2.4 for c in rgb) + (1,)
    material, shader, output = new_surface("Echo / " + source.name)
    nodes = material.node_tree.nodes
    shader.inputs["Base Color"].default_value = tint
    shader.inputs["Emission Color"].default_value = tint
    shader.inputs["Emission Strength"].default_value = .30
    # One value controls all swatches so the recorded silhouette fades together.
    mix = nodes.new("ShaderNodeMixShader")
    mix.inputs[0].default_value = opacity
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    links = material.node_tree.links
    links.new(transparent.outputs[0], mix.inputs[1])
    links.new(shader.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], output.inputs["Surface"])
    return material, mix.inputs[0]


def target_dummy(camera):
    # Two rings and a pin on a freestanding target. The brighter amber ring
    # above this target is reserved for Xiaohei's seeker mark.
    def material(name, color, metal):
        mat, bsdf, _ = new_surface(name)
        bsdf.inputs["Base Color"].default_value = (*color, 1)
        bsdf.inputs["Roughness"].default_value = .56
        bsdf.inputs["Metallic"].default_value = metal
        return mat

    plate = material("Target / graphite", (.029, .057, .069), .42)
    ring = material("Target / brushed metal", (.22, .34, .35), .45)
    inner = material("Target / enamel", (.057, .15, .16), .25)
    normal = (camera.location - TARGET).normalized()

    def cylinder(name, center, radius, depth, axis, mat):
        bpy.ops.mesh.primitive_cylinder_add(vertices=64, radius=radius, depth=depth, location=center)
        obj = bpy.context.object
        obj.name = name
        obj.rotation_euler = axis.to_track_quat("Z", "Y").to_euler()
        obj.data.materials.append(mat)
        bevel = obj.modifiers.new("Machined edge", "BEVEL")
        bevel.width = min(.007, depth * .24)
        bevel.segments = 2
        obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
        return obj

    upright = Vector((0, 0, 1))
    post = TARGET - normal*.07
    cylinder("Target / foot", (post.x, post.y, .025), .145, .034, upright, plate)
    cylinder("Target / rod", (post.x, post.y, TARGET.z/2), .018, TARGET.z, upright, plate)
    cylinder("Target / rim", TARGET - normal*.034, .164, .065, normal, ring)
    cylinder("Target / face", TARGET, .147, .012, normal, plate)
    cylinder("Target / inner ring", TARGET + normal*.009, .088, .008, normal, ring)
    cylinder("Target / inset", TARGET + normal*.015, .077, .008, normal, inner)
    cylinder("Target / pin", TARGET + normal*.020, .022, .008, normal, ring)


def place(shot, i, actor, echo, ghost_mesh, fades):
    slide = .32
    x = 0.
    if shot == "seeker":
        x = slide * ease((i - 4) / 10)
    elif shot == "blink":
        x = -.48 if 7 <= i < 21 else slide
    elif shot == "replay":
        x = replay_step_x(i)
    actor.location.x = x
    actor.rotation_euler.z = atan2(-x, 2.1)
    alpha = 0.
    if shot == "blink" and 7 <= i < 21:
        apply_pose(echo, "blink", 6, 30)
        echo.location.x = slide
        echo.rotation_euler.z = atan2(-slide, 2.1)
        alpha = .53
    elif shot == "replay" and 4 <= i <= 37:
        apply_pose(echo, "pistol", 0, 24)
        echo.location.x = slide - .99
        echo.rotation_euler.z = atan2(-echo.location.x, 2.1)
        alpha = .71 * ease((i-3)/4) * (1-ease((i-34)/4))
    ghost_mesh.hide_render = alpha == 0
    if alpha:
        for socket in fades:
            socket.default_value = alpha


def render():
    parser = argparse.ArgumentParser()
    parser.add_argument("directory", type=Path)
    parser.add_argument("--inspect", nargs="+", type=int, help="Render only these one-based frames")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    args.directory.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    old = bpy.data.objects["Training rig"]
    for obj in list(old.children_recursive) + [old]:
        bpy.data.objects.remove(obj, do_unlink=True)
    actor, _ = gunner()
    echo, ghost_mesh = gunner()
    ghost_mesh.visible_shadow = False
    fades = []
    for slot in ghost_mesh.material_slots:
        slot.material, opacity = spectral_material(slot.material, .53)
        fades.append(opacity)

    cam = scene.camera
    at = Vector((-.07, -.65, .96))
    cam.location = at + Vector((-3.25, -3.60, .94))
    cam.rotation_euler = (at - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 2.59
    target_dummy(cam)
    effects = Effects()
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 6
    scene.cycles.use_denoising = True
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 7
    scene.render.use_persistent_data = True
    scene.render.resolution_x, scene.render.resolution_y = 450, 424
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"

    shots = []
    frame = 1
    for shot, label, count in SHOTS:
        shots.append(dict(id=shot, label=label, first=frame, last=frame+count-1))
        for i in range(count):
            apply_pose(actor, shot, i, count)
            place(shot, i, actor, echo, ghost_mesh, fades)
            bpy.context.view_layer.update()
            effects.update(shot, i, *muzzle_ray(actor), *muzzle_ray(echo), TARGET)
            if args.inspect is None or frame in args.inspect:
                scene.render.filepath = str(args.directory / f"frame-{frame:04d}.png")
                bpy.ops.render.render(write_still=True)
            frame += 1
    sequence = dict(fps=18, shots=shots, stem="Xiaohei-Moves", footer="XIAOHEI   /   TRAINING ROOM")
    (args.directory / "sequence.json").write_text(json.dumps(sequence, ensure_ascii=False), encoding="utf-8")


if __name__ == "__main__":
    render()
