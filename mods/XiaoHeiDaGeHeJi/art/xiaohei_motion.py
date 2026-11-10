"""Training-room poses for the skinned Xiaohei GLB. Coordinates are Blender metres."""
from math import pi, radians, sin, tau

from mathutils import Euler, Vector


# Each key controls the gun raise, knee bend (metres), lateral weight shift
# (metres), torso turn (degrees), free arm extension and glance (degrees).
GUARD = (0.02, .014, -.008, -12, .08, 10)
AIM = (1, .044, -.018, 9, .22, 0)
BRACE = (.92, .115, -.058, -4, .40, 2)
SLIDE = (1, .163, .085, 17, 1, -6)
COIL = (.49, .124, -.077, -17, .60, -9)
RETURN = (.70, .103, .064, 16, .73, 8)
WATCH = (.36, .028, .038, -7, .04, -34)


TRACKS = {
    "ready": [(0, GUARD), (5, (.08, .030, -.023, -15, .15, 7)),
              (15, (1.06, .060, -.027, 12, .30, -2)), (21, AIM)],
    "pistol": [(0, AIM), (16, (1, .053, -.024, 10, .27, 0)), (23, AIM)],
    "seeker": [(0, AIM), (4, BRACE), (11, SLIDE), (15, SLIDE), (24, AIM)],
    "blink": [(0, AIM), (6, COIL), (7, RETURN), (13, AIM),
              (20, RETURN), (21, COIL), (29, AIM)],
    "replay": [(0, AIM), (6, WATCH), (13, WATCH),
               (22, (1, .072, -.040, 14, .28, -2)), (39, AIM)],
    "settle": [(0, AIM), (7, (.66, .040, -.014, 2, .14, 5)),
               (14, (.01, .011, -.003, -10, .05, 13)), (17, GUARD)],
}


def angles(x=0, y=0, z=0):
    return Euler(tuple(map(radians, (x, y, z))), "XYZ").to_quaternion()


def sample(keys, frame):
    for (a, first), (b, last) in zip(keys, keys[1:]):
        if frame <= b:
            t = max(0, (frame - a) / (b - a))
            t = t * t * (3 - 2 * t)
            return tuple(start + (end - start) * t for start, end in zip(first, last))
    return keys[-1][1]


def replay_step_x(i):
    """World X for the two-step sidestep from the slide mark back to center."""
    t = max(0, min(1, (i - 11) / 20))
    return .32 * (1 - t*t*(3 - 2*t))


def replay_foot_offset(side, i):
    # Step with the near shoe, then collect the other. Cancel root travel
    # on the planted foot while its partner describes a short swing arc.
    start = 11 if side == "Right" else 21
    t = max(0, min(1, (i - start) / 10))
    x = .32 * (1 - t*t*(3 - 2*t)) - replay_step_x(i)
    return Vector((x, 0, .065 * sin(pi*t)**2))


def bend_legs(rig, rotate, hips, offset, replay_frame):
    """Two-bone solve keeps each shoe level as the body crouches and steps."""
    bones = rig.data.bones
    for side, sign in (("Left", 1), ("Right", -1)):
        thigh, shin, foot = (side + suffix for suffix in ("UpperLeg", "LowerLeg", "Foot"))
        hip, knee, ankle = (bones[name].head_local for name in (thigh, shin, foot))
        root = bones["Hips"].head_local
        hip_now = root + offset + hips @ (hip - root)
        target = ankle if replay_frame is None else ankle + replay_foot_offset(side, replay_frame)
        towards = (target - hip_now).normalized()
        a, b = (knee - hip).length, (ankle - knee).length
        reach = min((target - hip_now).length, a + b - .000001)
        along = (a*a - b*b + reach*reach) / (2 * reach)
        # Knees track the slightly splayed toes, flexing toward the camera.
        pole = Vector((.13 * sign, -1, 0))
        knee_dir = (pole - towards * pole.dot(towards)).normalized()
        knee_now = hip_now + towards * along + knee_dir * max(0, a*a - along*along)**.5
        upper = (knee - hip).rotation_difference(knee_now - hip_now)
        lower = (ankle - knee).rotation_difference(target - knee_now)
        rotate(thigh, hips.inverted() @ upper)
        rotate(shin, upper.inverted() @ lower)
        rotate(foot, lower.inverted())


def apply_pose(rig, shot, i, count):
    """Set a complete pose. The caller owns the rig object's position and rotation."""
    keys = TRACKS[shot]
    frame = i * keys[-1][0] / max(1, count - 1)
    raise_gun, crouch, shift, turn, spread, glance = sample(keys, frame)
    # A slight rise and fall within each shot also gives quiet holds some life.
    breath = sin(tau * i / max(1, count - 1))
    shots = {"pistol": (4, 10, 16), "seeker": (5,)}.get(shot, ())
    kick = 0
    for fire in shots:
        if 0 <= frame - fire < 5:
            kick = sample([(0, (.10,)), (1, (1,)), (2, (.62,)), (3, (.27,)),
                           (5, (0,))], frame - fire)[0]
    if shot == "seeker":
        kick *= 1.35

    def rotate(name, delta):
        bone = rig.pose.bones[name]
        # glTF reorients individual bone axes on import. Express each delta in
        # its actual bind basis so the same world-space gestures survive import.
        bind = bone.bone.matrix_local.to_quaternion()
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = bind.inverted() @ delta @ bind
        return delta

    offset = Vector((shift, crouch * .13 + kick * .012, -crouch + breath * .0025))
    hips = rotate("Hips", angles(crouch * 40, shift * 34, turn * .30))
    rig.pose.bones["Hips"].location = rig.data.bones["Hips"].matrix_local.to_quaternion().inverted() @ offset
    bend_legs(rig, rotate, hips, offset, frame if shot == "replay" else None)
    chest = rotate("Chest", angles(1 + crouch * 47 - kick * 4 + breath * .45, shift * 63, turn * .70))
    torso = hips @ chest
    # Look along the sights first, then let the eyes follow the spectral partner.
    rotate("Head", torso.inverted() @ angles(2 + crouch * 10, -.9 + shift * 13, turn * .24 + glance))

    upper = rotate("RightUpperArm", angles(24 - 39 * raise_gun + kick * 3,
                                          -5 + 5 * raise_gun, -6 + 7 * raise_gun))
    fore = rotate("RightLowerArm", angles(7 - 10 * raise_gun - kick * 10, 2, -2))
    # The wrist keeps the barrel on target despite the chest roll during the slide.
    barrel = angles(34 * (1 - raise_gun) - kick * 13, -6 + 5 * raise_gun,
                    1 + 7 * raise_gun)
    rotate("RightHand", (torso @ upper @ fore).inverted() @ barrel)

    # The off hand curls in near the ribs when sighting and flares outward for
    # balance in the side-slide. The original sculpt already has bent fingers.
    rotate("LeftUpperArm", angles(-7 - 15 * raise_gun + 30 * spread,
                                 5 - 53 * spread, -9 + turn * .18))
    rotate("LeftLowerArm", angles(-11 - 39 * raise_gun + 31 * spread, 8, 10))
    rotate("LeftHand", angles(7 + 8 * spread, -10 + 11 * spread, -8))


def muzzle_ray(rig):
    """Return world-space origin and unit direction after a view-layer update."""
    hand = rig.pose.bones["RightHand"]
    matrix = rig.matrix_world @ hand.matrix @ hand.bone.matrix_local.inverted()
    return (matrix @ Vector((-.246, -.609, 1.381)),
            (matrix.to_3x3() @ Vector((0, -1, 0))).normalized())
