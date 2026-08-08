"""Generate CoffeeGAME's heroine v3 production candidate.

Run with Blender 4.5 LTS or newer:
  blender -b --python tools/blender/generate_hero_v3.py

The authored scene is Z-up and faces Blender -Y. FBX export converts that to
Unity Y-up / +Z-forward.  v3 deliberately keeps the established 20-bone and
15-action contract while replacing the v2 silhouette and face from scratch.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import generate_hero as base  # noqa: E402
import generate_hero_v2 as v2  # noqa: E402


BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v3.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v3.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v3.json"
VALIDATION_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v3-fbx-validation.json"
REFERENCE_PATH = ROOT / "art" / "3d" / "reference" / "hero-turnaround-v1.png"
FRONT_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v3-front.png"
GAME_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v3-game-camera.png"

ACTION_NAMES = base.ACTION_NAMES
RIG = None


def signed_mesh_volume(obj):
    """Return signed local-space volume; positive means outward winding."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    volume = 0.0
    for triangle in mesh.loop_triangles:
        a, b, c = (mesh.vertices[index].co for index in triangle.vertices)
        volume += a.dot(b.cross(c)) / 6.0
    return volume


def mesh_is_watertight(obj):
    obj.data.calc_loop_triangles()
    edge_counts = {}
    for triangle in obj.data.loop_triangles:
        ids = list(triangle.vertices)
        for index in range(3):
            edge = tuple(sorted((ids[index], ids[(index + 1) % 3])))
            edge_counts[edge] = edge_counts.get(edge, 0) + 1
    return bool(edge_counts) and all(count == 2 for count in edge_counts.values())


def recalculate_all_outside():
    """Normalize every authored island before meshes are joined/exported.

    v2's procedural sweep/ribbon helpers historically emitted inward winding.
    Blender's studio render showed both sides, while Unity URP backface culling
    removed the crown and near-side cloth. Recalculating on the still-separate,
    closed authored parts makes the result deterministic for FBX/URP.
    """
    report = []
    for obj in [candidate for candidate in bpy.context.scene.objects if candidate.type == "MESH"]:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(obj.data)
        bm.free()
        obj.data.update()
        volume = signed_mesh_volume(obj)
        obj["outside_normals_recalculated"] = True
        obj["signed_volume_after_recalc"] = float(volume)
        watertight = mesh_is_watertight(obj)
        report.append({"name": obj.name, "signedVolume": round(volume, 9), "watertight": watertight})
    negative = [item for item in report if item["watertight"] and item["signedVolume"] < -1e-8]
    if negative:
        raise RuntimeError("Outside-normal QA failed: " + json.dumps(negative))
    return report


def mark_material(mat, hex_color: str, role: str, *, two_sided=False):
    mat["unity_shader_hint"] = "Universal Render Pipeline/Lit"
    mat["unity_base_color_srgb"] = hex_color
    mat["coffee_material_role"] = role
    mat["unity_two_sided"] = bool(two_sided)
    return mat


def make_materials():
    # Values are intentionally brighter than v2.  They survive FBX diffuse
    # conversion and give Unity's URP importer enough headroom under a top light.
    specs = {
        "skin": ("CG_Hero_Skin_Peach_URP", (0.78, 0.50, 0.42, 1), 0.62, "#FFD8CB", "skin"),
        "skin_shadow": ("CG_Hero_Skin_Shadow_URP", (0.42, 0.15, 0.12, 1), 0.65, "#D98C82", "skin-shadow"),
        "hair": ("CG_Hero_Hair_SkyCyan_URP", (0.020, 0.24, 0.58, 1), 0.42, "#77D8FF", "hair-main"),
        "hair_light": ("CG_Hero_Hair_Highlight_URP", (0.070, 0.42, 0.72, 1), 0.34, "#B5EEFF", "hair-highlight"),
        "hair_shadow": ("CG_Hero_Hair_BlueShadow_URP", (0.008, 0.090, 0.26, 1), 0.50, "#3282BD", "hair-shadow"),
        "white": ("CG_Hero_Top_WarmWhite_URP", (0.86, 0.82, 0.76, 1), 0.78, "#FFF8ED", "top"),
        "white_shadow": ("CG_Hero_Top_FoldShadow_URP", (0.48, 0.54, 0.62, 1), 0.72, "#B2BEC9", "top-shadow"),
        "red": ("CG_Hero_Haori_Crimson_URP", (0.26, 0.006, 0.032, 1), 0.66, "#CF3657", "haori-main"),
        "red_light": ("CG_Hero_Haori_Highlight_URP", (0.43, 0.014, 0.060, 1), 0.58, "#E95772", "haori-highlight"),
        "red_dark": ("CG_Hero_Haori_InnerWine_URP", (0.065, 0.0015, 0.010, 1), 0.74, "#741D35", "haori-inner"),
        "orange": ("CG_Hero_Skirt_Apricot_URP", (0.53, 0.14, 0.020, 1), 0.70, "#F3A15F", "skirt-main"),
        "orange_light": ("CG_Hero_Skirt_Highlight_URP", (0.72, 0.26, 0.050, 1), 0.68, "#FFC47D", "skirt-highlight"),
        "orange_dark": ("CG_Hero_Skirt_PleatShadow_URP", (0.20, 0.025, 0.004, 1), 0.76, "#B85B36", "skirt-shadow"),
        "black": ("CG_Hero_Obi_Glove_Black_URP", (0.018, 0.025, 0.040, 1), 0.64, "#171C25", "black-cloth"),
        "sole": ("CG_Hero_Boot_Rubber_URP", (0.006, 0.009, 0.014, 1), 0.88, "#0D1118", "boot-rubber"),
        "amber": ("CG_Hero_Eye_Amber_URP", (1.00, 0.29, 0.008, 1), 0.24, "#FF8B18", "iris"),
        "amber_light": ("CG_Hero_Eye_GoldHighlight_URP", (1.00, 0.86, 0.18, 1), 0.18, "#FFD95A", "iris-highlight"),
        "ink": ("CG_Hero_Face_Ink_URP", (0.012, 0.006, 0.013, 1), 0.45, "#1E1420", "face-ink"),
        "lip": ("CG_Hero_Lip_MutedRose_URP", (0.49, 0.055, 0.095, 1), 0.65, "#B75F68", "lip"),
        "steel": ("CG_Hero_Katana_Steel_URP", (0.57, 0.73, 0.86, 1), 0.18, "#B3D8E8", "blade"),
        "steel_dark": ("CG_Hero_Katana_Fittings_URP", (0.06, 0.09, 0.13, 1), 0.28, "#263849", "metal-dark"),
        "blade_edge": ("CG_Hero_Katana_Edge_URP", (0.88, 0.97, 1.00, 1), 0.10, "#E8FAFF", "blade-edge"),
        "wrap": ("CG_Hero_Katana_Wrap_Rose_URP", (0.54, 0.075, 0.18, 1), 0.60, "#9E3456", "handle-wrap"),
    }
    mats = {}
    for key, (name, color, roughness, hex_color, role) in specs.items():
        metallic = 0.82 if key in {"steel", "steel_dark", "blade_edge"} else 0.0
        mats[key] = mark_material(base.material(name, color, metallic=metallic, roughness=roughness), hex_color, role)
    return mats


def solid_panel(name, xz_points, y_front, y_back, mat, bone, *, bevel=0.003):
    """A closed thin cloth panel, facing -Y, with a controllable silhouette."""
    front = [(x, y_front, z) for x, z in xz_points]
    back = [(x, y_back, z) for x, z in xz_points]
    vertices = front + back
    count = len(xz_points)
    faces = [tuple(range(count)), tuple(range(count, count * 2))[::-1]]
    for index in range(count):
        nxt = (index + 1) % count
        faces.append((index, nxt, count + nxt, count + index))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return v2.finish(obj, mat, bone, smooth=True, bevel=bevel)


def almond(name, center, width, height, depth, mat, bone, *, tilt=0.0):
    """Closed eight-sided anime eye plane; far subtler than a bulging sphere."""
    cx, cy, cz = center
    profile = [
        (-1.0, 0.00), (-0.56, 0.58), (0.0, 0.78), (0.58, 0.52),
        (1.0, 0.00), (0.58, -0.45), (0.0, -0.60), (-0.58, -0.44),
    ]
    cosine, sine = math.cos(tilt), math.sin(tilt)
    points = []
    for px, pz in profile:
        x, z = px * width, pz * height
        points.append((cx + x * cosine - z * sine, cz + x * sine + z * cosine))
    return solid_panel(name, points, cy - depth * 0.5, cy + depth * 0.5, mat, bone, bevel=0.001)


def pleated_skirt(m):
    # Front-facing -Y. Alternating material slots give each fold readable depth
    # without textures and keep the Android draw/texture budget predictable.
    folds = 18
    segments = folds * 2
    rows = [(0.925, 0.155, 0.092), (0.850, 0.190, 0.100), (0.660, 0.270, 0.132)]
    vertices = []
    for row_index, (z, rx, ry) in enumerate(rows):
        for i in range(segments):
            angle = math.tau * i / segments
            fold = (0.010 if i % 2 == 0 else -0.012) * (0.35 + row_index * 0.42)
            vertices.append((math.sin(angle) * (rx + fold), math.cos(angle) * (ry + fold * 0.65), z))
    faces = []
    for row_index in range(len(rows) - 1):
        for i in range(segments):
            a = row_index * segments + i
            faces.append((a, row_index * segments + (i + 1) % segments,
                          (row_index + 1) * segments + (i + 1) % segments,
                          (row_index + 1) * segments + i))
    mesh = bpy.data.meshes.new("FlowingPleatedSkirtMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("FlowingPleatedSkirt", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(m["orange"])
    obj.data.materials.append(m["orange_light"])
    obj.data.materials.append(m["orange_dark"])
    for index, polygon in enumerate(obj.data.polygons):
        fold_index = index % segments
        polygon.material_index = 1 if fold_index % 6 == 0 else (2 if fold_index % 2 else 0)
        polygon.use_smooth = False
    solid = obj.modifiers.new("SkirtClothThickness", "SOLIDIFY")
    solid.thickness = 0.006
    solid.offset = 0.0
    v2.apply_modifier(obj, solid.name)
    v2.bind(obj, "Pelvis")
    return obj


def build_body(m):
    # Slim underlayers: the visible shoulder span is only 0.30 m.
    v2.uv("TopTorso", (0, 0.006, 1.095), (0.116, 0.069, 0.205), m["white"], "Spine", 40, 24)
    v2.uv("TopChestSoftShape", (0, -0.052, 1.165), (0.101, 0.032, 0.085), m["white"], "Chest", 32, 18)
    # Small cloth folds break the plain white cylinder in close views.
    for index, x in enumerate((-0.055, 0.0, 0.055)):
        base.curve_tube(f"TopFold.{index}", [(x, -0.081, 1.195), (x * 0.65, -0.083, 1.095), (x * 0.35, -0.078, 1.005)],
                        0.0022, m["white_shadow"], "Spine")

    pleated_skirt(m)
    v2.rounded_box("ObiBand", (0, -0.002, 0.925), (0.160, 0.082, 0.038), m["black"], "Pelvis", bevel=0.014)
    v2.rounded_box("ObiKnot.L", (-0.020, -0.096, 0.917), (0.031, 0.014, 0.022), m["black"], "Pelvis",
                   rotation=(0, 0, math.radians(-28)), bevel=0.010)
    v2.rounded_box("ObiKnot.R", (0.020, -0.096, 0.917), (0.031, 0.014, 0.022), m["black"], "Pelvis",
                   rotation=(0, 0, math.radians(28)), bevel=0.010)
    for side, x in (("L", 1), ("R", -1)):
        v2.ribbon_mesh(f"ObiTail.{side}", [(0.014 * x, -0.098, 0.900), (0.030 * x, -0.101, 0.820), (0.035 * x, -0.098, 0.720)],
                       [0.022, 0.020, 0.012], 0.008, m["black"], "Pelvis", outward=(0, -1, 0), bevel=0.002)

    # Long haori: a back plane plus narrow front quarters leave the top/skirt
    # visible. This matches the illustration's robe instead of v2's bolero.
    solid_panel("HaoriBack", [(-0.090, 1.280), (0.090, 1.280), (0.175, 1.205), (0.205, 0.735),
                              (-0.205, 0.735), (-0.175, 1.205)],
                0.055, 0.100, m["red"], "Spine", bevel=0.006)
    for side, sign in (("L", 1), ("R", -1)):
        points = [(0.055 * sign, 1.285), (0.150 * sign, 1.250), (0.205 * sign, 0.745),
                  (0.115 * sign, 0.730), (0.095 * sign, 1.030)]
        # Reverse one side so polygon winding remains deterministic.
        if sign < 0:
            points.reverse()
        solid_panel(f"HaoriFront.{side}", points, -0.098, -0.050, m["red"], "Spine", bevel=0.005)
        v2.ribbon_mesh(f"HaoriLapel.{side}", [(0.045 * sign, -0.105, 1.286), (0.067 * sign, -0.108, 1.205),
                                               (0.087 * sign, -0.109, 1.080), (0.105 * sign, -0.104, 0.910)],
                       [0.018, 0.021, 0.020, 0.015], 0.007, m["red_dark"], "Chest", outward=(0, -1, 0), bevel=0.002)

        # Flat, flowing sleeves follow the arm bones but avoid the huge round
        # shoulder silhouette that made v2 look stocky in Unity.
        sleeve = [(0.125 * sign, 1.245), (0.190 * sign, 1.215), (0.385 * sign, 0.955),
                  (0.305 * sign, 0.900), (0.225 * sign, 1.020)]
        if sign < 0:
            sleeve.reverse()
        solid_panel(f"HaoriSleeve.{side}", sleeve, -0.050, 0.045, m["red"], f"UpperArm.{side}", bevel=0.008)
        # A small highlight seam helps the sleeve read at gameplay scale.
        base.curve_tube(f"SleeveFold.{side}", [(0.170 * sign, -0.055, 1.185), (0.245 * sign, -0.057, 1.080), (0.325 * sign, -0.056, 0.955)],
                        0.0028, m["red_light"], f"UpperArm.{side}")

        # Slender legs and smaller shoes establish the requested 6-head figure.
        v2.sweep_mesh(f"UpperLeg.{side}", [(0.078 * sign, 0, 0.800), (0.083 * sign, 0, 0.690),
                                            (0.087 * sign, 0, 0.575), (0.091 * sign, 0, 0.485)],
                      [0.051, 0.047, 0.044, 0.041], m["skin"], f"Thigh.{side}", 20, 0.80)
        v2.sweep_mesh(f"LowerLeg.{side}", [(0.091 * sign, 0, 0.490), (0.094 * sign, 0, 0.385),
                                            (0.097 * sign, 0, 0.255), (0.099 * sign, 0, 0.135)],
                      [0.042, 0.048, 0.039, 0.034], m["skin"], f"Shin.{side}", 20, 0.80)
        v2.sweep_mesh(f"Sock.{side}", [(0.098 * sign, 0, 0.196), (0.099 * sign, 0, 0.118)],
                      [0.040, 0.042], m["black"], f"Shin.{side}", 18, 0.78)
        v2.rounded_box(f"BootAnkle.{side}", (0.099 * sign, -0.002, 0.090), (0.042, 0.042, 0.050), m["black"], f"Foot.{side}", bevel=0.017)
        v2.rounded_box(f"BootToe.{side}", (0.099 * sign, -0.072, 0.052), (0.052, 0.072, 0.028), m["black"], f"Foot.{side}", bevel=0.019)
        v2.rounded_box(f"BootSole.{side}", (0.099 * sign, -0.051, 0.016), (0.056, 0.096, 0.010), m["sole"], f"Foot.{side}", bevel=0.007)

        # Sleeve ends at the wrist; fingerless gloves keep the hands defined.
        v2.sweep_mesh(f"ForearmSkin.{side}", [(0.315 * sign, 0, 1.020), (0.350 * sign, 0, 0.945), (0.397 * sign, 0, 0.842)],
                      [0.034, 0.032, 0.027], m["skin"], f"Forearm.{side}", 18, 0.78)
        v2.sweep_mesh(f"GloveCuff.{side}", [(0.373 * sign, 0, 0.895), (0.402 * sign, 0, 0.825)],
                      [0.037, 0.034], m["black"], f"Hand.{side}", 18, 0.78)
        v2.sweep_mesh(f"GlovePalm.{side}", [(0.402 * sign, 0, 0.825), (0.414 * sign, -0.004, 0.785),
                                              (0.418 * sign, -0.006, 0.752)],
                      [0.027, 0.023, 0.017], m["black"], f"Hand.{side}", 16, 0.72)
        for finger in range(4):
            offset = (finger - 1.5) * 0.010
            px = (0.414 + offset) * sign
            v2.sweep_mesh(f"Finger.{side}.{finger}", [(px, -0.010, 0.760), (px + 0.004 * sign, -0.014, 0.728)],
                          [0.0048, 0.0034], m["skin"], f"Hand.{side}", 8, 0.82)


def build_face(m):
    # A compact 0.26 m face brings the full figure close to six visual heads.
    v2.shaped_head("Head", (0, 0.002, 1.466), (0.132, 0.112, 0.134), m["skin"], "Head", 56, 36)
    v2.uv("Neck", (0, 0.010, 1.312), (0.044, 0.041, 0.068), m["skin"], "Neck", 26, 16)
    for side, sign in (("L", 1), ("R", -1)):
        v2.uv(f"Ear.{side}", (0.130 * sign, 0.004, 1.462), (0.015, 0.010, 0.027), m["skin"], "Head", 20, 12)
        # Slightly tilted almond eyes, thin outline, large amber iris.
        tilt = math.radians(3.0 * sign)
        almond(f"EyeInk.{side}", (0.046 * sign, -0.1108, 1.477), 0.042, 0.0200, 0.0024, m["ink"], "Head", tilt=tilt)
        almond(f"EyeWhite.{side}", (0.046 * sign, -0.1130, 1.477), 0.0380, 0.0163, 0.0020, m["white"], "Head", tilt=tilt)
        v2.uv(f"Iris.{side}", (0.046 * sign, -0.1156, 1.476), (0.0135, 0.0021, 0.0153), m["amber"], "Head", 24, 14)
        v2.uv(f"Pupil.{side}", (0.046 * sign, -0.1178, 1.475), (0.0044, 0.0012, 0.0088), m["ink"], "Head", 16, 10)
        v2.uv(f"EyeGold.{side}", (0.042 * sign, -0.1192, 1.482), (0.0033, 0.0008, 0.0037), m["amber_light"], "Head", 12, 8)
        v2.uv(f"EyeSpark.{side}", (0.043 * sign, -0.1200, 1.485), (0.0020, 0.0006, 0.0023), m["white"], "Head", 10, 6)
        base.curve_tube(f"UpperLash.{side}", [(0.015 * sign, -0.117, 1.482), (0.045 * sign, -0.120, 1.493), (0.085 * sign, -0.115, 1.481)],
                        0.0018, m["ink"], "Head")
        # Calm, slightly assertive brows echo the source illustration.
        base.curve_tube(f"Brow.{side}", [(0.015 * sign, -0.111, 1.514), (0.047 * sign, -0.114, 1.520), (0.079 * sign, -0.108, 1.514)],
                        0.0027, m["hair_shadow"], "Head")
    v2.uv("NoseTip", (0, -0.112, 1.441), (0.0060, 0.0040, 0.0070), m["skin_shadow"], "Head", 14, 8)
    base.curve_tube("Mouth", [(-0.013, -0.113, 1.412), (0, -0.115, 1.410), (0.013, -0.113, 1.412)], 0.0017, m["lip"], "Head")


def build_hair(m):
    # The full rear ellipsoid sits behind the protruding face. Unlike the v2
    # open shell it cannot expose a bald crown from Unity's elevated camera.
    v2.uv("HairFullCoverageCap", (0, 0.024, 1.490), (0.154, 0.121, 0.156), m["hair"], "Head", 52, 34)

    # Back and side bob layers create a light, uneven jaw-length silhouette.
    for index in range(15):
        # Only the side/back half (source +Y) receives long bob leaves. A 360°
        # ring put one lock down the center of v3's face and formed a chin mask.
        theta = math.radians(index * (180 / 14))
        x = math.cos(theta) * 0.135
        y = math.sin(theta) * 0.103 + 0.024
        end_z = 1.335 + 0.030 * (index % 3)
        outward = Vector((x, y - 0.024, 0))
        if outward.length < 1e-5:
            outward = Vector((0, 1, 0))
        outward.normalize()
        v2.ribbon_mesh(
            f"BobLock.{index:02d}",
            [(x * 0.62, y * 0.62, 1.575), (x * 0.88, y * 0.88, 1.520),
             (x * 1.06, y * 1.05, 1.435), (x * 1.10, y * 1.08, end_z)],
            [0.020, 0.026, 0.025, 0.003], 0.010,
            m["hair_shadow"] if index % 5 == 0 else m["hair"], "Head",
            outward=tuple(outward), bevel=0.003,
        )

    # Nine overlapping tapered bangs hide the cap/face boundary completely.
    bang_x = (-0.105, -0.080, -0.055, -0.028, 0.0, 0.028, 0.055, 0.080, 0.105)
    for index, x in enumerate(bang_x):
        drift = (index - 4) * 0.0025
        end_z = 1.495 + (0.020 if index in {0, 8} else 0.0) + (0.012 if index in {3, 5} else 0.0)
        v2.ribbon_mesh(
            f"Fringe.{index:02d}",
            [(x * 0.58, -0.087, 1.607), (x * 0.78, -0.113, 1.570),
             (x + drift, -0.120, 1.530), (x + drift * 1.3, -0.121, end_z)],
            [0.018, 0.021, 0.016, 0.0025], 0.008,
            m["hair_light"] if index in {2, 5} else m["hair"], "Head",
            outward=(0, -1, 0), bevel=0.0025,
        )

    for side, sign in (("L", 1), ("R", -1)):
        v2.ribbon_mesh(f"CheekLock.{side}", [(0.112 * sign, -0.070, 1.560), (0.135 * sign, -0.090, 1.505),
                                              (0.143 * sign, -0.094, 1.430), (0.125 * sign, -0.094, 1.360)],
                       [0.021, 0.026, 0.021, 0.003], 0.009, m["hair"], "Head", outward=(0, -1, 0), bevel=0.003)
    v2.sweep_mesh("Ahoge", [(0, 0.016, 1.638), (0.018, 0.006, 1.681), (0.062, 0.004, 1.690), (0.094, 0.018, 1.661)],
                  [0.0060, 0.0050, 0.0036, 0.0014], m["hair_light"], "Head", sides=10, depth_ratio=1.0)


def build_weapon(m):
    # The blade is fully nested inside the sheath at rest. Animations continue
    # to move the independent Weapon bone exactly as in v1/v2.
    v2.sweep_mesh("Sheath", [(0.205, 0.090, 0.855), (0.236, 0.091, 0.720), (0.278, 0.093, 0.555), (0.326, 0.096, 0.365)],
                  [0.027, 0.026, 0.024, 0.019], m["black"], "Sheath", 18, 0.70)
    v2.sweep_mesh("SheathMouth", [(0.199, 0.090, 0.880), (0.209, 0.090, 0.838)],
                  [0.033, 0.030], m["steel_dark"], "Sheath", 18, 0.72)
    # Slender blade centerline remains inside the larger sheath in idle.
    v2.sweep_mesh("KatanaBlade", [(0.205, 0.088, 0.850), (0.238, 0.089, 0.715), (0.280, 0.091, 0.550), (0.324, 0.094, 0.380)],
                  [0.010, 0.009, 0.007, 0.001], m["steel"], "Weapon", 12, 0.35)
    v2.sweep_mesh("KatanaGrip", [(0.203, -0.046, 0.890), (0.174, -0.049, 0.980), (0.143, -0.050, 1.072)],
                  [0.020, 0.019, 0.017], m["black"], "Weapon", 16, 0.78)
    for wrap in range(6):
        t = wrap / 5
        x = 0.195 + (0.150 - 0.195) * t
        z = 0.914 + (1.050 - 0.914) * t
        base.curve_tube(f"GripWrap.{wrap}", [(x - 0.014, -0.066, z - 0.006), (x, -0.070, z), (x + 0.014, -0.066, z + 0.006)],
                        0.0028, m["wrap"], "Weapon")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.035, minor_radius=0.0055, major_segments=22, minor_segments=8,
                                     location=(0.205, -0.023, 0.877), rotation=(math.radians(76), 0, math.radians(-14)))
    guard = bpy.context.object
    guard.name = "KatanaGuard"
    v2.finish(guard, m["steel_dark"], "Weapon")


def build_model(m):
    build_body(m)
    build_face(m)
    build_hair(m)
    build_weapon(m)


def point_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def render_studio(output, camera_location, target, resolution, lens):
    helpers = []
    floor_mat = base.material("PreviewOnly_NeutralFloor", (0.115, 0.130, 0.155, 1), roughness=0.92)
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.004))
    floor = bpy.context.object
    floor.name = "PreviewOnly_Floor"
    floor.data.materials.append(floor_mat)
    helpers.append(floor)

    camera_data = bpy.data.cameras.new("PreviewOnly_Camera")
    camera = bpy.data.objects.new("PreviewOnly_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_location
    camera.data.lens = lens
    camera.data.sensor_width = 36
    point_at(camera, target)
    bpy.context.scene.camera = camera
    helpers.append(camera)

    world = bpy.context.scene.world or bpy.data.worlds.new("HeroV3PreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.055, 0.070, 0.095, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.48

    lights = (
        ("PreviewOnly_Key", (-2.0, -3.0, 3.4), 680, 2.8, (1.0, 0.82, 0.74)),
        ("PreviewOnly_Fill", (2.4, -2.0, 2.4), 520, 2.6, (0.50, 0.78, 1.0)),
        ("PreviewOnly_Rim", (0.5, 2.4, 3.0), 760, 2.0, (0.38, 0.72, 1.0)),
    )
    for name, location, energy, size, color in lights:
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        light = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        point_at(light, target)
        helpers.append(light)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    scene.frame_set(1)
    bpy.ops.render.render(write_still=True)

    scene.camera = None
    for helper in helpers:
        bpy.data.objects.remove(helper, do_unlink=True)
    bpy.data.materials.remove(floor_mat)


def render_previews():
    FRONT_PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    render_studio(FRONT_PREVIEW_PATH, (0.0, -4.45, 1.22), (0, 0, 0.83), (900, 1200), 78)
    # Deliberately close to the elevated three-quarter gameplay view; this is
    # the crown/shoulder regression image that v2 was missing.
    render_studio(GAME_PREVIEW_PATH, (1.70, -3.05, 2.55), (0, 0, 0.80), (1100, 900), 64)


def scene_counts():
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    return {
        "objects": len(bpy.context.scene.objects),
        "meshObjects": len(meshes),
        "vertices": sum(len(obj.data.vertices) for obj in meshes),
        "triangles": sum(sum(len(poly.vertices) - 2 for poly in obj.data.polygons) for obj in meshes),
        "materials": len({mat for obj in meshes for mat in obj.data.materials if mat}),
        "armatures": len(rigs),
        "bones": len(rigs[0].data.bones) if rigs else 0,
        "actions": len(bpy.data.actions),
        "actionNames": sorted(action.name.split("|")[-1] for action in bpy.data.actions),
    }


def write_manifest(validation=None):
    counts = scene_counts()
    palette = {}
    for mat in bpy.data.materials:
        if "unity_base_color_srgb" in mat:
            palette[mat.name] = {
                "baseColor": mat["unity_base_color_srgb"],
                "role": mat["coffee_material_role"],
                "shader": mat["unity_shader_hint"],
            }
    data = {
        "schemaVersion": 2,
        "asset": "heroine-v3",
        "status": "production-candidate-mobile-mid-poly",
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "reference": str(REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "fbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "previews": {
            "front": str(FRONT_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
            "gameCamera": str(GAME_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
        },
        "fbxValidationReport": str(VALIDATION_PATH.relative_to(ROOT)).replace("\\", "/"),
        "units": "meters",
        "heightMetersIncludingAhoge": 1.69,
        "visualProportionHeads": 6.1,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "runtimeMeshes": ["HeroineBody", "HeroineKatana", "HeroineSheath"],
        "separateProps": ["HeroineKatana", "HeroineSheath"],
        "requiredActions": ACTION_NAMES,
        "counts": counts,
        "materialPalette": palette,
        "mobileBudget": {"targetVerticesMax": 40000, "targetTrianglesMax": 80000},
        "validation": validation or {"reopened": False, "fbxReimportPassed": False},
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
    scene["asset_status"] = "production-candidate-mobile-mid-poly"
    scene["source_forward"] = "-Y"
    scene["unity_forward"] = "+Z"
    scene["material_contract"] = "Use manifest materialPalette with URP/Lit"

    render_previews()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
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
    write_manifest()


def generate():
    global RIG
    base.reset_scene()
    mats = make_materials()
    RIG = base.create_rig()
    RIG.name = "HeroineRigV3"
    RIG.data.name = "HeroineRigV3"
    RIG["model_version"] = "heroine-v3"
    RIG["hair_coverage"] = "full crown; face protrudes in front"
    base.RIG = RIG
    v2.RIG = RIG
    build_model(mats)
    normal_report = recalculate_all_outside()
    RIG["outside_normal_qa_parts"] = len(normal_report)
    RIG["outside_normal_qa_passed"] = True
    v2.consolidate_for_runtime()
    base.build_actions()
    RIG.animation_data.action = None
    base.set_pose_defaults()
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    export_assets()
    print("HEROINE_V3_GENERATED=" + json.dumps(write_manifest(), ensure_ascii=False))


if __name__ == "__main__":
    generate()
