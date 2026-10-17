"""Builds the monster model in Blender and exports it for Unity.

Run headless (no Blender UI needed):
  "C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --factory-startup \
      --python Tools/blender/build_monster.py -- [--preview DIR]

Outputs
  Art/Source/Monster.blend                     editable source (parts, materials, live modifiers)
  Assets/Game/Resources/MonsterModel.kfmodel   what Unity imports (KaiModelImporter)
  DIR/monster_*.png                            preview renders (optional)

The model is four parts so the game's procedural animation keeps working. Each part's origin is
its joint, matching CharacterFactory.BuildMonster:
  Body  - robe, torso, neck        pivot: ground
  Head  - skull, mask, hair        pivot: top of the neck   (Unity 0, 2.5, 0.04)
  ArmL / ArmR - sleeve, arm, hand  pivot: shoulder          (Unity -/+0.24, 2.05, 0)
Material slot names map to MaterialLibrary entries in the importer.

Blender is Z-up and the monster faces -Y here. Unity is Y-up, left-handed, facing +Z, so points
are written as (-x, z, -y) and triangle winding is reversed.
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", ".."))
OUT_MODEL = os.path.join(REPO, "Assets", "Game", "Resources", "MonsterModel.kfmodel")
OUT_BLEND = os.path.join(REPO, "Art", "Source", "Monster.blend")

MATERIALS = {
    # name: (base colour, roughness, emission strength)
    "MonsterBody": ((0.015, 0.015, 0.018, 1), 0.85, 0.0),
    "Hair": ((0.008, 0.008, 0.01, 1), 0.45, 0.0),
    "MonsterFace": ((0.86, 0.85, 0.8, 1), 0.5, 0.15),
    "MonsterSkin": ((0.62, 0.6, 0.58, 1), 0.6, 0.05),
    "MonsterEye": ((1.0, 0.9, 0.7, 1), 0.3, 3.0),
}

random.seed(20261017)


def to_blender(ux, uy, uz):
    """Unity coordinates -> Blender coordinates (inverse of the export mapping)."""
    return Vector((-ux, -uz, uy))


# ---------------------------------------------------------------------------------- materials

def material(name):
    m = bpy.data.materials.get(name)
    if m is not None:
        return m
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    col, rough, emit = MATERIALS[name]
    m.diffuse_color = col
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = col
        bsdf.inputs["Roughness"].default_value = rough
        if emit > 0:
            for key in ("Emission Color", "Emission"):
                if key in bsdf.inputs:
                    bsdf.inputs[key].default_value = col
                    break
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emit
    return m


class Part:
    """One bmesh with named material slots; geometry is built in the part's local space."""

    def __init__(self, name, pivot_unity):
        self.name = name
        self.pivot = to_blender(*pivot_unity)
        self.bm = bmesh.new()
        self.slots = []

    def slot(self, mat_name):
        if mat_name not in self.slots:
            self.slots.append(mat_name)
        return self.slots.index(mat_name)

    def tube(self, rings, sides, mat, cap_bottom=False, cap_top=False):
        """rings: (z, cx, cy, rx, ry, jitter) bottom to top."""
        idx = self.slot(mat)
        vrings = []
        for z, cx, cy, rx, ry, jit in rings:
            ring = []
            for s in range(sides):
                a = 2 * math.pi * s / sides
                j = 1 + (random.uniform(-jit, jit) if jit else 0)
                ring.append(self.bm.verts.new((cx + math.cos(a) * rx * j, cy + math.sin(a) * ry * j, z)))
            vrings.append(ring)
        faces = []
        for r in range(len(vrings) - 1):
            a, b = vrings[r], vrings[r + 1]
            for s in range(sides):
                faces.append(self.bm.faces.new((a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s])))
        if cap_bottom:
            faces.append(self.bm.faces.new(list(reversed(vrings[0]))))
        if cap_top:
            faces.append(self.bm.faces.new(vrings[-1]))
        for f in faces:
            f.material_index = idx
            f.smooth = True
        return faces

    def blob(self, center, radii, mat, rot=None, segs=(16, 10), jitter=0.0):
        idx = self.slot(mat)
        m = Matrix.Translation(Vector(center)) @ (rot or Matrix.Identity(4)) @ Matrix.Diagonal((*radii, 1))
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=segs[0], v_segments=segs[1], radius=1.0, matrix=m)
        faces = {f for v in res["verts"] for f in v.link_faces}
        if jitter:
            c = Vector(center)
            for v in res["verts"]:
                v.co = c + (v.co - c) * (1 + random.uniform(-jitter, jitter))
        for f in faces:
            f.material_index = idx
            f.smooth = True
        return faces

    def build(self, parent=None):
        mesh = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        for s in self.slots:
            mesh.materials.append(material(s))
        obj.location = self.pivot
        if parent is not None:
            obj.parent = parent
            obj.matrix_parent_inverse = parent.matrix_world.inverted()
        return obj


# ---------------------------------------------------------------------------------- parts

def build_body():
    p = Part("Body", (0, 0, 0))
    # Robe from a ragged hem up to hunched shoulders; the torso leans forward (-Y).
    p.tube([
        (0.00, 0.00, 0.00, 0.36, 0.30, 0.10),
        (0.06, 0.00, 0.00, 0.34, 0.29, 0.05),
        (0.50, 0.00, 0.00, 0.28, 0.23, 0.02),
        (1.00, 0.00, 0.00, 0.22, 0.18, 0.00),
        (1.30, 0.00, -0.01, 0.19, 0.15, 0.00),
        (1.60, 0.00, -0.03, 0.21, 0.15, 0.00),
        (1.85, 0.00, -0.06, 0.25, 0.15, 0.00),
        (2.02, 0.00, -0.09, 0.27, 0.14, 0.00),
        (2.12, 0.00, -0.11, 0.17, 0.11, 0.00),
        (2.19, 0.00, -0.12, 0.07, 0.06, 0.00),
    ], 18, "MonsterBody", cap_bottom=True)
    # Long thin neck, craning forward then up into the head.
    p.tube([
        (2.17, 0.00, -0.12, 0.065, 0.06, 0.0),
        (2.32, 0.00, -0.10, 0.055, 0.05, 0.0),
        (2.47, 0.00, -0.05, 0.05, 0.05, 0.0),
    ], 10, "MonsterSkin", cap_top=True)
    # Pale under-kimono collar crossing at the chest.
    for side in (-1, 1):
        rot = Matrix.Rotation(math.radians(28 * side), 4, "Y")
        p.blob((0.05 * side, -0.215, 1.86), (0.018, 0.012, 0.17), "MonsterSkin", rot=rot, segs=(8, 6))
    return p.build()


def build_arm(name, side):
    # side: +1 = ArmL at Unity -X (Blender +X), -1 = ArmR.
    p = Part(name, (-0.24 * side, 2.05, 0))
    # Wide kimono sleeve hanging from the shoulder, ragged at the opening.
    p.tube([
        (-0.66, 0.0, 0.0, 0.17, 0.15, 0.14),
        (-0.36, 0.0, 0.0, 0.12, 0.11, 0.02),
        (0.04, 0.0, 0.0, 0.08, 0.08, 0.0),
    ], 14, "MonsterBody", cap_top=True)
    # Far too long, thin forearm.
    p.tube([
        (-1.22, 0.0, -0.01, 0.028, 0.026, 0.0),
        (-0.90, 0.0, 0.00, 0.034, 0.032, 0.0),
        (-0.50, 0.0, 0.00, 0.040, 0.038, 0.0),
    ], 10, "MonsterSkin", cap_bottom=True, cap_top=True)
    # Narrow palm and long fingers curling slightly forward.
    p.blob((0.0, -0.01, -1.28), (0.035, 0.016, 0.07), "MonsterSkin", segs=(10, 8))
    for i, dx in enumerate((-0.026, -0.009, 0.009, 0.026)):
        length = (0.23, 0.27, 0.26, 0.21)[i]
        top = -1.33
        p.tube([
            (top - length, dx * 1.15, -0.05, 0.007, 0.007, 0.0),
            (top - length * 0.55, dx * 1.08, -0.025, 0.009, 0.009, 0.0),
            (top, dx, -0.01, 0.011, 0.011, 0.0),
        ], 6, "MonsterSkin", cap_bottom=True)
    # Thumb on the inner (body) side, pointing forward.
    p.tube([
        (-1.42, -0.032 * side, -0.05, 0.007, 0.007, 0.0),
        (-1.30, -0.028 * side, -0.02, 0.011, 0.011, 0.0),
    ], 6, "MonsterSkin", cap_bottom=True)
    return p.build(parent=bpy.data.objects["Body"])


def build_head():
    p = Part("Head", (0, 2.5, 0.04))
    # Skull, slightly too tall.
    p.blob((0.0, 0.01, 0.0), (0.15, 0.15, 0.2), "Hair", segs=(18, 12))
    # Long pale mask face, chin pushed forward.
    face = p.blob((0.0, -0.10, -0.04), (0.11, 0.065, 0.19), "MonsterFace", segs=(18, 14))
    for v in {v for f in face for v in f.verts}:  # each vertex once (the pole is shared by many faces)
        if v.co.z < -0.1:
            v.co.y -= (-0.1 - v.co.z) * 0.12
    # Narrow, upturned hollow eyes with pinpoint lights, black tear streaks, a long gaping mouth.
    for side in (-1, 1):
        rot = Matrix.Rotation(math.radians(-22 * side), 4, "Y")
        p.blob((0.054 * side, -0.158, 0.005), (0.042, 0.014, 0.013), "MonsterBody", rot=rot, segs=(12, 8))
        p.blob((0.050 * side, -0.170, 0.004), (0.006, 0.005, 0.006), "MonsterEye", segs=(8, 6))
        streak = Matrix.Rotation(math.radians(4 * side), 4, "Y")
        p.blob((0.047 * side, -0.162, -0.065), (0.006, 0.006, 0.06), "MonsterBody", rot=streak, segs=(6, 6))
    p.blob((0.0, -0.162, -0.155), (0.022, 0.016, 0.07), "MonsterBody", segs=(12, 8))

    # Long straight hair: an open shell around the back and sides, falling to mid-back,
    # leaving the face uncovered.
    hair_rings = [
        (0.215, 0.0, 0.02, 0.05, 0.05, 0.0),
        (0.17, 0.0, 0.015, 0.14, 0.145, 0.0),
        (0.05, 0.0, 0.02, 0.17, 0.165, 0.0),
        (-0.12, 0.0, 0.035, 0.17, 0.16, 0.02),
        (-0.35, 0.0, 0.06, 0.2, 0.15, 0.03),
        (-0.70, 0.0, 0.08, 0.22, 0.15, 0.08),
    ]
    sides = 22
    gap = 6  # ring segments left open at the front
    idx = p.slot("Hair")
    vrings = []
    for z, cx, cy, rx, ry, jit in hair_rings:
        ring = []
        for s in range(sides - gap + 1):
            # angle runs around the back: from front-right (-Y side) through +Y to front-left
            a = math.radians(-90 + 360 * (s + gap / 2) / sides)
            j = 1 + (random.uniform(-jit, jit) if jit else 0)
            ring.append(p.bm.verts.new((cx + math.cos(a) * rx * j, cy + math.sin(a) * ry * j, z)))
        vrings.append(ring)
    hair = []
    for r in range(len(vrings) - 1):
        a, b = vrings[r], vrings[r + 1]
        for s in range(len(a) - 1):
            f = p.bm.faces.new((a[s], a[s + 1], b[s + 1], b[s]))
            f.material_index = idx
            f.smooth = True
            hair.append(f)
    # Ragged ends at the bottom.
    for v in vrings[-1]:
        v.co.z -= random.uniform(0.0, 0.18)
    # The shell is open, so give it a separate inward-facing copy (Unity culls back faces).
    # Front and back copies can share positions: only one of them faces any given viewer.
    dup = bmesh.ops.duplicate(p.bm, geom=hair)
    back = [g for g in dup["geom"] if isinstance(g, bmesh.types.BMFace)]
    bmesh.ops.reverse_faces(p.bm, faces=back)
    # A few flat strands hanging over the sides of the face.
    for x, length in ((0.10, 0.42), (0.075, 0.30), (-0.095, 0.38), (-0.12, 0.46)):
        p.tube([
            (0.15 - length, x * 1.05, -0.165 + abs(x) * 0.25, 0.012, 0.004, 0.0),
            (0.15, x, -0.13 + abs(x) * 0.2, 0.016, 0.005, 0.0),
        ], 5, "Hair", cap_bottom=True, cap_top=True)
    return p.build(parent=bpy.data.objects["Body"])


# ---------------------------------------------------------------------------------- export

def export_kfmodel(objs, path):
    depsgraph = bpy.context.evaluated_depsgraph_get()
    parts = []
    for obj in objs:
        ev = obj.evaluated_get(depsgraph)
        mesh = ev.to_mesh()
        mesh.calc_loop_triangles()
        corner_normals = mesh.corner_normals
        verts, norms, lookup = [], [], {}
        subs = {i: [] for i in range(len(obj.material_slots))}
        for tri in mesh.loop_triangles:
            corner = []
            for li in tri.loops:
                co = mesh.vertices[mesh.loops[li].vertex_index].co
                n = corner_normals[li].vector
                pu = (-co.x, co.z, -co.y)
                nu = (-n.x, n.z, -n.y)
                key = tuple(round(c, 5) for c in pu + nu)
                if key not in lookup:
                    lookup[key] = len(verts) // 3
                    verts.extend(round(c, 5) for c in pu)
                    norms.extend(round(c, 4) for c in nu)
                corner.append(lookup[key])
            # Mirrored handedness: reverse the winding so faces stay outward in Unity.
            subs.setdefault(tri.material_index, []).extend((corner[0], corner[2], corner[1]))
        ev.to_mesh_clear()
        pw = obj.matrix_world.translation
        parts.append({
            "name": obj.name,
            "parent": obj.parent.name if obj.parent else "",
            "pivot": [round(-pw.x, 5), round(pw.z, 5), round(-pw.y, 5)],
            "vertices": verts,
            "normals": norms,
            "submeshes": [
                {"material": obj.material_slots[i].material.name, "indices": idx}
                for i, idx in sorted(subs.items()) if idx
            ],
        })
        print(f"[monster] {obj.name}: {len(verts) // 3} verts, {sum(len(s['indices']) for s in parts[-1]['submeshes']) // 3} tris")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump({"version": 1, "parts": parts}, f, separators=(",", ":"))


def render_previews(out_dir):
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
    fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", "POINT"))
    fill.data.energy = 300
    fill.location = (-1.5, -1.8, 2.6)
    scene.collection.objects.link(fill)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    scene.collection.objects.link(cam)
    scene.camera = cam
    os.makedirs(out_dir, exist_ok=True)
    target = Vector((0, 0, 1.45))
    for label, eye, tgt, lens in (
        ("front", Vector((0, -5.2, 1.5)), target, 50),
        ("three_quarter", Vector((3.4, -3.8, 1.9)), target, 50),
        ("side", Vector((5.2, 0, 1.5)), target, 50),
        ("face", Vector((0.35, -1.25, 2.55)), Vector((0, -0.06, 2.48)), 50),
    ):
        cam.location = eye
        cam.rotation_euler = (tgt - eye).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        scene.render.filepath = os.path.join(out_dir, f"monster_{label}.png")
        bpy.ops.render.render(write_still=True)
    for o in (sun, fill, cam):
        bpy.data.objects.remove(o)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None

    bpy.ops.wm.read_factory_settings(use_empty=True)
    body = build_body()
    head = build_head()
    arm_l = build_arm("ArmL", +1)
    arm_r = build_arm("ArmR", -1)
    bpy.context.view_layer.update()
    export_kfmodel([body, head, arm_l, arm_r], OUT_MODEL)

    os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
    print("[monster] saved", OUT_BLEND, "and", OUT_MODEL)

    if preview:
        render_previews(preview)
        print("[monster] previews in", preview)


main()
