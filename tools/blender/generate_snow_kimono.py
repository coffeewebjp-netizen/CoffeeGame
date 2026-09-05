"""Generate the additive Snow Kimono swordswoman trial for CoffeeGAME.

Run with Blender 4.5 LTS:
  blender -b --python tools/blender/generate_snow_kimono.py

Validate the saved source/FBX without regenerating:
  blender -b art/3d/trials/snow-kimono/source/snow-kimono.blend \
    --python tools/blender/generate_snow_kimono.py -- --validate-only

The authored scene is Z-up and faces -Y. FBX conversion produces Unity Y-up,
+Z-forward. The owner-local reference is intentionally neither loaded nor copied.
Unseen side and back details are a conservative first-pass reconstruction.
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import generate_hero as base  # noqa: E402
import generate_hero_v2 as v2  # noqa: E402
import generate_hero_v3 as v3  # noqa: E402


TRIAL = ROOT / "art" / "3d" / "trials" / "snow-kimono"
BLEND_PATH = TRIAL / "source" / "snow-kimono.blend"
FBX_ARCHIVE_PATH = TRIAL / "export" / "snow-kimono.fbx"
UNITY_FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "snow-kimono.fbx"
MANIFEST_PATH = TRIAL / "manifests" / "snow-kimono.json"
VALIDATION_PATH = TRIAL / "manifests" / "snow-kimono-validation.json"
PREVIEWS = TRIAL / "previews"

ACTION_NAMES = [*base.ACTION_NAMES, "Dodge"]
RIG = None


def materials():
    specs = {
        "skin": ("SK_Skin_Porcelain", (0.82, 0.62, 0.57, 1), 0.68, "#F3CDC4", "skin"),
        "skin_warm": ("SK_Skin_WarmShadow", (0.53, 0.30, 0.28, 1), 0.70, "#C58A83", "skin-shadow"),
        "hair": ("SK_Hair_Cobalt", (0.008, 0.075, 0.30, 1), 0.38, "#225EB8", "hair-main"),
        "hair_light": ("SK_Hair_AzureLight", (0.018, 0.17, 0.52, 1), 0.32, "#438DD8", "hair-highlight"),
        "hair_dark": ("SK_Hair_IndigoShadow", (0.003, 0.025, 0.105, 1), 0.49, "#101E4E", "hair-shadow"),
        "eye_white": ("SK_Eye_SoftWhite", (0.92, 0.91, 0.87, 1), 0.50, "#F2F0E9", "eye-white"),
        "amber": ("SK_Eye_Amber", (0.95, 0.25, 0.004, 1), 0.20, "#FF8C19", "iris"),
        "gold": ("SK_Eye_Gold", (1.0, 0.67, 0.045, 1), 0.16, "#FFD24D", "iris-highlight"),
        "ink": ("SK_Face_Ink", (0.008, 0.004, 0.012, 1), 0.52, "#17121D", "face-ink"),
        "lip": ("SK_Lip_DryRose", (0.42, 0.08, 0.095, 1), 0.72, "#A65A64", "lip"),
        "kimono": ("SK_Kimono_BlackSilk", (0.012, 0.016, 0.027, 1), 0.48, "#171B27", "kimono-main"),
        "kimono_light": ("SK_Kimono_FoldLight", (0.034, 0.043, 0.067, 1), 0.54, "#2B3348", "kimono-fold"),
        "kimono_dark": ("SK_Kimono_DeepFold", (0.004, 0.006, 0.012, 1), 0.62, "#0A0D16", "kimono-shadow"),
        "red": ("SK_Trim_Oxblood", (0.42, 0.008, 0.020, 1), 0.45, "#A7192C", "narrow-red-piping"),
        "obi": ("SK_Obi_Satin", (0.020, 0.025, 0.039, 1), 0.34, "#252B3B", "obi"),
        "tabi": ("SK_Tabi_White", (0.78, 0.78, 0.75, 1), 0.78, "#E8E7E1", "tabi"),
        "sole": ("SK_Sandal_Sole", (0.012, 0.009, 0.010, 1), 0.82, "#181317", "sandal-sole"),
        "steel": ("SK_Katana_Steel", (0.50, 0.63, 0.72, 1), 0.17, "#B9D3DE", "blade"),
        "edge": ("SK_Katana_Edge", (0.86, 0.92, 0.94, 1), 0.10, "#EEF8FA", "blade-edge"),
        "fitting": ("SK_Katana_Fittings", (0.055, 0.045, 0.040, 1), 0.25, "#403830", "metal-fitting"),
        "wrap": ("SK_Katana_Wrap", (0.20, 0.018, 0.030, 1), 0.58, "#692A37", "handle-wrap"),
    }
    result = {}
    for key, (name, color, roughness, hex_color, role) in specs.items():
        metallic = 0.82 if key in {"steel", "edge", "fitting"} else 0.0
        result[key] = v3.mark_material(
            base.material(name, color, metallic=metallic, roughness=roughness),
            hex_color,
            role,
        )
    return result


def create_rig():
    """Keep the established 20 names; parent Weapon to the right hand."""
    arm = bpy.data.armatures.new("SnowKimonoRig")
    rig = bpy.data.objects.new("SnowKimonoRig", arm)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    rig["unity_forward"] = "+Z"
    rig["source_forward"] = "-Y"
    rig["character_height_m"] = 1.64
    rig["contract"] = "CoffeeGAME heroine 20-bone compatible names; Weapon follows Hand.R"
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def bone(name, head, tail, parent=None, connected=False, deform=True):
        result = arm.edit_bones.new(name)
        result.head, result.tail = head, tail
        result.use_deform = deform
        if parent:
            result.parent = arm.edit_bones[parent]
            result.use_connect = connected
        return result

    bone("Root", (0, 0, 0), (0, 0, 0.08), deform=False)
    bone("Pelvis", (0, 0, 0.78), (0, 0, 0.91), "Root")
    bone("Spine", (0, 0, 0.90), (0, 0, 1.08), "Pelvis")
    bone("Chest", (0, 0, 1.06), (0, 0, 1.24), "Spine")
    bone("Neck", (0, 0, 1.23), (0, 0, 1.31), "Chest")
    bone("Head", (0, 0, 1.30), (0, 0, 1.59), "Neck")
    for side, sign in (("L", 1), ("R", -1)):
        bone(f"Thigh.{side}", (0.067 * sign, 0, 0.80), (0.072 * sign, 0, 0.48), "Pelvis")
        bone(f"Shin.{side}", (0.072 * sign, 0, 0.48), (0.076 * sign, 0, 0.15), f"Thigh.{side}", True)
        bone(f"Foot.{side}", (0.076 * sign, 0, 0.15), (0.076 * sign, -0.145, 0.075), f"Shin.{side}", True)
        bone(f"UpperArm.{side}", (0.125 * sign, 0, 1.19), (0.200 * sign, 0, 1.02), "Chest")
        bone(f"Forearm.{side}", (0.200 * sign, 0, 1.02), (0.250 * sign, 0, 0.84), f"UpperArm.{side}", True)
        bone(f"Hand.{side}", (0.250 * sign, 0, 0.84), (0.263 * sign, -0.010, 0.755), f"Forearm.{side}", True)
    bone("Weapon", (-0.258, -0.006, 0.815), (-0.277, -0.010, 0.670), "Hand.R")
    bone("Sheath", (0.155, 0.036, 0.84), (0.305, 0.062, 0.32), "Pelvis")
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def ellipse_tube(name, rings, segments, mat, bone, *, alternate=None):
    """Closed elliptical ring loft with optional alternating fold material."""
    vertices = []
    for z, rx, ry, phase in rings:
        for index in range(segments):
            angle = math.tau * index / segments
            fold = phase * (1.0 if index % 2 == 0 else -1.0)
            vertices.append((math.sin(angle) * (rx + fold), math.cos(angle) * (ry + fold * 0.55), z))
    faces = []
    for row in range(len(rings) - 1):
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((row * segments + index, row * segments + nxt,
                          (row + 1) * segments + nxt, (row + 1) * segments + index))
    top = len(vertices)
    vertices.append((0, 0, rings[0][0]))
    bottom = len(vertices)
    vertices.append((0, 0, rings[-1][0]))
    for index in range(segments):
        nxt = (index + 1) % segments
        faces.append((top, nxt, index))
        base_index = (len(rings) - 1) * segments
        faces.append((bottom, base_index + index, base_index + nxt))
    mesh = bpy.data.meshes.new(name + "Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if alternate:
        obj.data.materials.append(alternate)
        for index, polygon in enumerate(obj.data.polygons):
            if index < (len(rings) - 1) * segments:
                polygon.material_index = 1 if index % 6 == 1 else 0
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("SoftClothEdges", "BEVEL")
    bevel.width = 0.002
    bevel.segments = 2
    v2.apply_modifier(obj, bevel.name)
    v2.bind(obj, bone)
    return obj


def two_bone_gradient(obj, first, second, weight_for_second):
    """Replace a rigid helper binding with a smooth two-bone vertex gradient."""
    first_group = obj.vertex_groups.get(first)
    if first_group is None:
        first_group = obj.vertex_groups.new(name=first)
    second_group = obj.vertex_groups.get(second)
    if second_group is None:
        second_group = obj.vertex_groups.new(name=second)
    indices = list(range(len(obj.data.vertices)))
    first_group.remove(indices)
    second_group.remove(indices)
    for vertex in obj.data.vertices:
        second_weight = max(0.0, min(1.0, float(weight_for_second(vertex.co))))
        first_group.add([vertex.index], 1.0 - second_weight, "REPLACE")
        second_group.add([vertex.index], second_weight, "REPLACE")
    return obj


def skirt_weights(obj):
    """Let the lower closed kimono follow each thigh without losing its wrap."""
    pelvis = obj.vertex_groups.get("Pelvis")
    if pelvis is None:
        pelvis = obj.vertex_groups.new(name="Pelvis")
    left = obj.vertex_groups.get("Thigh.L") or obj.vertex_groups.new(name="Thigh.L")
    right = obj.vertex_groups.get("Thigh.R") or obj.vertex_groups.new(name="Thigh.R")
    indices = list(range(len(obj.data.vertices)))
    pelvis.remove(indices)
    for vertex in obj.data.vertices:
        lower = max(0.0, min(0.10, (0.50 - vertex.co.z) * 0.24))
        side = max(-1.0, min(1.0, vertex.co.x / 0.10))
        left_weight = lower * (0.5 + 0.5 * side)
        right_weight = lower - left_weight
        pelvis.add([vertex.index], 1.0 - lower, "REPLACE")
        if left_weight > 0:
            left.add([vertex.index], left_weight, "REPLACE")
        if right_weight > 0:
            right.add([vertex.index], right_weight, "REPLACE")
    return obj


def almond_layers(side, sign, m):
    # Restrained width/height and tilted corners match the sharp mature expression.
    x = 0.050 * sign
    tilt = math.radians(5 * sign)
    v3.almond(f"EyeInk.{side}", (x, -0.1115, 1.472), 0.047, 0.017, 0.004, m["ink"], "Head", tilt=tilt)
    v3.almond(f"EyeWhite.{side}", (x, -0.1150, 1.4715), 0.041, 0.0132, 0.003, m["eye_white"], "Head", tilt=tilt)
    v3.almond(f"Iris.{side}", (x, -0.1172, 1.470), 0.0115, 0.0125, 0.0025, m["amber"], "Head", tilt=0)
    v3.almond(f"Pupil.{side}", (x, -0.1188, 1.470), 0.0034, 0.0085, 0.0015, m["ink"], "Head", tilt=0)
    v2.uv(f"EyeGlint.{side}", (x - 0.003 * sign, -0.1202, 1.475), (0.0026, 0.0013, 0.0026), m["gold"], "Head", 12, 6)
    base.curve_tube(
        f"UpperLash.{side}",
        [(x - 0.043 * sign, -0.121, 1.470), (x, -0.123, 1.485), (x + 0.045 * sign, -0.119, 1.475)],
        0.0023,
        m["ink"],
        "Head",
    )
    base.curve_tube(
        f"Brow.{side}",
        [(x - 0.034 * sign, -0.113, 1.509), (x, -0.118, 1.516), (x + 0.035 * sign, -0.112, 1.511)],
        0.0025,
        m["hair_dark"],
        "Head",
    )


def build_face(m):
    # The hair cap sits behind the face surface; the tapered head stays visible.
    v2.shaped_head("Head", (0, 0, 1.450), (0.122, 0.101, 0.137), m["skin"], "Head", 52, 34)
    v2.uv("Neck", (0, 0.010, 1.290), (0.046, 0.044, 0.080), m["skin"], "Neck", 32, 18)
    for side, sign in (("L", 1), ("R", -1)):
        v2.uv(f"Ear.{side}", (0.127 * sign, 0.004, 1.449), (0.014, 0.010, 0.026), m["skin_warm"], "Head", 20, 12)
        almond_layers(side, sign, m)
    v2.uv("NoseTip", (0, -0.111, 1.432), (0.007, 0.005, 0.009), m["skin_warm"], "Head", 16, 8)
    base.curve_tube("Mouth", [(-0.018, -0.1055, 1.407), (0, -0.1075, 1.408), (0.018, -0.1055, 1.407)], 0.0015, m["lip"], "Head")


def build_hair(m):
    # Smooth under-cap, kept behind the face plane to avoid a helmet mask.
    v2.shaped_head("BobUnderCap", (0, 0.018, 1.495), (0.137, 0.107, 0.154), m["hair"], "Head", 52, 34)
    # Overlapping broad locks give the bob a layered, curved surface.
    for index in range(18):
        theta = math.radians(index * 20)
        if math.cos(theta) < -0.25:
            # The fringe owns the face opening; keep crown/bob layers off cheeks.
            continue
        x = math.sin(theta) * 0.142
        y = math.cos(theta) * 0.113 + 0.017
        outward = Vector((x, y - 0.017, 0))
        if outward.length < 1e-5:
            outward = Vector((0, 1, 0))
        outward.normalize()
        mat = m["hair_light"] if index in {2, 7, 13} else (m["hair_dark"] if index in {5, 10, 16} else m["hair"])
        points = []
        widths = []
        for step in range(10):
            phi = 0.10 + step * (1.48 / 9)
            radial = math.sin(phi)
            points.append((
                math.sin(theta) * 0.142 * radial,
                0.017 + math.cos(theta) * 0.113 * radial,
                1.500 + 0.154 * math.cos(phi),
            ))
            widths.append(0.023 + 0.008 * math.sin(phi))
        points.append((points[-1][0] * 1.015, points[-1][1], 1.338 + 0.010 * (index % 3)))
        widths.append(0.002)
        v2.ribbon_mesh(f"BobLayer.{index:02d}", points, widths, 0.0055, mat, "Head", outward=tuple(outward), bevel=0.002)
    # Fine side-swept fringe, with variable endpoints and overlapping curvature.
    xs = (-0.105, -0.084, -0.062, -0.040, -0.017, 0.006, 0.030, 0.054, 0.078, 0.100)
    tips = (1.500, 1.490, 1.475, 1.482, 1.492, 1.480, 1.486, 1.495, 1.506, 1.518)
    for index, (x, tip) in enumerate(zip(xs, tips)):
        drift = 0.014 + index * 0.0012
        v2.ribbon_mesh(
            f"Fringe.{index:02d}",
            [(x * 0.30, -0.058, 1.620), (x * 0.60 + drift * 0.25, -0.099, 1.578),
             (x + drift * 0.62, -0.112, 1.530), (x + drift, -0.113, tip)],
            [0.012, 0.014, 0.009, 0.0012],
            0.0045,
            m["hair_light"] if index in {1, 5} else m["hair"],
            "Head",
            outward=(0, -1, 0),
            bevel=0.0018,
        )
    for side, sign in (("L", 1), ("R", -1)):
        v2.ribbon_mesh(
            f"CheekLock.{side}",
            [(0.090 * sign, -0.060, 1.584), (0.118 * sign, -0.084, 1.525),
             (0.132 * sign, -0.088, 1.438), (0.124 * sign, -0.083, 1.350)],
            [0.016, 0.021, 0.016, 0.002], 0.0055, m["hair"], "Head", outward=(0, -1, 0), bevel=0.002,
        )


def build_body(m):
    # Tabi and low wooden sandals remain visible below the ankle-length hem.
    for side, sign in (("L", 1), ("R", -1)):
        v2.uv(f"TabiFoot.{side}", (0.075 * sign, -0.060, 0.078), (0.055, 0.110, 0.043), m["tabi"], f"Foot.{side}", 28, 16)
        v2.rounded_box(f"SandalSole.{side}", (0.075 * sign, -0.060, 0.030), (0.061, 0.120, 0.017), m["sole"], f"Foot.{side}", bevel=0.012)
        base.curve_tube(f"SandalStrapA.{side}", [(0.075 * sign, -0.155, 0.093), (0.030 * sign, -0.070, 0.110), (0.075 * sign, 0.015, 0.090)], 0.007, m["red"], f"Foot.{side}")
        base.curve_tube(f"SandalStrapB.{side}", [(0.075 * sign, -0.155, 0.093), (0.120 * sign, -0.070, 0.110), (0.075 * sign, 0.015, 0.090)], 0.007, m["red"], f"Foot.{side}")

    # Subtle torso shape beneath the crossed collar.
    v2.uv("KimonoTorso", (0, 0.006, 1.075), (0.148, 0.082, 0.235), m["kimono"], "Spine", 48, 28)
    v2.uv("KimonoChestDrape", (0, -0.032, 1.145), (0.124, 0.036, 0.103), m["kimono_light"], "Chest", 36, 20)

    # Closed, ankle-length lower kimono: mild flare plus authored fold rhythm.
    long_kimono = ellipse_tube(
        "LongKimono",
        [(0.985, 0.148, 0.083, 0.001), (0.850, 0.166, 0.088, 0.002),
         (0.610, 0.192, 0.096, 0.004), (0.360, 0.220, 0.108, 0.006),
         (0.125, 0.245, 0.120, 0.007)],
        36,
        m["kimono"],
        "Pelvis",
    )
    skirt_weights(long_kimono)
    # A seam that hugs the actual front surface, rather than a floating slab.
    front_piping = base.curve_tube("KimonoFrontPiping", [(-0.055, -0.087, 0.990), (-0.030, -0.094, 0.760),
                                                          (-0.005, -0.108, 0.440), (0.018, -0.123, 0.130)], 0.0021, m["red"], "Pelvis")
    skirt_weights(front_piping)

    # Closed crossed wrap neckline. Each strip crosses the torso and overlaps.
    v2.ribbon_mesh("UnderCollar.L", [(-0.034, -0.079, 1.265), (-0.026, -0.087, 1.215),
                                      (-0.010, -0.094, 1.145), (0.000, -0.096, 1.110)],
                   [0.012, 0.013, 0.011, 0.008], 0.004, m["tabi"], "Chest", outward=(0, -1, 0), bevel=0.0015)
    v2.ribbon_mesh("UnderCollar.R", [(0.034, -0.080, 1.265), (0.026, -0.088, 1.215),
                                      (0.010, -0.095, 1.145), (0.000, -0.097, 1.110)],
                   [0.012, 0.013, 0.011, 0.008], 0.004, m["tabi"], "Chest", outward=(0, -1, 0), bevel=0.0015)
    v2.ribbon_mesh("KimonoCollar.L", [(-0.045, -0.087, 1.255), (-0.035, -0.095, 1.205),
                                       (-0.015, -0.102, 1.145), (0.000, -0.104, 1.105)],
                   [0.020, 0.024, 0.023, 0.018], 0.006, m["kimono_dark"], "Chest", outward=(0, -1, 0), bevel=0.002)
    v2.ribbon_mesh("KimonoCollar.R", [(0.045, -0.092, 1.255), (0.035, -0.100, 1.205),
                                       (0.015, -0.107, 1.145), (0.000, -0.109, 1.105)],
                   [0.020, 0.024, 0.023, 0.018], 0.006, m["kimono"], "Chest", outward=(0, -1, 0), bevel=0.002)
    v2.ribbon_mesh("KimonoOverlap", [(0.000, -0.110, 1.110), (0.018, -0.111, 1.070),
                                      (0.040, -0.109, 1.025), (0.060, -0.104, 0.985)],
                   [0.018, 0.022, 0.021, 0.014], 0.006, m["kimono"], "Chest", outward=(0, -1, 0), bevel=0.002)
    base.curve_tube("CollarPiping.L", [(-0.050, -0.109, 1.252), (-0.036, -0.114, 1.200),
                                        (-0.016, -0.117, 1.145), (-0.002, -0.118, 1.105)], 0.0018, m["red"], "Chest")
    base.curve_tube("CollarPiping.R", [(0.050, -0.114, 1.252), (0.036, -0.119, 1.200),
                                        (0.016, -0.122, 1.145), (0.002, -0.123, 1.105)], 0.0018, m["red"], "Chest")
    base.curve_tube("OverlapPiping", [(0.002, -0.124, 1.105), (0.020, -0.125, 1.065),
                                       (0.040, -0.123, 1.025), (0.057, -0.118, 0.990)], 0.0018, m["red"], "Chest")

    v2.rounded_box("Obi", (0, -0.002, 0.955), (0.174, 0.092, 0.048), m["obi"], "Pelvis", bevel=0.014)
    base.curve_tube("ObiRedCord", [(-0.171, -0.097, 0.952), (0, -0.104, 0.948), (0.171, -0.097, 0.952)], 0.0032, m["red"], "Pelvis")
    # Conservative back reconstruction: compact structured bow and two tails.
    v2.ribbon_mesh("ObiBow.L", [(-0.010, 0.104, 0.970), (0.070, 0.126, 1.000), (0.132, 0.122, 0.975), (0.070, 0.112, 0.940)], [0.025, 0.055, 0.020, 0.008], 0.016, m["obi"], "Pelvis", outward=(0, 1, 0), bevel=0.004)
    v2.ribbon_mesh("ObiBow.R", [(0.010, 0.104, 0.970), (-0.070, 0.126, 1.000), (-0.132, 0.122, 0.975), (-0.070, 0.112, 0.940)], [0.025, 0.055, 0.020, 0.008], 0.016, m["obi"], "Pelvis", outward=(0, 1, 0), bevel=0.004)
    v2.ribbon_mesh("ObiTail.L", [(0.025, 0.115, 0.950), (0.070, 0.126, 0.820), (0.060, 0.122, 0.680)], [0.024, 0.034, 0.010], 0.010, m["obi"], "Pelvis", outward=(0, 1, 0), bevel=0.003)
    v2.ribbon_mesh("ObiTail.R", [(-0.025, 0.115, 0.950), (-0.055, 0.128, 0.815), (-0.080, 0.120, 0.705)], [0.024, 0.032, 0.010], 0.010, m["kimono_dark"], "Pelvis", outward=(0, 1, 0), bevel=0.003)

    # Deep hanging sleeve volumes and visible hands.
    for side, sign in (("L", 1), ("R", -1)):
        sleeve = v2.sweep_mesh(f"KimonoSleeve.{side}", [(0.130 * sign, 0.012, 1.185), (0.170 * sign, 0.014, 1.080),
                                                         (0.214 * sign, 0.010, 0.940), (0.244 * sign, 0.000, 0.825)],
                               [0.062, 0.076, 0.082, 0.053], m["kimono"], f"UpperArm.{side}", 24, 0.62)
        two_bone_gradient(sleeve, f"UpperArm.{side}", f"Forearm.{side}", lambda co: (1.08 - co.z) / 0.25)
        fold = v2.ribbon_mesh(f"SleeveFold.{side}", [(0.145 * sign, -0.052, 1.150), (0.185 * sign, -0.062, 1.040),
                                                      (0.222 * sign, -0.058, 0.915), (0.246 * sign, -0.045, 0.835)],
                              [0.008, 0.010, 0.009, 0.002], 0.004, m["kimono_light"], f"UpperArm.{side}", outward=(0, -1, 0), bevel=0.0015)
        two_bone_gradient(fold, f"UpperArm.{side}", f"Forearm.{side}", lambda co: (1.08 - co.z) / 0.25)
        sleeve_piping = base.curve_tube(f"SleevePiping.{side}", [(0.222 * sign, -0.047, 0.875), (0.246 * sign, -0.050, 0.835),
                                                                 (0.262 * sign, -0.044, 0.808)], 0.0020, m["red"], f"Forearm.{side}")
        v2.uv(f"Hand.{side}", (0.258 * sign, -0.010, 0.780), (0.034, 0.029, 0.060), m["skin"], f"Hand.{side}", 28, 16)


def build_weapon(m):
    # Drawn katana follows Hand.R rigidly; saya remains rigid on the left hip.
    v2.sweep_mesh("KatanaBlade", [(-0.268, -0.012, 0.760), (-0.292, -0.005, 0.535),
                                   (-0.315, 0.010, 0.295), (-0.326, 0.020, 0.095)],
                  [0.0090, 0.0080, 0.0065, 0.0010], m["steel"], "Weapon", 12, 0.30)
    v2.ribbon_mesh("KatanaEdge", [(-0.276, -0.022, 0.750), (-0.300, -0.015, 0.530),
                                   (-0.322, 0.000, 0.290), (-0.331, 0.010, 0.098)],
                   [0.0030, 0.0026, 0.0020, 0.0005], 0.002, m["edge"], "Weapon", outward=(0, -1, 0), bevel=0.0008)
    v2.sweep_mesh("KatanaGrip", [(-0.255, -0.010, 0.790), (-0.238, -0.006, 0.925)], [0.017, 0.014], m["wrap"], "Weapon", 18, 0.78)
    for index in range(5):
        t = index / 4
        z = 0.805 + 0.105 * t
        x = -0.253 + 0.013 * t
        base.curve_tube(f"GripDiamond.{index}", [(x - 0.011, -0.021, z - 0.009), (x, -0.026, z), (x + 0.011, -0.021, z + 0.009)], 0.0018, m["tabi"], "Weapon")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.032, minor_radius=0.0045, major_segments=24, minor_segments=8,
                                     location=(-0.263, -0.010, 0.775), rotation=(math.radians(7), math.radians(2), math.radians(7)))
    guard = bpy.context.object
    guard.name = "KatanaGuard"
    v2.finish(guard, m["fitting"], "Weapon")

    v2.sweep_mesh("Saya", [(0.155, 0.067, 0.842), (0.198, 0.075, 0.690),
                            (0.252, 0.083, 0.500), (0.305, 0.091, 0.318)],
                  [0.023, 0.022, 0.019, 0.013], m["kimono_dark"], "Sheath", 18, 0.72)
    v2.sweep_mesh("SayaMouth", [(0.149, 0.066, 0.865), (0.162, 0.068, 0.824)], [0.028, 0.026], m["fitting"], "Sheath", 18, 0.74)
    base.curve_tube("SayaCord", [(0.160, 0.095, 0.800), (0.105, 0.120, 0.750), (0.145, 0.125, 0.690)], 0.004, m["red"], "Sheath")


def build_model(m):
    build_body(m)
    build_face(m)
    build_hair(m)
    build_weapon(m)


def build_actions():
    base.build_actions()
    base.create_action("Dodge", 24, [
        (1, {"Chest": base.pose(rot=(0, 0, 0)), "Pelvis": base.pose(loc=(0, 0, 0))}),
        (7, {"Pelvis": base.pose(loc=(0, 0.055, -0.055), rot=(0, 0, -20)),
             "Chest": base.pose(rot=(16, 0, 22)), "Head": base.pose(rot=(-7, 0, -12)),
             "Thigh.L": base.pose(rot=(35, 0, -8)), "Thigh.R": base.pose(rot=(-20, 0, 10)),
             "UpperArm.L": base.pose(rot=(-30, 0, -18)), "UpperArm.R": base.pose(rot=(20, 0, 36)),
             "Weapon": base.pose(rot=(10, 18, 38))}),
        (14, {"Pelvis": base.pose(loc=(0, -0.035, -0.020), rot=(0, 0, 18)),
              "Chest": base.pose(rot=(-8, 0, -14)), "Thigh.L": base.pose(rot=(-24, 0, 4)),
              "Thigh.R": base.pose(rot=(30, 0, -5)), "Weapon": base.pose(rot=(-12, -10, -24))}),
        (24, {}),
    ])


def set_action_pose(name, frame):
    base.set_pose_defaults()
    action = bpy.data.actions.get(name)
    if action is None:
        raise RuntimeError(f"Missing preview action: {name}")
    RIG.animation_data_create()
    RIG.animation_data.action = action
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def render_studio(output, camera_location, target=(0, 0, 0.83), resolution=(720, 960), lens=72, ortho=None):
    output.parent.mkdir(parents=True, exist_ok=True)
    helpers = []
    floor_mat = base.material("PreviewOnly_Snow", (0.20, 0.23, 0.28, 1), roughness=0.94)
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.004))
    floor = bpy.context.object
    floor.data.materials.append(floor_mat)
    helpers.append(floor)

    camera_data = bpy.data.cameras.new("PreviewOnly_Camera")
    camera = bpy.data.objects.new("PreviewOnly_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_location
    if ortho:
        camera.data.type = "ORTHO"
        camera.data.ortho_scale = ortho
    else:
        camera.data.lens = lens
    base.point_camera(camera, target)
    bpy.context.scene.camera = camera
    helpers.append(camera)

    world = bpy.context.scene.world or bpy.data.worlds.new("SnowKimonoPreviewWorld")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.050, 0.065, 0.095, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.42
    lights = (
        ("Key", (-2.1, -3.0, 3.6), 610, 3.2, (1.0, 0.78, 0.70)),
        ("Fill", (2.6, -1.9, 2.5), 360, 2.8, (0.42, 0.70, 1.0)),
        ("Rim", (0.5, 2.5, 3.0), 620, 2.2, (0.36, 0.68, 1.0)),
    )
    for suffix, location, energy, size, color in lights:
        data = bpy.data.lights.new("PreviewOnly_" + suffix, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        light = bpy.data.objects.new("PreviewOnly_" + suffix, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        base.point_camera(light, target)
        helpers.append(light)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.view_settings.exposure = -0.55
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.filepath = str(output)
    scene.render.film_transparent = False
    bpy.ops.render.render(write_still=True)
    scene.camera = None
    for helper in helpers:
        bpy.data.objects.remove(helper, do_unlink=True)
    bpy.data.materials.remove(floor_mat)


def render_previews():
    set_action_pose("Idle", 1)
    render_studio(PREVIEWS / "beauty-three-quarter.png", (1.85, -3.75, 1.92), (0, 0, 0.84), (900, 1100), 76)
    render_studio(PREVIEWS / "front.png", (0, -4.0, 0.86), (0, 0, 0.84), (720, 1000), ortho=1.82)
    render_studio(PREVIEWS / "side.png", (4.0, 0, 0.86), (0, 0, 0.84), (720, 1000), ortho=1.82)
    render_studio(PREVIEWS / "back.png", (0, 4.0, 0.86), (0, 0, 0.84), (720, 1000), ortho=1.82)
    set_action_pose("Walk", 1)
    render_studio(PREVIEWS / "walk.png", (1.55, -3.55, 1.55), (0, 0, 0.82), (800, 900), 72)
    set_action_pose("Run", 1)
    render_studio(PREVIEWS / "run.png", (1.55, -3.55, 1.55), (0, 0, 0.82), (800, 900), 72)
    set_action_pose("Sword", 14)
    render_studio(PREVIEWS / "sword.png", (1.65, -3.65, 1.55), (0, 0, 0.82), (800, 900), 72)
    set_action_pose("Dodge", 7)
    render_studio(PREVIEWS / "dodge.png", (1.65, -3.65, 1.55), (0, 0, 0.82), (800, 900), 72)
    set_action_pose("Idle", 1)


def counts():
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


def manifest(validation=None):
    palette = {}
    for mat in bpy.data.materials:
        if "unity_base_color_srgb" in mat:
            palette[mat.name] = {
                "baseColor": mat["unity_base_color_srgb"],
                "role": mat["coffee_material_role"],
                "shader": mat["unity_shader_hint"],
            }
    data = {
        "schemaVersion": 1,
        "asset": "snow-kimono",
        "status": "reference-guided-prototype",
        "fidelityDisclosure": "Front view is reference-guided; unseen side/back are conservative first-pass reconstruction.",
        "reference": "owner-local image; intentionally not included",
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "archivedFbx": str(FBX_ARCHIVE_PATH.relative_to(ROOT)).replace("\\", "/"),
        "unityFbx": str(UNITY_FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "previews": {name: str((PREVIEWS / f"{name}.png").relative_to(ROOT)).replace("\\", "/")
                     for name in ("beauty-three-quarter", "front", "side", "back", "walk", "run", "sword", "dodge")},
        "units": "meters",
        "heightMeters": 1.64,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "locomotion": "in-place",
        "runtimeMeshes": ["SnowKimonoBody", "SnowKimonoKatana", "SnowKimonoSaya"],
        "separateRigidProps": {"katana": "Weapon/Hand.R", "saya": "Sheath/Pelvis"},
        "boneContract": {"count": 20, "compatibleNames": True, "weaponParent": "Hand.R"},
        "requiredActions": ACTION_NAMES,
        "counts": counts(),
        "materialPalette": palette,
        "validation": validation or {"passed": False},
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return data


def export_fbx(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"}:
            obj.select_set(True)
    bpy.context.view_layer.objects.active = RIG
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL", use_space_transform=True,
        add_leaf_bones=False, use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode="AUTO", embed_textures=False,
    )


def validate_saved():
    global RIG
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    RIG = rigs[0] if rigs else None
    action_names = sorted(action.name.split("|")[-1] for action in bpy.data.actions)
    missing = sorted(set(ACTION_NAMES) - set(action_names))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    required_meshes = {"SnowKimonoBody", "SnowKimonoKatana", "SnowKimonoSaya"}
    validation = {
        "blendReadable": bool(bpy.data.filepath),
        "fbxArchiveExists": FBX_ARCHIVE_PATH.exists(),
        "unityFbxExists": UNITY_FBX_PATH.exists(),
        "meshNames": sorted(obj.name for obj in meshes),
        "requiredMeshesPresent": required_meshes.issubset({obj.name for obj in meshes}),
        "armatureCount": len(rigs),
        "boneCount": len(RIG.data.bones) if RIG else 0,
        "actionCount": len(action_names),
        "missingActions": missing,
        "weaponParent": RIG.data.bones["Weapon"].parent.name if RIG and "Weapon" in RIG.data.bones else None,
        "sayaParent": RIG.data.bones["Sheath"].parent.name if RIG and "Sheath" in RIG.data.bones else None,
        "previewsPresent": all((PREVIEWS / f"{name}.png").exists() for name in ("beauty-three-quarter", "front", "side", "back", "walk", "run", "sword", "dodge")),
    }
    validation["passed"] = (
        validation["blendReadable"] and validation["fbxArchiveExists"] and validation["unityFbxExists"]
        and validation["requiredMeshesPresent"] and validation["armatureCount"] == 1
        and validation["boneCount"] == 20 and not missing
        and validation["weaponParent"] == "Hand.R" and validation["sayaParent"] == "Pelvis"
        and validation["previewsPresent"]
    )
    VALIDATION_PATH.parent.mkdir(parents=True, exist_ok=True)
    VALIDATION_PATH.write_text(json.dumps(validation, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    manifest(validation)
    print("SNOW_KIMONO_VALIDATION=" + json.dumps(validation, ensure_ascii=False))
    if not validation["passed"]:
        raise RuntimeError("Snow Kimono validation failed")


def generate():
    global RIG
    base.reset_scene()
    m = materials()
    RIG = create_rig()
    base.RIG = RIG
    v2.RIG = RIG
    v3.RIG = RIG
    build_model(m)
    v3.recalculate_all_outside()
    v2.consolidate_for_runtime()
    bpy.data.objects.get("HeroineBody").name = "SnowKimonoBody"
    bpy.data.objects.get("HeroineKatana").name = "SnowKimonoKatana"
    bpy.data.objects.get("HeroineSheath").name = "SnowKimonoSaya"
    build_actions()
    RIG.animation_data.action = None
    base.set_pose_defaults()
    scene = bpy.context.scene
    scene.frame_start, scene.frame_end, scene.render.fps = 1, 48, 30
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene["asset"] = "snow-kimono"
    scene["asset_status"] = "reference-guided-prototype"
    scene["source_forward"] = "-Y"
    scene["unity_forward"] = "+Z"
    scene["reference_original_included"] = False
    render_previews()
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    export_fbx(FBX_ARCHIVE_PATH)
    export_fbx(UNITY_FBX_PATH)
    result = manifest()
    print("SNOW_KIMONO_GENERATED=" + json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    if "--validate-only" in sys.argv:
        validate_saved()
    else:
        generate()
