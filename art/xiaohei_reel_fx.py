"""Blender move-reel effects, in metres. Frame indices are local to each shot."""
from math import cos, radians, sin

import bpy
from mathutils import Vector

# Three primary hits earn a Highlight. The replay spends it for two extra rounds.
REPLAY_FRAMES = tuple(7 + round(27 * n / 7) for n in range(8))


def light_material(name, color, strength):
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    emission = nodes.new("ShaderNodeEmission")
    emission.inputs["Color"].default_value = (*color, 1)
    emission.inputs["Strength"].default_value = strength
    output = nodes.new("ShaderNodeOutputMaterial")
    material.node_tree.links.new(emission.outputs[0], output.inputs["Surface"])
    return material


def stroke(name, material, width, radii):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = width / 2
    curve.bevel_resolution = 2
    curve.use_fill_caps = True
    path = curve.splines.new("POLY")
    path.points.add(len(radii) - 1)
    for point, radius in zip(path.points, radii):
        point.radius = radius
    obj = bpy.data.objects.new(name, curve)
    bpy.context.scene.collection.objects.link(obj)
    curve.materials.append(material)
    return obj


def draw(obj, coordinates):
    for point, position in zip(obj.data.splines[0].points, coordinates):
        point.co = (*position, 1)
    obj.hide_render = False


class Effects:
    def __init__(self):
        core = light_material("Reel / light cell", (.49, 1, .90), 2.8)
        teal = light_material("Reel / ion trail", (.015, .59, .62), 1.4)
        silver = light_material("Reel / replay streak", (.36, .60, .70), 1.1)
        amber = light_material("Reel / seeker collar", (1, .38, .035), 2.0)
        self.flash = stroke("Barrel flare", core, .042, (.45, 1, .03))
        self.tracer = stroke("Pistol streak", core, .023, (.06, .85, 1, .04))
        self.echo_tracer = stroke("Replay streak", silver, .018, (.08, .8, 1, .05))
        self.tail = stroke("Seeker wake", teal, .031, (.02, .22, .43, .65, .90, 1))
        self.round = stroke("Seeker luminous nose", core, .065, (.65, 1, .03))
        self.band = stroke("Seeker amber band", amber, .077, (.94, 1, .94))
        self.rim = stroke("Seeker mark", amber, .017, [1] * 33)
        self.pointer = stroke("Seeker mark index", core, .013, (.7, 1, .7))
        self.objects = (self.flash, self.tracer, self.echo_tracer, self.tail,
                        self.round, self.band, self.rim, self.pointer)
        self.seeker_origin = None

    def barrel_flash(self, muzzle, direction):
        draw(self.flash, [muzzle + direction * distance for distance in (0, .026, .12)])

    def fire(self, age, muzzle, direction, target, replay):
        if age == 0 and not replay:
            self.barrel_flash(muzzle, direction)
        tracer = self.echo_tracer if replay else self.tracer
        # Three readable positions in a burst, with a tapered tail and a sharp tip.
        limits = ((.03, .36), (.27, .77), (.70, 1.0))
        start, end = limits[age]
        draw(tracer, [muzzle.lerp(target, start + (end - start) * t)
                      for t in (0, .50, .92, 1)])

    def seeker(self, i, muzzle, direction, target):
        if i == 5 or self.seeker_origin is None:
            self.seeker_origin = muzzle.copy()
        if i < 5:
            return
        if i == 5:
            self.barrel_flash(muzzle, direction)
        if i < 13:
            start = self.seeker_origin
            arc = Vector((0, 0, .22))

            def flight(t):
                return start.lerp(target, t) + arc * (4 * t * (1 - t))

            t = (i - 5) / 8
            tip = flight(t)
            aim = ((target - start) + arc * (4 - 8 * t)).normalized()
            draw(self.round, [tip + aim * d for d in (-.031, .018, .087)])
            draw(self.band, [tip + aim * d for d in (-.033, -.017, -.005)])
            if t > 0:
                draw(self.tail, [flight(max(0, t - .32 * (1 - j / 5))) - aim * .032
                                 for j in range(6)])

    def mark(self, age, target):
        # Match the game's open amber collar and the pale downward index.
        rise = 1 - (1 - min(1, age / 4)) ** 2
        center = target + Vector((0, 0, .30 * rise))
        radius = .075 * (.35 + .65 * rise)
        view = bpy.context.scene.camera.matrix_world.to_quaternion()
        right = view @ Vector((1, 0, 0))
        up = view @ Vector((0, 1, 0))
        # Bring the first contact slightly forward of the solid target face.
        center += view @ Vector((0, 0, .025))
        angles = [radians(-70 + 320 * j / 32) for j in range(33)]
        draw(self.rim, [center + radius * (cos(a) * right + sin(a) * up) for a in angles])
        draw(self.pointer, [center + radius * (x * right + y * up)
                            for x, y in ((-.31, -.93), (0, -1.32), (.31, -.93))])

    def update(self, shot, i, muzzle, direction, echo_muzzle, echo_direction, target):
        for obj in self.objects:
            obj.hide_render = True
        if shot == "seeker":
            self.seeker(i, muzzle, direction, target)
        else:
            self.seeker_origin = None
        if shot == "pistol" and 4 <= i <= 18 and (i - 4) % 6 < 3:
            self.fire((i - 4) % 6, muzzle, direction, target, replay=False)
        if shot == "replay":
            for fired in REPLAY_FRAMES:
                if 0 <= i - fired < 3:
                    self.fire(i - fired, echo_muzzle, echo_direction, target, replay=True)
                    break
        # At 18 fps the five-second mark spans the blink and replay.
        age_at_cut = {"seeker": -13, "blink": 12, "replay": 42, "settle": 82}
        age = age_at_cut.get(shot, -1000) + i
        if 0 <= age < 90:
            self.mark(age, target)
