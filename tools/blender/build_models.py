"""Build BOO-TRAYAL's first low-poly toon models from code (no downloaded assets).

Usage:
    blender --background --python tools/blender/build_models.py -- <models_dir> <previews_dir>

Writes <models_dir>/<Name>.fbx, <models_dir>/tris.txt and <previews_dir>/<Name>.png.
Exits with code 1 when a model is over its triangle budget.

Conventions (matched by Editor/Builders/ModelImportBuilder.cs):
- Metres, Z up, the front faces +Y -> +Z in Unity (the importer bakes the axis conversion; checked by ModelBudgetTests).
- Characters: origin at the feet. Props: origin at the centre of their collider box.
- Material slot names are the toon material names in Unity: Body, Skin, Hair, EyeWhite, Pupil, Shoe, KrasueSkin,
  KrasueHair, KrasueEye, Glow, Urn, Gold, Clay, Incense, Stick, Ember, Bell, Shrine, Roof, Pillar.
- The player is rigidly skinned (each part follows one bone), which suits the chunky style and never fails to bind.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Euler, Vector

BUDGET = {"Player": 3000, "Krasue": 2500, "Urn": 800, "IncensePot": 800, "ClayPot": 800, "Bell": 800, "SpiritHouse": 1500}
FPS = 24

PALETTE = {
    "Body": "#F2B33D", "Skin": "#FFD9B8", "Hair": "#2A1B14", "EyeWhite": "#F5F2FF", "Pupil": "#120A16", "Shoe": "#3A2E52",
    "KrasueSkin": "#EADFD6", "KrasueHair": "#100A14", "KrasueEye": "#E0213A", "Glow": "#39FF88",
    "Urn": "#C79A3E", "Gold": "#E8C25A", "Clay": "#9E5536", "Incense": "#6E8F4A", "Stick": "#B5452E", "Ember": "#FF8A3D",
    "Bell": "#D9A93A", "Shrine": "#CDA04A", "Roof": "#7A2E2A", "Pillar": "#5A3A26",
}


# ---------------------------------------------------------------- scene and materials

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = FPS
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 900
    scene.render.resolution_y = 900
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.035, 0.02, 0.07, 1.0)
    return scene


def srgb(hex_str):
    hex_str = hex_str.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(hex_str[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    emit = nt.nodes.new("ShaderNodeEmission")
    base = srgb(PALETTE[name])
    cr = ramp.color_ramp
    cr.interpolation = "CONSTANT"
    cr.elements[0].color = tuple(c * 0.4 for c in base[:3]) + (1.0,)
    cr.elements[1].position = 0.3
    cr.elements[1].color = base
    nt.links.new(diff.outputs[0], to_rgb.inputs[0])
    nt.links.new(to_rgb.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], emit.inputs[0])
    nt.links.new(emit.outputs[0], out.inputs[0])
    mat.diffuse_color = base
    return mat


# ---------------------------------------------------------------- mesh helpers

def new_object(name, bm):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def finish(obj, mat_name, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1), group=None):
    obj.location = loc
    obj.rotation_euler = Euler([math.radians(a) for a in rot])
    obj.scale = scale
    obj.data.materials.append(material(mat_name))
    for p in obj.data.polygons:
        p.use_smooth = False
    if group:
        vg = obj.vertex_groups.new(name=group)
        vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    return obj


def sphere(name, mat, radius, loc, segments=10, rings=7, scale=(1, 1, 1), group=None, rot=(0, 0, 0)):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    return finish(new_object(name, bm), mat, loc, rot, scale, group)


def cylinder(name, mat, r1, r2, depth, loc, segments=8, rot=(0, 0, 0), scale=(1, 1, 1), group=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=r1, radius2=r2, depth=depth)
    return finish(new_object(name, bm), mat, loc, rot, scale, group)


def box(name, mat, size, loc, rot=(0, 0, 0), group=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    return finish(new_object(name, bm), mat, loc, rot, size, group)


def lathe(name, mat, profile, segments=12, loc=(0, 0, 0)):
    """Spin a (radius, z) profile around Z; profile from bottom to top."""
    bm = bmesh.new()
    verts = [bm.verts.new((r, 0.0, z)) for r, z in profile]
    edges = [bm.edges.new((verts[i], verts[i + 1])) for i in range(len(verts) - 1)]
    bmesh.ops.spin(bm, geom=verts + edges, cent=(0, 0, 0), axis=(0, 0, 1), angle=math.tau, steps=segments, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    # Close the bottom and the top if the profile starts/ends on the axis.
    return finish(new_object(name, bm), mat, loc)


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    obj.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


# ---------------------------------------------------------------- player (rigged)

BONES = {
    # name: (head, tail, parent)
    "Root": ((0, 0, 0), (0, 0, 0.2), None),
    "Hips": ((0, 0, 0.55), (0, 0, 0.72), "Root"),
    "Spine": ((0, 0, 0.72), (0, 0, 1.06), "Hips"),
    "Head": ((0, 0, 1.08), (0, 0, 1.7), "Spine"),
    "ArmL": ((0.27, 0, 1.0), (0.42, 0, 0.66), "Spine"),
    "ArmR": ((-0.27, 0, 1.0), (-0.42, 0, 0.66), "Spine"),
    "LegL": ((0.12, 0, 0.55), (0.12, 0, 0.06), "Hips"),
    "LegR": ((-0.12, 0, 0.55), (-0.12, 0, 0.06), "Hips"),
}


def build_player():
    parts = [
        cylinder("Torso", "Body", 0.27, 0.22, 0.5, (0, 0, 0.82), segments=10, group="Spine"),
        cylinder("Belly", "Body", 0.22, 0.27, 0.12, (0, 0, 0.6), segments=10, group="Hips"),
        sphere("HeadBall", "Skin", 0.32, (0, 0, 1.38), segments=12, rings=8, scale=(1, 0.95, 0.92), group="Head"),
        sphere("HairCap", "Hair", 0.33, (0, -0.03, 1.47), segments=12, rings=6, scale=(1.02, 0.98, 0.7), group="Head"),
        sphere("EyeWhiteL", "EyeWhite", 0.07, (0.11, 0.28, 1.4), segments=8, rings=5, scale=(1, 0.5, 1.2), group="Head"),
        sphere("EyeWhiteR", "EyeWhite", 0.07, (-0.11, 0.28, 1.4), segments=8, rings=5, scale=(1, 0.5, 1.2), group="Head"),
        sphere("PupilL", "Pupil", 0.035, (0.11, 0.31, 1.39), segments=6, rings=4, scale=(1, 0.5, 1.3), group="Head"),
        sphere("PupilR", "Pupil", 0.035, (-0.11, 0.31, 1.39), segments=6, rings=4, scale=(1, 0.5, 1.3), group="Head"),
        cylinder("ArmMeshL", "Body", 0.07, 0.06, 0.36, (0.345, 0, 0.83), rot=(0, 24, 0), group="ArmL"),
        cylinder("ArmMeshR", "Body", 0.07, 0.06, 0.36, (-0.345, 0, 0.83), rot=(0, -24, 0), group="ArmR"),
        sphere("HandL", "Skin", 0.075, (0.43, 0, 0.64), segments=8, rings=5, group="ArmL"),
        sphere("HandR", "Skin", 0.075, (-0.43, 0, 0.64), segments=8, rings=5, group="ArmR"),
        cylinder("LegMeshL", "Body", 0.09, 0.08, 0.45, (0.12, 0, 0.32), group="LegL"),
        cylinder("LegMeshR", "Body", 0.09, 0.08, 0.45, (-0.12, 0, 0.32), group="LegR"),
        box("ShoeL", "Shoe", (0.16, 0.24, 0.1), (0.12, 0.03, 0.05), group="LegL"),
        box("ShoeR", "Shoe", (0.16, 0.24, 0.1), (-0.12, 0.03, 0.05), group="LegR"),
    ]
    body = join(parts, "PlayerMesh")

    arm_data = bpy.data.armatures.new("PlayerRig")
    rig = bpy.data.objects.new("PlayerRig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for name, (head, tail, parent) in BONES.items():
        b = arm_data.edit_bones.new(name)
        b.head, b.tail = head, tail
        if parent:
            b.parent = arm_data.edit_bones[parent]
    bpy.ops.object.mode_set(mode="OBJECT")

    body.parent = rig
    mod = body.modifiers.new("Armature", "ARMATURE")
    mod.object = rig
    make_actions(rig)
    return [rig, body], body


def key(rig, frame, poses):
    """poses: {bone: (rx, ry, rz) degrees} plus optional ('loc', bone): (x, y, z)."""
    for bone, value in poses.items():
        if isinstance(bone, tuple):
            pb = rig.pose.bones[bone[1]]
            pb.location = value
            pb.keyframe_insert("location", frame=frame)
        else:
            pb = rig.pose.bones[bone]
            pb.rotation_mode = "XYZ"
            pb.rotation_euler = Euler([math.radians(a) for a in value])
            pb.keyframe_insert("rotation_euler", frame=frame)


def action(rig, name, frames, pose_at):
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    for f in frames:
        key(rig, f, pose_at(f))
    return act


ALL = ["Root", "Hips", "Spine", "Head", "ArmL", "ArmR", "LegL", "LegR"]


def rest(**over):
    pose = {b: (0, 0, 0) for b in ALL}
    pose.update(over)
    return pose


def make_actions(rig):
    def idle(f):
        s = math.sin(f / 48 * math.tau)
        return rest(Spine=(2 * s, 0, 0), Head=(-2 * s, 0, 3 * s), ArmL=(0, 0, 4 + 2 * s), ArmR=(0, 0, -4 - 2 * s))

    def gait(period, leg, arm, lean):
        def pose(f):
            s = math.sin(f / period * math.tau)
            p = rest(LegL=(leg * s, 0, 0), LegR=(-leg * s, 0, 0), ArmL=(-arm * s, 0, 6), ArmR=(arm * s, 0, -6),
                     Spine=(-lean, 0, 3 * s), Head=(lean * 0.5, 0, -3 * s))
            p[("loc", "Hips")] = (0, 0, 0.025 * abs(math.sin(f / period * math.tau)))
            return p
        return pose

    def carry(f):
        s = math.sin(f / 24 * math.tau)
        return rest(ArmL=(80, 0, 4), ArmR=(80, 0, -4), Spine=(4 + s, 0, 0), LegL=(6 * s, 0, 0), LegR=(-6 * s, 0, 0))

    def fall(f):
        t = min(1.0, f / 14)
        return rest(Root=(80 * t * t, 0, 0), ArmL=(40 * t, 0, 40 * t), ArmR=(40 * t, 0, -40 * t), LegL=(-20 * t, 0, 0))

    action(rig, "Idle", range(0, 49, 6), idle)
    action(rig, "Walk", range(0, 21, 2), gait(20, 28, 22, 4))
    action(rig, "Run", range(0, 13, 2), gait(12, 48, 42, 12))
    action(rig, "Carry", range(0, 25, 6), carry)
    action(rig, "Fall", range(0, 15, 2), fall)
    rig.animation_data.action = bpy.data.actions["Idle"]


# ---------------------------------------------------------------- Krasue and props

def build_krasue():
    h = 1.4
    parts = [
        sphere("Face", "KrasueSkin", 0.23, (0, 0, h), segments=12, rings=8, scale=(0.95, 0.9, 1.05)),
        sphere("HairTop", "KrasueHair", 0.27, (0, -0.05, h + 0.06), segments=12, rings=7, scale=(1.05, 1.0, 0.95)),
        cylinder("HairTail", "KrasueHair", 0.15, 0.05, 0.34, (0, -0.2, h - 0.2), segments=8, rot=(18, 0, 0)),
        sphere("EyeL", "KrasueEye", 0.045, (0.08, 0.205, h + 0.03), segments=6, rings=4, scale=(1.3, 0.5, 0.8)),
        sphere("EyeR", "KrasueEye", 0.045, (-0.08, 0.205, h + 0.03), segments=6, rings=4, scale=(1.3, 0.5, 0.8)),
        box("Mouth", "KrasueHair", (0.1, 0.03, 0.025), (0, 0.215, h - 0.1)),
    ]
    # Entrails: three tapering, gently curving strands hanging under the neck.
    for i, (x, length, sway) in enumerate([(0.0, 0.75, 6), (0.06, 0.6, -10), (-0.06, 0.5, 14)]):
        z = h - 0.22
        r = 0.07 - 0.012 * i
        for seg in range(4):
            seg_len = length / 4
            parts.append(cylinder(f"Gut{i}_{seg}", "Glow", r, r * 0.8, seg_len,
                                  (x + math.sin(seg) * 0.03 * (i - 1), -0.01 * seg, z - seg_len * 0.5),
                                  segments=6, rot=(sway * (seg % 2 * 2 - 1) * 0.4, sway, 0)))
            z -= seg_len * 0.95
            r *= 0.82
    return [join(parts, "KrasueMesh")]


def build_urn():
    profile = [(0.0, -0.6), (0.24, -0.6), (0.36, -0.42), (0.45, -0.1), (0.42, 0.18), (0.26, 0.38), (0.2, 0.46),
               (0.24, 0.5), (0.2, 0.52), (0.0, 0.53)]
    urn = lathe("UrnBody", "Urn", profile, segments=14)
    lid = lathe("UrnLid", "Gold", [(0.0, 0.52), (0.18, 0.52), (0.12, 0.56), (0.04, 0.6), (0.0, 0.6)], segments=10)
    band = cylinder("UrnBand", "Gold", 0.455, 0.455, 0.06, (0, 0, -0.05), segments=14)
    return [join([urn, lid, band], "UrnMesh")]


def build_incense():
    pot = lathe("IncenseBowl", "Incense", [(0.0, -0.25), (0.09, -0.25), (0.125, -0.15), (0.12, -0.02), (0.1, 0.0), (0.0, 0.0)], segments=10)
    parts = [pot]
    for i, (x, y) in enumerate([(0.0, 0.0), (0.035, 0.02), (-0.03, -0.02)]):
        parts.append(cylinder(f"Stick{i}", "Stick", 0.006, 0.006, 0.24, (x, y, 0.12), segments=4, rot=(4 * (i - 1), -5 * (i - 1), 0)))
        parts.append(sphere(f"Ember{i}", "Ember", 0.01, (x * 1.3, y * 1.3, 0.245), segments=4, rings=3))
    return [join(parts, "IncenseMesh")]


def build_clay_pot():
    profile = [(0.0, -0.25), (0.12, -0.25), (0.19, -0.12), (0.2, 0.02), (0.15, 0.17), (0.11, 0.21), (0.13, 0.25), (0.0, 0.25)]
    return [join([lathe("ClayBody", "Clay", profile, segments=12)], "ClayPotMesh")]


def build_bell():
    profile = [(0.0, 0.12), (0.03, 0.12), (0.05, 0.1), (0.07, 0.04), (0.09, -0.06), (0.11, -0.11), (0.115, -0.12), (0.0, -0.12)]
    bell = lathe("BellBody", "Bell", profile, segments=12)
    hook = cylinder("BellHook", "Pillar", 0.012, 0.012, 0.06, (0, 0, 0.15), segments=5)
    clapper = sphere("Clapper", "Pillar", 0.025, (0, 0, -0.1), segments=6, rings=4)
    return [join([bell, hook, clapper], "BellMesh")]


def build_spirit_house():
    parts = [
        cylinder("Pedestal", "Pillar", 0.15, 0.13, 1.2, (0, 0, 0.6), segments=8),
        box("Base", "Shrine", (0.9, 0.8, 0.08), (0, 0, 1.18)),
        box("Hall", "Shrine", (0.64, 0.54, 0.5), (0, 0, 1.45)),
        box("Door", "Roof", (0.22, 0.02, 0.32), (0, 0.28, 1.4)),
    ]
    # Two-tier Thai roof with chofa horns at the gable tips.
    for tier, (w, z, d) in enumerate([(0.86, 1.82, 0.78), (0.62, 2.02, 0.56)]):
        for side in (-1, 1):
            parts.append(box(f"Roof{tier}{side}", "Roof", (w, d * 0.62, 0.04), (0, side * d * 0.22, z), rot=(side * 38, 0, 0)))
        for end in (-1, 1):
            parts.append(cylinder(f"Chofa{tier}{end}", "Gold", 0.018, 0.004, 0.18, (end * w * 0.5, 0, z + 0.16), segments=4,
                                  rot=(0, end * -30, 0)))
    parts.append(cylinder("Spire", "Gold", 0.05, 0.005, 0.32, (0, 0, 2.28), segments=6))
    return [join(parts, "SpiritHouseMesh")]


# ---------------------------------------------------------------- export, preview, budget

def export(objects, path, animated):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "ARMATURE"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        mesh_smooth_type="FACE", add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=animated, bake_anim_use_all_actions=animated, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)


def preview(objects, path):
    scene = bpy.context.scene
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objects:
        if o.type != "MESH":
            continue
        for corner in o.bound_box:
            w = o.matrix_world @ Vector(corner)
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
    center = (lo + hi) / 2
    size = max((hi - lo).length, 0.2)
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    direction = Vector((0.55, 1.0, 0.35)).normalized()
    cam.location = center + direction * size * 1.7
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    cam_data.lens = 50
    scene.camera = cam
    sun_data = bpy.data.lights.new("PreviewSun", "SUN")
    sun_data.energy = 3.0
    sun = bpy.data.objects.new("PreviewSun", sun_data)
    sun.rotation_euler = Euler((math.radians(50), math.radians(10), math.radians(30)))
    scene.collection.objects.link(sun)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    models_dir = os.path.abspath(argv[0] if argv else "Assets/_Project/Art/Models")
    previews_dir = os.path.abspath(argv[1] if len(argv) > 1 else "docs/art_previews/models")
    os.makedirs(models_dir, exist_ok=True)
    os.makedirs(previews_dir, exist_ok=True)

    builders = [("Player", build_player, True), ("Krasue", build_krasue, False), ("Urn", build_urn, False),
                ("IncensePot", build_incense, False), ("ClayPot", build_clay_pot, False), ("Bell", build_bell, False),
                ("SpiritHouse", build_spirit_house, False)]
    report = []
    over = []
    for name, build, animated in builders:
        reset()
        result = build()
        if isinstance(result, tuple):
            objects, mesh = result
        else:
            objects, mesh = result, result[0]
        tris = triangles(mesh)
        report.append(f"{name} {tris} {BUDGET[name]}")
        if tris > BUDGET[name]:
            over.append(f"{name} {tris} > {BUDGET[name]}")
        export(objects, os.path.join(models_dir, f"{name}.fbx"), animated)
        preview(objects, os.path.join(previews_dir, f"{name}.png"))
        print(f"MODEL_BUILT name={name} tris={tris} budget={BUDGET[name]}")

    with open(os.path.join(models_dir, "tris.txt"), "w", encoding="utf-8") as f:
        f.write("# model triangles budget (written by tools/blender/build_models.py)\n")
        f.write("\n".join(report) + "\n")
    if over:
        print("MODELS_OVER_BUDGET " + "; ".join(over))
        sys.exit(1)
    print("MODELS_OK")


if __name__ == "__main__":
    main()
