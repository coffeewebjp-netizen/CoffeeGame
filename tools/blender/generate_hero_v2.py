"""Generate CoffeeGAME's refined procedural 3D heroine v2.

Run:
  blender -b --python tools/blender/generate_hero_v2.py

Validate an already generated file:
  blender -b art/3d/source/heroine-v2.blend \
    --python tools/blender/generate_hero_v2.py -- --validate-only

The source scene is Z-up and faces -Y. FBX export converts it to Unity's
Y-up, +Z-forward convention.  The model deliberately stays in a mobile-safe
mid-poly range while replacing the v1 cuboid blockout with rounded anatomy,
curved clothing, real skirt pleats, layered hair cards, and a modeled face.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import generate_hero as base  # noqa: E402


BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v2.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v2.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v2.json"
FBX_VALIDATION_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v2-fbx-validation.json"
REFERENCE_PATH = ROOT / "art" / "3d" / "reference" / "hero-turnaround-v1.png"
PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v2.png"
FACE_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v2-face.png"

ACTION_NAMES = base.ACTION_NAMES
RIG = None


def apply_modifier(obj, name: str) -> None:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=name)
    obj.select_set(False)


def bind(obj, bone: str) -> None:
    group = obj.vertex_groups.new(name=bone)
    group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("HeroRig", "ARMATURE")
    mod.object = RIG
    mod.use_deform_preserve_volume = True


def finish(obj, mat, bone: str, smooth=True, bevel=0.0, subdivision=0):
    obj.data.materials.append(mat)
    if smooth:
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    base.apply_transform(obj)
    if bevel:
        mod = obj.modifiers.new("TailoredEdges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        apply_modifier(obj, mod.name)
    if subdivision:
        mod = obj.modifiers.new("SilhouetteSmoothing", "SUBSURF")
        mod.levels = subdivision
        mod.render_levels = subdivision
        apply_modifier(obj, mod.name)
    bind(obj, bone)
    return obj


def uv(name, loc, scale, mat, bone, segments=36, rings=24, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=rings, location=loc, rotation=rotation
    )
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, mat, bone)


def rounded_box(name, loc, scale, mat, bone, rotation=(0, 0, 0), bevel=0.015):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.scale = scale
    return finish(obj, mat, bone, smooth=True, bevel=bevel, subdivision=1)


def sweep_mesh(name, points, radii, mat, bone, sides=20, depth_ratio=0.82, cap=True):
    """Build an organic tapered tube through points with elliptical rings."""
    points = [Vector(p) for p in points]
    if isinstance(radii, (int, float)):
        radii = [float(radii)] * len(points)
    vertices = []
    faces = []
    for i, (point, radius) in enumerate(zip(points, radii)):
        if i == 0:
            tangent = (points[1] - point).normalized()
        elif i == len(points) - 1:
            tangent = (point - points[i - 1]).normalized()
        else:
            tangent = (points[i + 1] - points[i - 1]).normalized()
        basis_y = Vector((0, 1, 0))
        if abs(tangent.dot(basis_y)) > 0.92:
            basis_y = Vector((1, 0, 0))
        basis_x = tangent.cross(basis_y).normalized()
        basis_y = basis_x.cross(tangent).normalized()
        for j in range(sides):
            angle = math.tau * j / sides
            vertices.append(tuple(point + basis_x * math.cos(angle) * radius + basis_y * math.sin(angle) * radius * depth_ratio))
    for i in range(len(points) - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            c = (i + 1) * sides + (j + 1) % sides
            d = (i + 1) * sides + j
            faces.append((a, b, c, d))
    if cap:
        start_center = len(vertices)
        vertices.append(tuple(points[0]))
        end_center = len(vertices)
        vertices.append(tuple(points[-1]))
        for j in range(sides):
            faces.append((start_center, (j + 1) % sides, j))
            a = (len(points) - 1) * sides + j
            b = (len(points) - 1) * sides + (j + 1) % sides
            faces.append((end_center, a, b))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, bone)


def ribbon_mesh(name, points, widths, thickness, mat, bone, outward=(0, -1, 0), bevel=0.003):
    """Create a tapered solid ribbon; used for layered anime hair and cloth ties."""
    points = [Vector(p) for p in points]
    if isinstance(widths, (int, float)):
        widths = [float(widths)] * len(points)
    normal = Vector(outward).normalized()
    vertices = []
    for i, (point, width) in enumerate(zip(points, widths)):
        if i == 0:
            tangent = (points[1] - point).normalized()
        elif i == len(points) - 1:
            tangent = (point - points[i - 1]).normalized()
        else:
            tangent = (points[i + 1] - points[i - 1]).normalized()
        side = normal.cross(tangent)
        if side.length < 1e-5:
            side = Vector((1, 0, 0))
        side.normalize()
        for front in (-1, 1):
            for lateral in (-1, 1):
                vertices.append(tuple(point + normal * thickness * 0.5 * front + side * width * lateral))
    faces = []
    for i in range(len(points) - 1):
        a = i * 4
        b = (i + 1) * 4
        faces.extend([
            (a, a + 1, b + 1, b),
            (a + 2, b + 2, b + 3, a + 3),
            (a, b, b + 2, a + 2),
            (a + 1, a + 3, b + 3, b + 1),
        ])
    faces.extend([(0, 2, 3, 1), (len(vertices) - 4, len(vertices) - 3, len(vertices) - 1, len(vertices) - 2)])
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, bone, smooth=True, bevel=bevel, subdivision=1)


def cloth_panel(name, points, thickness, mat, bone, bevel=0.003):
    """Create a thin tailored polygon panel with closed, softly beveled edges."""
    front = [(x, y - thickness * 0.5, z) for x, y, z in points]
    back = [(x, y + thickness * 0.5, z) for x, y, z in points]
    vertices = front + back
    n = len(points)
    faces = [tuple(range(n)), tuple(range(n, n * 2))[::-1]]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, j, n + j, n + i))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, bone, smooth=True, bevel=bevel)


def shaped_head(name, loc, scale, mat, bone, segments=64, rings=40):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=loc)
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        z = vertex.co.z
        if z < -0.18:
            taper = 1.0 + 0.34 * (z + 0.18)
        elif z > 0.62:
            taper = 1.0 - 0.12 * (z - 0.62) / 0.38
        else:
            taper = 1.0
        vertex.co.x *= scale[0] * taper
        vertex.co.y *= scale[1] * (0.96 + 0.05 * max(z, 0.0))
        vertex.co.z *= scale[2]
        if z < -0.45:
            vertex.co.y += 0.008 * (-z - 0.45)
    return finish(obj, mat, bone)


def hair_cap(name, center, scale, mat, bone, segments=56, rings=27):
    """Open-front scalp shell so skin remains visible instead of a helmet face."""
    cx, cy, cz = center
    vertices = []
    valid = []
    for r in range(rings + 1):
        phi = math.pi * 0.78 * r / rings
        row = []
        for s in range(segments):
            theta = math.tau * s / segments
            x = cx + math.sin(phi) * math.cos(theta) * scale[0]
            y = cy + math.sin(phi) * math.sin(theta) * scale[1]
            z = cz + math.cos(phi) * scale[2]
            row.append(len(vertices))
            vertices.append((x, y, z))
        valid.append(row)
    faces = []
    for r in range(rings):
        for s in range(segments):
            ids = (valid[r][s], valid[r][(s + 1) % segments], valid[r + 1][(s + 1) % segments], valid[r + 1][s])
            center_y = sum(vertices[i][1] for i in ids) / 4
            center_z = sum(vertices[i][2] for i in ids) / 4
            if center_y < cy - 0.025 and center_z < cz + 0.025:
                continue
            faces.append(ids)
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return finish(obj, mat, bone)


def coat_shell(mat, trim):
    z_rows = [0.90, 0.95, 1.03, 1.12, 1.21, 1.27]
    radii = [(0.215, 0.102), (0.205, 0.098), (0.188, 0.093), (0.178, 0.090), (0.172, 0.087), (0.155, 0.082)]
    angular = 36
    gap = 0.31
    vertices = []
    for z, (rx, ry) in zip(z_rows, radii):
        for i in range(angular):
            t = i / (angular - 1)
            phi = -math.pi / 2 + gap + (math.tau - 2 * gap) * t
            vertices.append((rx * math.cos(phi), ry * math.sin(phi) + 0.012, z))
    faces = []
    for row in range(len(z_rows) - 1):
        for i in range(angular - 1):
            a = row * angular + i
            faces.append((a, a + 1, a + 1 + angular, a + angular))
    mesh = bpy.data.meshes.new("HaoriBodyMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("HaoriBody", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    solid = obj.modifiers.new("ClothThickness", "SOLIDIFY")
    solid.thickness = 0.010
    solid.offset = 0
    apply_modifier(obj, solid.name)
    bevel = obj.modifiers.new("SoftSeams", "BEVEL")
    bevel.width = 0.004
    bevel.segments = 2
    apply_modifier(obj, bevel.name)
    bind(obj, "Spine")

    # Flat curved lapels preserve the open haori shape.
    ribbon_mesh("HaoriLapel.L", [(0.055, -0.088, 1.28), (0.075, -0.098, 1.20), (0.095, -0.102, 1.10), (0.108, -0.101, 0.94)],
                [0.022, 0.026, 0.026, 0.023], 0.009, trim, "Chest", outward=(0, -1, 0), bevel=0.002)
    ribbon_mesh("HaoriLapel.R", [(-0.055, -0.088, 1.28), (-0.075, -0.098, 1.20), (-0.095, -0.102, 1.10), (-0.108, -0.101, 0.94)],
                [0.022, 0.026, 0.026, 0.023], 0.009, trim, "Chest", outward=(0, -1, 0), bevel=0.002)


def pleated_skirt(mat, shadow):
    sectors = 28
    rows = [(0.93, 0.158), (0.88, 0.180), (0.73, 0.255)]
    vertices = []
    for z, radius in rows:
        for i in range(sectors * 2):
            angle = math.tau * i / (sectors * 2)
            fold = 0.014 if i % 2 == 0 else -0.010
            rr = radius + fold * ((0.93 - z) / 0.20 + 0.25)
            vertices.append((math.sin(angle) * rr, math.cos(angle) * rr, z))
    faces = []
    count = sectors * 2
    for row in range(len(rows) - 1):
        for i in range(count):
            a = row * count + i
            faces.append((a, row * count + (i + 1) % count,
                          (row + 1) * count + (i + 1) % count, (row + 1) * count + i))
    mesh = bpy.data.meshes.new("PleatedSkirtMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("PleatedSkirt", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    solid = obj.modifiers.new("SkirtThickness", "SOLIDIFY")
    solid.thickness = 0.007
    solid.offset = 0
    apply_modifier(obj, solid.name)
    bevel = obj.modifiers.new("PleatSoftness", "BEVEL")
    bevel.width = 0.0025
    bevel.segments = 2
    apply_modifier(obj, bevel.name)
    bind(obj, "Pelvis")

    # A dark inner hem gives the pleats readable depth from low camera angles.
    bpy.ops.mesh.primitive_torus_add(major_radius=0.241, minor_radius=0.006,
                                     major_segments=56, minor_segments=8,
                                     location=(0, 0, 0.732))
    hem = bpy.context.object
    hem.name = "SkirtHem"
    finish(hem, shadow, "Pelvis")


def build_hair(m):
    hair = m["hair"]
    shadow = m["hair_shadow"]
    hair_cap("HairScalp", (0, 0.012, 1.485), (0.145, 0.115, 0.155), hair, "Head")

    # Back-half bob: overlapping leaf-like ribbons.  The front half is left
    # clear for the dedicated fringe and cheek locks below, avoiding strands
    # crossing the mouth/chin.
    for i in range(13):
        theta = math.radians(i * (180 / 12))
        x = math.cos(theta) * 0.125
        y = math.sin(theta) * 0.100 + 0.014
        outward = Vector((x, y - 0.014, 0)).normalized()
        z_end = 1.335 + 0.020 * (i % 3)
        ribbon_mesh(
            f"BobLayer.{i:02d}",
            [(x * 0.72, y * 0.72, 1.545), (x * 0.94, y * 0.94, 1.485),
             (x * 1.08, y * 1.08, 1.405), (x * 1.11, y * 1.10, z_end)],
            [0.027, 0.034, 0.030, 0.004], 0.014,
            shadow if i % 4 == 0 else hair, "Head", outward=tuple(outward), bevel=0.004,
        )

    # Forehead fringe. Varying length keeps the eyes visible and avoids a comb shape.
    xs = [-0.112, -0.086, -0.060, -0.032, 0.0, 0.032, 0.060, 0.086, 0.112]
    for i, x in enumerate(xs):
        end_z = 1.485 + (0.018 if i in (0, 8) else 0.0) + (0.016 if i in (3, 5) else 0.0)
        slant = (i - 4) * 0.003
        ribbon_mesh(
            f"Fringe.{i:02d}",
            [(x * 0.62, -0.072, 1.595), (x * 0.82, -0.106, 1.555),
             (x + slant, -0.116, 1.515), (x + slant * 1.3, -0.118, end_z)],
            [0.019, 0.022, 0.017, 0.0025], 0.010, hair, "Head",
            outward=(0, -1, 0), bevel=0.003,
        )

    for side, x in (("L", 1), ("R", -1)):
        ribbon_mesh(
            f"FaceLock.{side}",
            [(0.118 * x, -0.050, 1.555), (0.139 * x, -0.075, 1.500),
             (0.145 * x, -0.085, 1.420), (0.125 * x, -0.090, 1.350)],
            [0.028, 0.032, 0.026, 0.003], 0.012, shadow if side == "R" else hair,
            "Head", outward=(0, -1, 0), bevel=0.004,
        )

    # Curved ahoge as a smooth tapered sweep plus a fine tip.
    sweep_mesh("AhogeBase", [(0, 0.012, 1.613), (0.012, 0.000, 1.652),
                              (0.050, -0.002, 1.672), (0.086, 0.010, 1.652)],
               [0.008, 0.007, 0.005, 0.002], hair, "Head", sides=12, depth_ratio=1.0)


def build_face(m):
    skin, white, amber, pupil = m["skin"], m["white"], m["amber"], m["pupil"]
    shaped_head("Head", (0, 0, 1.460), (0.132, 0.105, 0.148), skin, "Head")
    uv("Neck", (0, 0.010, 1.302), (0.050, 0.046, 0.080), skin, "Neck", 28, 18)
    for side, x in (("L", 1), ("R", -1)):
        uv(f"Ear.{side}", (0.132 * x, 0.002, 1.455), (0.020, 0.012, 0.034), skin, "Head", 24, 16)
        # Layered flattened ellipsoids create crisp anime eyes without textures.
        uv(f"EyeLine.{side}", (0.046 * x, -0.1054, 1.475), (0.039, 0.0045, 0.019), pupil, "Head", 36, 20)
        uv(f"EyeWhite.{side}", (0.046 * x, -0.1098, 1.475), (0.0365, 0.0032, 0.0165), white, "Head", 36, 20)
        uv(f"Iris.{side}", (0.046 * x, -0.1132, 1.474), (0.0115, 0.0023, 0.0125), amber, "Head", 28, 18)
        uv(f"Pupil.{side}", (0.046 * x, -0.1157, 1.474), (0.0045, 0.0015, 0.0085), pupil, "Head", 20, 12)
        uv(f"EyeSpark.{side}", (0.042 * x, -0.1174, 1.479), (0.0032, 0.0009, 0.0036), white, "Head", 14, 8)
        base.curve_tube(f"UpperLash.{side}", [
            (0.012 * x, -0.116, 1.484), (0.045 * x, -0.119, 1.493), (0.083 * x, -0.114, 1.484)
        ], 0.0042, pupil, "Head")
        base.curve_tube(f"Brow.{side}", [
            (0.018 * x, -0.106, 1.515), (0.048 * x, -0.110, 1.521), (0.080 * x, -0.104, 1.516)
        ], 0.0035, m["hair_shadow"], "Head")
    uv("NoseTip", (0, -0.108, 1.438), (0.008, 0.006, 0.010), skin, "Head", 18, 10)
    base.curve_tube("Mouth", [(-0.015, -0.108, 1.407), (0, -0.110, 1.406), (0.015, -0.108, 1.407)],
                    0.0020, m["lip"], "Head")


def build_body(m):
    skin, white, black = m["skin"], m["white"], m["black"]
    # Rounded torso under the open coat.
    uv("TorsoUnderTop", (0, 0.005, 1.085), (0.126, 0.078, 0.215), white, "Spine", 40, 26)
    uv("ChestContour", (0, -0.054, 1.170), (0.108, 0.040, 0.100), white, "Chest", 32, 20)
    coat_shell(m["red"], m["red_dark"])
    pleated_skirt(m["orange"], m["orange_dark"])

    # Obi is a rounded waist band, with a real central knot and curved hanging tails.
    rounded_box("ObiBand", (0, -0.003, 0.925), (0.174, 0.088, 0.040), black, "Pelvis", bevel=0.018)
    uv("ObiKnot", (0, -0.101, 0.918), (0.048, 0.024, 0.043), black, "Pelvis", 24, 14)
    ribbon_mesh("ObiTail.L", [(-0.018, -0.105, 0.895), (-0.035, -0.108, 0.825), (-0.044, -0.105, 0.742)],
                [0.025, 0.022, 0.015], 0.009, black, "Pelvis", outward=(0, -1, 0), bevel=0.003)
    ribbon_mesh("ObiTail.R", [(0.018, -0.105, 0.895), (0.040, -0.108, 0.815), (0.056, -0.104, 0.755)],
                [0.025, 0.022, 0.014], 0.009, black, "Pelvis", outward=(0, -1, 0), bevel=0.003)

    for side, x in (("L", 1), ("R", -1)):
        # Legs are five-ring sweeps with knee/calf shaping rather than cones.
        sweep_mesh(f"UpperLeg.{side}", [(0.083*x, 0, 0.815), (0.090*x, 0, 0.720),
                                         (0.094*x, 0, 0.610), (0.098*x, 0, 0.500)],
                   [0.061, 0.057, 0.052, 0.048], skin, f"Thigh.{side}", 24, 0.84)
        sweep_mesh(f"LowerLeg.{side}", [(0.098*x, 0, 0.505), (0.101*x, 0, 0.420),
                                         (0.104*x, 0, 0.285), (0.105*x, 0, 0.145)],
                   [0.050, 0.056, 0.043, 0.039], skin, f"Shin.{side}", 24, 0.84)
        sweep_mesh(f"Sock.{side}", [(0.105*x, 0, 0.205), (0.105*x, 0, 0.125)],
                   [0.046, 0.049], black, f"Shin.{side}", 22, 0.86)
        uv(f"BootUpper.{side}", (0.105*x, -0.055, 0.082), (0.066, 0.118, 0.058), black, f"Foot.{side}", 32, 18)
        rounded_box(f"BootSole.{side}", (0.105*x, -0.058, 0.019), (0.068, 0.122, 0.012), m["sole"], f"Foot.{side}", bevel=0.009)
        for lace in range(3):
            z = 0.091 + lace * 0.014
            base.curve_tube(f"BootLace.{side}.{lace}", [(0.065*x, -0.158, z), (0.105*x, -0.166, z+0.004), (0.145*x, -0.158, z)],
                            0.003, m["lace"], f"Foot.{side}")

        # The broad haori completely covers the upper arm.  Skin starts at the
        # forearm below; omitting hidden shoulder geometry prevents pale seams
        # from appearing between the torso cap and sleeve.
        sweep_mesh(f"ForearmSkin.{side}", [(0.320*x, 0, 1.040), (0.355*x, 0, 0.970),
                                            (0.390*x, 0, 0.885), (0.415*x, 0, 0.815)],
                   [0.044, 0.043, 0.038, 0.034], skin, f"Forearm.{side}", 22, 0.85)
        sweep_mesh(f"HaoriSleeve.{side}", [(0.140*x, 0.020, 1.225), (0.175*x, 0.021, 1.190),
                                            (0.215*x, 0.021, 1.145), (0.260*x, 0.020, 1.085),
                                            (0.300*x, 0.018, 1.015), (0.340*x, 0.014, 0.925)],
                   [0.064, 0.077, 0.096, 0.116, 0.130, 0.120], m["red"], f"UpperArm.{side}", 28, 0.72)
        cloth_panel(f"HaoriShoulder.{side}",
                    [(0.058*x, -0.108, 1.278), (0.158*x, -0.106, 1.258),
                     (0.245*x, -0.098, 1.135), (0.095*x, -0.112, 1.145)],
                    0.010, m["red"], "Chest", bevel=0.004)
        rounded_box(f"Palm.{side}", (0.424*x, -0.004, 0.792), (0.034, 0.024, 0.043), skin, f"Hand.{side}", bevel=0.014)
        sweep_mesh(f"GloveCuff.{side}", [(0.404*x, 0, 0.855), (0.420*x, 0, 0.815)],
                   [0.052, 0.048], black, f"Hand.{side}", 20, 0.80)
        sweep_mesh(f"Thumb.{side}", [(0.401*x, -0.010, 0.795), (0.393*x, -0.017, 0.765)],
                   [0.008, 0.005], skin, f"Hand.{side}", 10, 0.85)
        for finger in range(4):
            offset = (finger - 1.5) * 0.013
            px = (0.420 + offset) * x
            sweep_mesh(f"Finger.{side}.{finger}", [(px, -0.014, 0.780), (px + 0.006*x, -0.018, 0.745)],
                       [0.0065, 0.0045], skin, f"Hand.{side}", 10, 0.85)


def blade_mesh(mat, edge):
    # Slightly curved katana blade with a brighter cutting edge and tapered kissaki.
    centers = [Vector((0.210, 0.027, 0.835)), Vector((0.245, 0.027, 0.700)),
               Vector((0.286, 0.028, 0.555)), Vector((0.325, 0.030, 0.410))]
    widths = [0.015, 0.014, 0.012, 0.001]
    verts = []
    for c, w in zip(centers, widths):
        verts.extend([(c.x-w, c.y-0.003, c.z), (c.x+w, c.y-0.003, c.z),
                      (c.x-w, c.y+0.003, c.z), (c.x+w, c.y+0.003, c.z)])
    faces = []
    for i in range(len(centers)-1):
        a = i*4; b = (i+1)*4
        faces.extend([(a,b,b+1,a+1),(a+2,a+3,b+3,b+2),(a,a+2,b+2,b),(a+1,b+1,b+3,a+3)])
    mesh = bpy.data.meshes.new("KatanaBladeMesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("KatanaBlade", mesh)
    bpy.context.collection.objects.link(obj)
    finish(obj, mat, "Weapon", smooth=True, bevel=0.001)
    ribbon_mesh("KatanaEdge", [(0.195, 0.022, 0.835), (0.230, 0.022, 0.700), (0.274, 0.023, 0.555), (0.324, 0.025, 0.410)],
                [0.003, 0.003, 0.0025, 0.0005], 0.002, edge, "Weapon", outward=(0,-1,0), bevel=0.0005)


def build_weapon(m):
    # Sheath follows the same gentle arc as the blade and remains a separate prop.
    sweep_mesh("Sheath", [(0.205, 0.090, 0.850), (0.235, 0.090, 0.720),
                           (0.275, 0.091, 0.565), (0.325, 0.093, 0.380)],
               [0.029, 0.028, 0.026, 0.021], m["black"], "Sheath", 20, 0.72)
    sweep_mesh("SheathMouth", [(0.197, 0.090, 0.875), (0.210, 0.090, 0.835)],
               [0.035, 0.032], m["steel_dark"], "Sheath", 20, 0.72)
    blade_mesh(m["steel"], m["blade_edge"])
    sweep_mesh("KatanaGrip", [(0.205, -0.050, 0.885), (0.175, -0.052, 0.975), (0.142, -0.052, 1.070)],
               [0.024, 0.023, 0.020], m["black"], "Weapon", 20, 0.82)
    for wrap in range(6):
        t = wrap / 5
        x = 0.198 + (0.150 - 0.198) * t
        z = 0.910 + (1.050 - 0.910) * t
        base.curve_tube(f"GripWrap.{wrap}", [(x-0.018, -0.071, z-0.008), (x, -0.075, z), (x+0.018, -0.071, z+0.008)],
                        0.0035, m["accent"], "Weapon")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.040, minor_radius=0.006,
                                     major_segments=24, minor_segments=10,
                                     location=(0.207, -0.025, 0.872),
                                     rotation=(math.radians(76), 0, math.radians(-14)))
    guard = bpy.context.object
    guard.name = "KatanaGuard"
    finish(guard, m["steel_dark"], "Weapon")


def make_materials():
    mats = {
        "skin": base.material("SkinPorcelain", (1.0, 0.68, 0.55, 1), roughness=0.56),
        "hair": base.material("HairSkyBlue", (0.018, 0.23, 0.68, 1), metallic=0.01, roughness=0.38),
        "hair_shadow": base.material("HairBlueShadow", (0.006, 0.065, 0.28, 1), roughness=0.45),
        "amber": base.material("AmberIris", (1.0, 0.27, 0.005, 1), metallic=0.05, roughness=0.20),
        "pupil": base.material("Ink", (0.012, 0.005, 0.008, 1), roughness=0.32),
        "red": base.material("HaoriCrimson", (0.20, 0.004, 0.025, 1), roughness=0.58),
        "red_dark": base.material("HaoriWineTrim", (0.065, 0.001, 0.008, 1), roughness=0.62),
        "white": base.material("TopWarmWhite", (0.93, 0.92, 0.90, 1), roughness=0.72),
        "orange": base.material("SkirtAmberOrange", (0.62, 0.16, 0.018, 1), roughness=0.62),
        "orange_dark": base.material("PleatWarmShadow", (0.26, 0.030, 0.003, 1), roughness=0.72),
        "black": base.material("TextileBlack", (0.012, 0.016, 0.025, 1), roughness=0.55),
        "sole": base.material("RubberSole", (0.004, 0.006, 0.010, 1), roughness=0.88),
        "lace": base.material("BootLace", (0.10, 0.11, 0.13, 1), roughness=0.72),
        "steel": base.material("KatanaSteel", (0.42, 0.58, 0.70, 1), metallic=0.82, roughness=0.20),
        "steel_dark": base.material("KatanaFittings", (0.055, 0.075, 0.095, 1), metallic=0.72, roughness=0.30),
        "blade_edge": base.material("BladeEdge", (0.78, 0.91, 1.0, 1), metallic=0.90, roughness=0.12),
        "accent": base.material("HandleAccent", (0.56, 0.14, 0.25, 1), roughness=0.50),
        "lip": base.material("LipTint", (0.28, 0.025, 0.035, 1), roughness=0.60),
    }
    amber = mats["amber"].node_tree.nodes.get("Principled BSDF")
    if "Coat Weight" in amber.inputs:
        amber.inputs["Coat Weight"].default_value = 0.45
    return mats


def build_model(m):
    build_body(m)
    build_face(m)
    build_hair(m)
    build_weapon(m)


def dedupe_material_slots(obj):
    materials = list(obj.data.materials)
    unique = []
    remap = {}
    for old_index, mat in enumerate(materials):
        if mat not in unique:
            unique.append(mat)
        remap[old_index] = unique.index(mat)
    polygon_indices = [remap.get(polygon.material_index, 0) for polygon in obj.data.polygons]
    obj.data.materials.clear()
    for mat in unique:
        obj.data.materials.append(mat)
    for polygon, material_index in zip(obj.data.polygons, polygon_indices):
        polygon.material_index = material_index


def join_group(objects, name):
    if not objects:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    active = objects[0]
    bpy.context.view_layer.objects.active = active
    bpy.ops.object.join()
    active.name = name
    active.data.name = name + "Mesh"
    dedupe_material_slots(active)
    return active


def consolidate_for_runtime():
    """Reduce 100+ authored pieces to three skinned render meshes.

    Vertex groups survive Blender's join operation and every source mesh uses
    the same armature, so rigid bone weights and all action behavior are kept.
    Katana and sheath remain separately addressable from the character body.
    """
    body, weapon, sheath = [], [], []
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        groups = {group.name for group in obj.vertex_groups}
        if groups == {"Weapon"}:
            weapon.append(obj)
        elif groups == {"Sheath"}:
            sheath.append(obj)
        else:
            body.append(obj)
    joined = [
        join_group(body, "HeroineBody"),
        join_group(weapon, "HeroineKatana"),
        join_group(sheath, "HeroineSheath"),
    ]
    for obj in joined:
        if obj is not None:
            armature_modifiers = [mod for mod in obj.modifiers if mod.type == "ARMATURE"]
            for duplicate in armature_modifiers[1:]:
                obj.modifiers.remove(duplicate)
            if not armature_modifiers:
                mod = obj.modifiers.new("HeroRig", "ARMATURE")
                mod.object = RIG
                mod.use_deform_preserve_volume = True


def setup_studio(camera_loc, target, output, resolution=(900, 1200), lens=68):
    temporary = []
    ground_mat = base.material("PreviewGround", (0.075, 0.085, 0.105, 1), roughness=0.92)
    bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.003))
    ground = bpy.context.object
    ground.name = "PreviewOnly_Ground"
    ground.data.materials.append(ground_mat)
    temporary.append(ground)

    cam_data = bpy.data.cameras.new("PreviewCamera")
    camera = bpy.data.objects.new("PreviewCamera", cam_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_loc
    cam_data.lens = lens
    base.point_camera(camera, target)
    bpy.context.scene.camera = camera
    temporary.append(camera)

    world = bpy.context.scene.world or bpy.data.worlds.new("PreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.025, 0.035, 0.060, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.32

    def area(name, loc, energy, size, color):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = loc
        base.point_camera(obj, target)
        temporary.append(obj)

    area("PreviewKey", (-2.2, -3.2, 3.8), 720, 3.2, (1.0, 0.78, 0.67))
    area("PreviewFill", (2.6, -2.0, 2.3), 460, 2.8, (0.40, 0.70, 1.0))
    area("PreviewRim", (0.5, 2.8, 3.1), 800, 2.3, (0.25, 0.60, 1.0))

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    bpy.ops.render.render(write_still=True)

    for obj in temporary:
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.materials.remove(ground_mat)


def render_previews():
    PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    setup_studio((1.75, -4.8, 1.70), (0, 0, 0.84), PREVIEW_PATH, (900, 1200), 70)
    setup_studio((0.58, -2.05, 1.57), (0, -0.01, 1.45), FACE_PREVIEW_PATH, (1000, 1000), 82)


def make_manifest(validation=None):
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and not o.name.startswith("PreviewOnly")]
    armatures = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    arm = armatures[0] if armatures else None
    triangles = sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes)
    data = {
        "asset": "heroine-v2",
        "status": "refined-mid-poly-game-model",
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "reference": str(REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "fbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "previews": [str(PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
                     str(FACE_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/")],
        "fbxValidationReport": str(FBX_VALIDATION_PATH.relative_to(ROOT)).replace("\\", "/"),
        "units": "meters",
        "heightMeters": 1.62,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "counts": {
            "objects": len(bpy.data.objects),
            "meshObjects": len(meshes),
            "vertices": sum(len(o.data.vertices) for o in meshes),
            "triangles": triangles,
            "materials": len(bpy.data.materials),
            "armatures": len(armatures),
            "bones": len(arm.data.bones) if arm else 0,
            "actions": len(bpy.data.actions),
        },
        "actions": sorted(a.name for a in bpy.data.actions),
        "requiredActions": ACTION_NAMES,
        "runtimeMeshes": ["HeroineBody", "HeroineKatana", "HeroineSheath"],
        "separateProps": ["HeroineKatana", "HeroineSheath"],
        "validation": validation or {"reopened": False},
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return data


def export_assets():
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    FBX_PATH.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end, scene.render.fps = 1, 48, 30
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene["asset_status"] = "refined-mid-poly-game-model"
    scene["unity_forward"] = "+Z"
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = RIG
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH), use_selection=True,
        object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
        use_space_transform=True, add_leaf_bones=False,
        use_armature_deform_only=False, bake_anim=True,
        bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode="AUTO", embed_textures=False,
    )
    render_previews()
    make_manifest()


def validate_current():
    meshes = [o for o in bpy.data.objects if o.type == "MESH"]
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    actions = sorted(a.name for a in bpy.data.actions)
    missing = sorted(set(ACTION_NAMES) - set(actions))
    vertex_count = sum(len(o.data.vertices) for o in meshes)
    fbx_validation = None
    if FBX_VALIDATION_PATH.exists():
        fbx_validation = json.loads(FBX_VALIDATION_PATH.read_text(encoding="utf-8"))
    validation = {
        "reopened": True,
        "blendReadable": bool(bpy.data.filepath),
        "fbxExists": FBX_PATH.exists(),
        "previewsExist": PREVIEW_PATH.exists() and FACE_PREVIEW_PATH.exists(),
        "meshObjectCount": len(meshes),
        "vertexCount": vertex_count,
        "armatureCount": len(arms),
        "boneCount": len(arms[0].data.bones) if arms else 0,
        "actionCount": len(actions),
        "missingRequiredActions": missing,
        "mobileBudget": 15000 <= vertex_count <= 40000,
        "fbxReimportPassed": bool(fbx_validation and fbx_validation.get("passed")),
    }
    validation["passed"] = (
        len(meshes) > 0 and len(arms) == 1 and validation["boneCount"] >= 20
        and not missing and validation["fbxExists"] and validation["previewsExist"]
        and validation["mobileBudget"] and validation["fbxReimportPassed"]
    )
    make_manifest(validation)
    print("HERO_V2_VALIDATION=" + json.dumps(validation, sort_keys=True))
    if not validation["passed"]:
        raise RuntimeError("Heroine v2 validation failed: " + json.dumps(validation))


def generate():
    global RIG
    base.reset_scene()
    mats = make_materials()
    RIG = base.create_rig()
    base.RIG = RIG
    RIG.name = "HeroineRigV2"
    RIG.data.name = "HeroineRigV2"
    build_model(mats)
    consolidate_for_runtime()
    base.build_actions()
    RIG.animation_data.action = None
    base.set_pose_defaults()
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    export_assets()
    print(f"Generated {BLEND_PATH}")
    print(f"Generated {FBX_PATH}")
    print(f"Generated {MANIFEST_PATH}")


if __name__ == "__main__":
    if "--validate-only" in sys.argv:
        validate_current()
    else:
        generate()
