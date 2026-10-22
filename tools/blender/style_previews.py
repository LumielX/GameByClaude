"""Render art-style comparison previews (player + Krasue ghost) for the GDD.

Usage:
    blender --background --python tools/blender/style_previews.py -- <out_dir>

Renders three candidate styles: lowpoly_toon, soft_clay, chunky_blocky.
"""
import math
import os
import sys

import bpy
from mathutils import Vector

STYLES = ["lowpoly_toon", "soft_clay", "chunky_blocky"]


# ---------------------------------------------------------------- utilities

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in engines else "BLENDER_EEVEE"
    scene.render.resolution_x = 1280
    scene.render.resolution_y = 720
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "Standard"
    try:
        scene.eevee.taa_render_samples = 32
    except AttributeError:
        pass
    world = bpy.data.worlds.new("World")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.035, 0.02, 0.07, 1.0)
    bg.inputs["Strength"].default_value = 1.0
    return scene


def srgb(hex_str):
    hex_str = hex_str.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(hex_str[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def scaled(color, k):
    return (color[0] * k, color[1] * k, color[2] * k, 1.0)


def mat_toon(name, hex_color, bands=3):
    base = srgb(hex_color)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    to_rgb = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    emit = nt.nodes.new("ShaderNodeEmission")
    cr = ramp.color_ramp
    cr.interpolation = "CONSTANT"
    cr.elements[0].position = 0.0
    cr.elements[0].color = scaled(base, 0.38)
    if bands >= 3:
        cr.elements[1].position = 0.25
        cr.elements[1].color = scaled(base, 0.72)
        e = cr.elements.new(0.6)
        e.color = base
    else:
        cr.elements[1].position = 0.3
        cr.elements[1].color = base
    nt.links.new(diff.outputs[0], to_rgb.inputs[0])
    nt.links.new(to_rgb.outputs[0], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], emit.inputs[0])
    nt.links.new(emit.outputs[0], out.inputs[0])
    return mat


def mat_clay(name, hex_color):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = srgb(hex_color)
    bsdf.inputs["Roughness"].default_value = 0.55
    for key in ("Subsurface Weight", "Subsurface"):
        if key in bsdf.inputs:
            bsdf.inputs[key].default_value = 0.15
            break
    return mat


def mat_emit(name, hex_color, strength):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = srgb(hex_color)
    emit.inputs["Strength"].default_value = strength
    nt.links.new(emit.outputs[0], out.inputs[0])
    return mat


def mat_outline():
    mat = bpy.data.materials.get("Outline")
    if mat:
        return mat
    mat = mat_emit("Outline", "#140a1f", 1.0)
    mat.use_backface_culling = True
    return mat


class Style:
    def __init__(self, kind):
        self.kind = kind
        self._mats = {}

    def material(self, hex_color):
        if hex_color in self._mats:
            return self._mats[hex_color]
        if self.kind == "soft_clay":
            m = mat_clay("M_" + hex_color, hex_color)
        else:
            m = mat_toon("M_" + hex_color, hex_color, 3 if self.kind == "lowpoly_toon" else 2)
        self._mats[hex_color] = m
        return m

    def blob(self, name, loc, size, hex_color, rot=(0, 0, 0), outline=True):
        """Rounded primitive whose look depends on style. size = (x, y, z) half-extents."""
        if self.kind == "lowpoly_toon":
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=1.0, location=loc)
        elif self.kind == "soft_clay":
            bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, radius=1.0, location=loc)
        else:
            bpy.ops.mesh.primitive_cube_add(size=2.0, location=loc)
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = size
        obj.rotation_euler = rot
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if self.kind == "soft_clay":
            bpy.ops.object.shade_smooth()
            obj.modifiers.new("Subsurf", "SUBSURF").levels = 1
        elif self.kind == "chunky_blocky":
            bev = obj.modifiers.new("Bevel", "BEVEL")
            bev.width = min(size) * 0.25
            bev.segments = 2
        else:
            bpy.ops.object.shade_flat()
        self.assign(obj, hex_color, outline)
        return obj

    def assign(self, obj, hex_color, outline=True):
        obj.data.materials.append(self.material(hex_color))
        if outline and self.kind != "soft_clay":
            obj.data.materials.append(mat_outline())
            sol = obj.modifiers.new("Outline", "SOLIDIFY")
            sol.thickness = 0.03
            sol.offset = 1.0
            sol.use_flip_normals = True
            sol.use_rim = False
            sol.material_offset = 1


def plain(name, prim, loc, scale, mat, rot=(0, 0, 0), **kw):
    getattr(bpy.ops.mesh, prim)(location=loc, **kw)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    obj.rotation_euler = rot
    obj.data.materials.append(mat)
    return obj


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


# ---------------------------------------------------------------- content

def eye(style, name, loc, r):
    style.blob(name + "_white", loc, (r, r * 0.6, r * 1.15), "#f4f1ff", outline=True)
    pupil_loc = (loc[0], loc[1] - r * 0.5, loc[2] - r * 0.1)
    style.blob(name + "_pupil", pupil_loc, (r * 0.55, r * 0.3, r * 0.65), "#120a18", outline=False)
    hl = mat_emit("Highlight", "#ffffff", 2.0)
    plain(name + "_hl", "primitive_uv_sphere_add",
          (loc[0] + r * 0.18, loc[1] - r * 0.72, loc[2] + r * 0.2),
          (r * 0.16, r * 0.1, r * 0.16), hl, radius=1.0, segments=12, ring_count=6)


def build_player(style, origin):
    ox, oy, oz = origin
    coat = "#f2b33d"  # mustard raincoat
    skin = "#ffd9b8"
    # legs
    style.blob("LegL", (ox - 0.22, oy, oz + 0.25), (0.15, 0.15, 0.26), "#3b2f5c")
    style.blob("LegR", (ox + 0.22, oy, oz + 0.25), (0.15, 0.15, 0.26), "#3b2f5c")
    # bean body
    style.blob("Body", (ox, oy, oz + 0.85), (0.48, 0.42, 0.52), coat)
    # big head
    style.blob("Head", (ox, oy, oz + 1.72), (0.62, 0.56, 0.58), skin)
    # hood/hat
    style.blob("Hat", (ox, oy + 0.06, oz + 2.12), (0.5, 0.48, 0.25), "#7b3fe4")
    style.blob("HatBall", (ox + 0.12, oy + 0.05, oz + 2.42), (0.11, 0.11, 0.11), "#39ff88")
    # scared arms raised
    style.blob("ArmL", (ox - 0.62, oy, oz + 1.18), (0.12, 0.12, 0.34), coat, rot=(0, math.radians(-35), 0))
    style.blob("ArmR", (ox + 0.62, oy, oz + 1.18), (0.12, 0.12, 0.34), coat, rot=(0, math.radians(35), 0))
    style.blob("HandL", (ox - 0.82, oy, oz + 1.48), (0.12, 0.12, 0.12), skin)
    style.blob("HandR", (ox + 0.82, oy, oz + 1.48), (0.12, 0.12, 0.12), skin)
    # huge eyes looking at the ghost
    eye(style, "EyeL", (ox - 0.2, oy - 0.48, oz + 1.8), 0.17)
    eye(style, "EyeR", (ox + 0.2, oy - 0.48, oz + 1.8), 0.17)
    # screaming mouth
    style.blob("Mouth", (ox, oy - 0.53, oz + 1.47), (0.11, 0.05, 0.13), "#3a0f24", outline=False)


def build_krasue(style, origin):
    ox, oy, oz = origin
    skin = "#e8e3f2"
    style.blob("K_Head", (ox, oy, oz), (0.5, 0.46, 0.52), skin)
    style.blob("K_Hair", (ox, oy + 0.1, oz + 0.12), (0.58, 0.5, 0.56), "#1a1426")
    style.blob("K_Bangs", (ox, oy - 0.3, oz + 0.36), (0.46, 0.14, 0.16), "#1a1426")
    # sleepy-mischief eyes
    style.blob("K_EyeL", (ox - 0.17, oy - 0.42, oz + 0.05), (0.1, 0.04, 0.05), "#ff3d7f", outline=False)
    style.blob("K_EyeR", (ox + 0.17, oy - 0.42, oz + 0.05), (0.1, 0.04, 0.05), "#ff3d7f", outline=False)
    style.blob("K_Blush", (ox - 0.3, oy - 0.38, oz - 0.08), (0.08, 0.03, 0.05), "#ff9ac1", outline=False)
    style.blob("K_Blush2", (ox + 0.3, oy - 0.38, oz - 0.08), (0.08, 0.03, 0.05), "#ff9ac1", outline=False)
    # cheeky grin
    style.blob("K_Grin", (ox, oy - 0.44, oz - 0.2), (0.16, 0.04, 0.05), "#3a0f24", outline=False)
    # glowing dangling "organs", cute and stylized
    glow = mat_emit("KrasueGlow", "#ff4fa3", 4.0)
    heart = mat_emit("KrasueHeart", "#ff2d55", 5.0)
    plain("K_Heart", "primitive_uv_sphere_add", (ox, oy, oz - 0.68), (0.14, 0.12, 0.14), heart,
          radius=1.0, segments=16, ring_count=8)
    for i in range(6):
        z = oz - 0.85 - i * 0.2
        x = ox + math.sin(i * 1.3) * 0.12
        r = 0.09 - i * 0.008
        plain("K_Trail%d" % i, "primitive_uv_sphere_add", (x, oy, z), (r, r, r), glow,
              radius=1.0, segments=12, ring_count=6)
    bpy.ops.object.light_add(type="POINT", location=(ox, oy - 0.4, oz - 0.9))
    pl = bpy.context.active_object
    pl.data.energy = 120
    pl.data.color = (1.0, 0.35, 0.65)


def build_set(style):
    floor = mat_toon("Floor", "#3a2a24", 2) if style.kind != "soft_clay" else mat_clay("Floor", "#3a2a24")
    plain("Floor", "primitive_plane_add", (0, 2, 0), (12, 12, 1), floor)
    wood = "#5b3a2a"
    # stilt-house pillars behind
    for x in (-3.2, -1.2, 1.4, 3.4):
        style.blob("Pillar", (x, 3.2, 1.8), (0.16, 0.16, 1.8), wood)
    style.blob("Beam", (0.1, 3.2, 3.5), (3.8, 0.2, 0.14), wood)
    # spirit house (san phra phum)
    style.blob("SpiritPole", (-2.6, 1.6, 0.6), (0.08, 0.08, 0.6), "#c9a24a")
    style.blob("SpiritHouse", (-2.6, 1.6, 1.4), (0.32, 0.26, 0.24), "#e0b84d")
    style.blob("SpiritRoof", (-2.6, 1.6, 1.78), (0.42, 0.32, 0.12), "#b2302f")
    # green neon lantern
    lantern = mat_emit("Lantern", "#39ff88", 6.0)
    plain("Lantern", "primitive_uv_sphere_add", (2.5, 1.2, 1.6), (0.16, 0.16, 0.22), lantern,
          radius=1.0, segments=16, ring_count=8)
    bpy.ops.object.light_add(type="POINT", location=(2.5, 1.0, 1.6))
    gl = bpy.context.active_object
    gl.data.energy = 150
    gl.data.color = (0.3, 1.0, 0.55)


def build_lights_camera(scene):
    bpy.ops.object.light_add(type="SUN", location=(4, -4, 8))
    sun = bpy.context.active_object
    sun.data.energy = 2.2
    sun.data.color = (0.62, 0.7, 1.0)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    bpy.ops.object.light_add(type="AREA", location=(-3, 2, 3))
    rim = bpy.context.active_object
    rim.data.energy = 300
    rim.data.size = 3
    rim.data.color = (0.65, 0.35, 1.0)
    look_at(rim, (0, 0, 1.2))
    bpy.ops.object.camera_add(location=(0.3, -6.4, 2.3))
    cam = bpy.context.active_object
    cam.data.lens = 38
    look_at(cam, (0.15, 0, 1.35))
    scene.camera = cam


def render_style(kind, out_dir):
    scene = reset_scene()
    style = Style(kind)
    build_set(style)
    build_player(style, (-0.9, 0, 0))
    build_krasue(style, (1.25, 0.2, 2.15))
    build_lights_camera(scene)
    scene.render.filepath = os.path.join(out_dir, "style_%s.png" % kind)
    bpy.ops.render.render(write_still=True)
    print("RENDERED", scene.render.filepath)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out_dir = os.path.abspath(argv[0] if argv else "docs/art_previews")
    os.makedirs(out_dir, exist_ok=True)
    for kind in STYLES:
        render_style(kind, out_dir)


if __name__ == "__main__":
    main()
