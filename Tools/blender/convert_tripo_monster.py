"""Turns the Tripo-made monster (rigged GLB with its "run" animation) into the FBX the game uses.

Run headless (no Blender UI needed):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --factory-startup \
      --python Tools/blender/convert_tripo_monster.py -- [--src GLB] [--preview DIR]

Input (default)
  Art/Source/Tripo/Monster100_run.glb    exported from Tripo Studio with the "run" animation
Outputs
  Assets/Game/Resources/Monster100/Monster100.fbx            model + "idle" and "run" clips
  Assets/Game/Resources/Monster100/Monster100_basecolor.jpg  textures, next to the FBX so Unity's
  Assets/Game/Resources/Monster100/Monster100_normal.jpg     URP import puts them on the material
  DIR/monster100_*.png                                       preview renders (optional)

What it changes
  - Tripo exports it facing -X; it is turned to face +Y here, which comes out as +Z (forward) in
    Unity with this exporter's axis settings (-Y came out backwards).
  - Tripo's "run" moves forward ~1.9 m per loop. The game moves the monster itself, so the hips'
    forward drift is taken out and the run happens on the spot.
  - Tripo's rest pose is an A-pose, so an "idle" is added: arms hanging, a slight hunch, slow
    breathing. The game mixes idle and run by speed (half and half reads as a stalking walk).
The size is left as exported (about 1 m); MonsterBody scales it to the game's height.
Prints the run's ground speed (model units per second), which MonsterBody uses to match the
playback rate to how fast the monster really moves.
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(REPO, "Art", "Source", "Tripo", "Monster100_run.glb")
NAME = "Monster100"
OUT_DIR = os.path.join(REPO, "Assets", "Game", "Resources", NAME)
BONE = "mixamorig:"

UP = Vector((0, 0, 1))
FORWARD = Vector((0, 1, 0))  # becomes Unity's +Z
PITCH = UP.cross(FORWARD)  # rotating about this tips the top of a bone forward


def fcurves_of(action):
    """All F-curves of an action (Blender 5 keeps them in layers / strips / channel bags)."""
    try:
        return list(action.fcurves)
    except AttributeError:
        return [fc for layer in action.layers for strip in layer.strips
                for bag in strip.channelbags for fc in bag.fcurves]


def pose_bone(arm, name):
    return arm.pose.bones[BONE + name]


def world_head(arm, name):
    return arm.matrix_world @ pose_bone(arm, name).head


def load(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = next(o for o in bpy.data.objects if o.type == "MESH" and o.find_armature() == arm)
    # The importer adds a bone-display sphere; only the skinned body is the model.
    for o in [o for o in bpy.data.objects if o not in (arm, body)]:
        bpy.data.objects.remove(o)
    arm.name, body.name = "Armature", "Body"
    for m in body.data.materials:
        m.name = NAME
    return arm, body


def make_run_in_place(arm):
    """Removes the hips' forward travel from "run". Returns (direction it ran, ground speed)."""
    scene = bpy.context.scene
    run = arm.animation_data.action
    run.name = "run"
    f0, f1 = (int(round(f)) for f in run.frame_range)
    hips = []
    for f in range(f0, f1 + 1):
        scene.frame_set(f)
        hips.append(world_head(arm, "Hips"))
    drift = hips[-1] - hips[0]
    drift.z = 0.0
    speed = drift.length / ((f1 - f0) / scene.render.fps)
    # Tripo's loop also starts a third of a metre ahead of the rest pose: centre the hips over the
    # feet's rest position (the CharacterController) on average.
    rest_hips = arm.matrix_world @ arm.data.bones[BONE + "Hips"].head_local
    centre = sum((h - drift * (i / (f1 - f0)) for i, h in enumerate(hips)), Vector()) / len(hips)
    offset = centre - rest_hips
    offset.z = 0.0

    # Pose location is in the bone's rest frame: armature delta = rest3x3 @ location delta.
    to_local = arm.data.bones[BONE + "Hips"].matrix_local.to_3x3().inverted()
    path = f'pose.bones["{BONE}Hips"].location'
    for fc in fcurves_of(run):
        if fc.data_path != path:
            continue
        for kp in fc.keyframe_points:
            t = (kp.co.x - f0) / (f1 - f0)
            d = (to_local @ (-drift * t - offset))[fc.array_index]
            kp.co.y += d
            kp.handle_left.y += d
            kp.handle_right.y += d
        fc.update()

    scene.frame_set(f1)
    left = world_head(arm, "Hips")
    scene.frame_set(f0)
    left -= world_head(arm, "Hips")
    left.z = 0.0
    print(f"[tripo] run: frames {f0}-{f1}, travelled {drift.length:.3f} -> {left.length:.4f} after the fix, "
          f"hips moved back {offset.length:.3f} over the feet")
    return drift.normalized(), speed


def face_forward(arm, body, ran_towards):
    """Turns the model so the way it runs is FORWARD, and bakes the turn into the data."""
    turn = math.atan2(FORWARD.y, FORWARD.x) - math.atan2(ran_towards.y, ran_towards.x)
    # Through the matrix: glTF imports use quaternion rotation, so rotation_euler would be ignored.
    arm.matrix_world = Matrix.Rotation(turn, 4, "Z") @ arm.matrix_world
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    print(f"[tripo] turned {math.degrees(turn):.1f} deg to face {tuple(FORWARD)}; toes now point {toe_direction(arm)}")


def toe_direction(arm):
    """Where the left foot points in the rest pose (the way the model faces)."""
    arm.data.pose_position = "REST"
    bpy.context.view_layer.update()
    d = world_head(arm, "LeftToeBase") - world_head(arm, "LeftFoot")
    arm.data.pose_position = "POSE"
    bpy.context.view_layer.update()
    d.z = 0.0
    return tuple(round(c, 2) for c in d.normalized())


def rotate_about_head(pb, axis, angle):
    """Rotates a pose bone about its own head, axis in armature space."""
    m = pb.matrix.copy()
    head = m.translation.copy()
    pb.matrix = Matrix.Translation(head) @ Matrix.Rotation(angle, 4, axis) @ Matrix.Translation(-head) @ m
    bpy.context.view_layer.update()


def aim(pb, direction):
    """Turns a pose bone (with its children) so it points along direction."""
    current = (pb.tail - pb.head).normalized()
    q = current.rotation_difference(direction.normalized())
    angle, axis = q.angle, q.axis
    if angle > 1e-5:
        rotate_about_head(pb, axis, angle)


def pose_idle_base(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)
    bpy.context.view_layer.update()
    for name, deg in (("Spine", 4), ("Spine1", 4), ("Spine2", 4), ("Neck", 6), ("Head", 4)):
        rotate_about_head(pose_bone(arm, name), PITCH, math.radians(deg))
    centre = world_head(arm, "Spine2")
    for side in ("Left", "Right"):
        upper = pose_bone(arm, side + "Arm")
        out = upper.head - centre
        out.z = 0.0
        out = out.normalized()
        aim(upper, -UP + out * 0.2 + FORWARD * 0.1)
        aim(pose_bone(arm, side + "ForeArm"), -UP + out * 0.06 + FORWARD * 0.35)


def make_idle(arm):
    """Arms hanging, a slight hunch, slow breathing; loops over 64 frames."""
    idle = bpy.data.actions.new("idle")
    arm.animation_data.action = idle
    for f, s in ((0, 0.0), (16, 1.0), (32, 0.0), (48, -1.0), (64, 0.0)):
        pose_idle_base(arm)
        rotate_about_head(pose_bone(arm, "Spine2"), PITCH, math.radians(-1.6 * s))
        rotate_about_head(pose_bone(arm, "Neck"), PITCH, math.radians(1.2 * s))
        for pb in arm.pose.bones:
            pb.keyframe_insert("location", frame=f, group=pb.name)
            pb.keyframe_insert("rotation_quaternion", frame=f, group=pb.name)
    return idle


def write_textures(out_dir):
    """Writes the GLB's packed JPEGs next to the FBX unchanged (the FBX refers to them by name)."""
    for img in bpy.data.images:
        if img.packed_file is None:
            continue
        kind = "normal" if "normal" in img.name.lower() else "basecolor"
        # The FBX exporter names a packed image's file after the image.
        img.name = f"{NAME}_{kind}"
        path = os.path.join(out_dir, img.name + ".jpg")
        with open(path, "wb") as f:
            f.write(img.packed_file.data)
        print("[tripo] wrote", path)


def export_fbx(path):
    bpy.ops.export_scene.fbx(
        filepath=path,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        add_leaf_bones=False,
        use_armature_deform_only=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",
        embed_textures=False,
    )
    print("[tripo] exported", path)


def check_export(path):
    """Re-imports the FBX and reports what Unity will get."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    body = next(o for o in bpy.data.objects if o.type == "MESH")
    pts = [body.matrix_world @ Vector(c) for c in body.bound_box]
    print(f"[tripo] check: height {max(p.z for p in pts) - min(p.z for p in pts):.3f}, "
          f"actions {[a.name for a in bpy.data.actions]}, UV layers {[uv.name for uv in body.data.uv_layers]}, "
          f"materials {[m.name for m in body.data.materials]}")
    print(f"[tripo] check: toes point {toe_direction(arm)} (want ~{tuple(FORWARD)})")


def render_previews(arm, out_dir, run, idle):
    scene = bpy.context.scene
    engines = [i.identifier for i in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    for e in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "BLENDER_WORKBENCH"):
        if e in engines:
            try:
                scene.render.engine = e
                break
            except TypeError:
                continue
    scene.render.resolution_x, scene.render.resolution_y = 512, 768
    world = bpy.data.worlds.new("Preview")
    world.color = (0.05, 0.05, 0.06)
    scene.world = world
    sun = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    target = Vector((0, 0, 0.5))
    side = FORWARD.cross(UP)
    front = FORWARD * 2.6 + UP * 0.6
    three_quarter = FORWARD * 1.9 + side * 1.7 + UP * 0.75
    for label, action, frame, eye in (
        ("idle_front", idle, 0, front),
        ("idle_three_quarter", idle, 16, three_quarter),
        ("run_side", run, 8, side * 2.6 + UP * 0.6),
        ("run_three_quarter", run, 20, three_quarter),
    ):
        arm.animation_data.action = action
        scene.frame_set(frame)
        cam.location = eye
        cam.rotation_euler = (target - eye).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = 50
        scene.render.filepath = os.path.join(out_dir, f"monster100_{label}.png")
        bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    src = argv[argv.index("--src") + 1] if "--src" in argv else SRC
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    if preview:
        preview = os.path.abspath(preview)

    arm, body = load(src)
    print(f"[tripo] as exported from Tripo, toes point {toe_direction(arm)}")
    ran_towards, speed = make_run_in_place(arm)
    run = arm.animation_data.action
    face_forward(arm, body, ran_towards)
    idle = make_idle(arm)
    arm.animation_data.action = run

    os.makedirs(OUT_DIR, exist_ok=True)
    write_textures(OUT_DIR)
    out = os.path.join(OUT_DIR, NAME + ".fbx")
    export_fbx(out)
    print(f"[tripo] run ground speed: {speed:.3f} model units/s (put this in MonsterBody.RunGroundSpeed)")
    if preview:
        render_previews(arm, preview, run, idle)
        print("[tripo] previews in", preview)
    check_export(out)


main()
