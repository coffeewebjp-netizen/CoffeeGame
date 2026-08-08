"""Generate CoffeeGAME heroine v4: slender anime production candidate.

Run with Blender 4.5 LTS or newer:
  blender -b --python tools/blender/generate_hero_v4.py

The source scene is Z-up and faces -Y. FBX conversion produces Unity Y-up,
+Z-forward. The established 20 bone names and 15 action names are preserved.
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
import generate_hero_v2 as v2  # noqa: E402
import generate_hero_v3 as v3  # noqa: E402


BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v4.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v4.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v4.json"
VALIDATION_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v4-fbx-validation.json"
REFERENCE_PATH = ROOT / "art" / "3d" / "reference" / "hero-turnaround-v1.png"
FRONT_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v4-front.png"
GAME_PREVIEW_PATH = ROOT / "art" / "3d" / "previews" / "heroine-v4-game-camera.png"
UNITY_V3_REFERENCE = ROOT / "art" / "3d" / "previews" / "unity-combat-v3-steam-desktop.png"

ACTION_NAMES = base.ACTION_NAMES
RIG = None


def make_materials():
    specs = {
        "skin": ("CG_Hero_Skin_Peach_URP", (0.72, 0.43, 0.35, 1), 0.66, "#FFD8CB", "skin"),
        "skin_shadow": ("CG_Hero_Skin_Shadow_URP", (0.34, 0.10, 0.075, 1), 0.68, "#D98C82", "skin-shadow"),
        "hair": ("CG_Hero_Hair_SkyCyan_URP", (0.018, 0.23, 0.56, 1), 0.45, "#77D8FF", "hair-main"),
        "hair_light": ("CG_Hero_Hair_Highlight_URP", (0.065, 0.40, 0.70, 1), 0.38, "#B5EEFF", "hair-highlight"),
        "hair_shadow": ("CG_Hero_Hair_BlueShadow_URP", (0.006, 0.075, 0.23, 1), 0.53, "#3282BD", "hair-shadow"),
        "white": ("CG_Hero_Top_WarmWhite_URP", (0.84, 0.80, 0.74, 1), 0.80, "#FFF8ED", "top"),
        "white_shadow": ("CG_Hero_Top_FoldShadow_URP", (0.30, 0.35, 0.42, 1), 0.75, "#AAB8C7", "top-shadow"),
        "red": ("CG_Hero_Haori_Crimson_URP", (0.25, 0.005, 0.030, 1), 0.68, "#CF3657", "haori-main"),
        "red_light": ("CG_Hero_Haori_Highlight_URP", (0.42, 0.013, 0.058, 1), 0.62, "#E95772", "haori-highlight"),
        "red_dark": ("CG_Hero_Haori_InnerWine_URP", (0.055, 0.001, 0.008, 1), 0.78, "#741D35", "haori-inner"),
        "orange": ("CG_Hero_Skirt_Apricot_URP", (0.52, 0.135, 0.018, 1), 0.73, "#F3A15F", "skirt-main"),
        "orange_light": ("CG_Hero_Skirt_Highlight_URP", (0.70, 0.25, 0.045, 1), 0.70, "#FFC47D", "skirt-highlight"),
        "orange_dark": ("CG_Hero_Skirt_PleatShadow_URP", (0.18, 0.022, 0.003, 1), 0.80, "#B85B36", "skirt-shadow"),
        "black": ("CG_Hero_Obi_Glove_Black_URP", (0.010, 0.015, 0.025, 1), 0.70, "#171C25", "black-cloth"),
        "sole": ("CG_Hero_Boot_Rubber_URP", (0.003, 0.005, 0.009, 1), 0.90, "#0D1118", "boot-rubber"),
        "amber": ("CG_Hero_Eye_Amber_URP", (0.82, 0.12, 0.003, 1), 0.25, "#FF8B18", "iris"),
        "gold": ("CG_Hero_Eye_GoldHighlight_URP", (1.00, 0.52, 0.035, 1), 0.20, "#FFD95A", "iris-highlight"),
        "ink": ("CG_Hero_Face_Ink_URP", (0.008, 0.003, 0.010, 1), 0.48, "#1E1420", "face-ink"),
        "lip": ("CG_Hero_Lip_MutedRose_URP", (0.34, 0.025, 0.045, 1), 0.70, "#B75F68", "lip"),
        "steel": ("CG_Hero_Katana_Steel_URP", (0.50, 0.65, 0.78, 1), 0.18, "#B3D8E8", "blade"),
        "steel_dark": ("CG_Hero_Katana_Fittings_URP", (0.035, 0.055, 0.085, 1), 0.30, "#263849", "metal-dark"),
        "wrap": ("CG_Hero_Katana_Wrap_Rose_URP", (0.36, 0.030, 0.090, 1), 0.64, "#9E3456", "handle-wrap"),
    }
    mats = {}
    for key, (name, color, roughness, hex_color, role) in specs.items():
        metallic = 0.82 if key in {"steel", "steel_dark"} else 0.0
        mats[key] = v3.mark_material(base.material(name, color, metallic=metallic, roughness=roughness), hex_color, role)
    return mats


def create_rig_v4():
    """Same 20-name contract, with a narrower natural neutral silhouette."""
    arm = bpy.data.armatures.new("HeroineRigV4")
    rig = bpy.data.objects.new("HeroineRigV4", arm)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    rig["unity_forward"] = "+Z"
    rig["source_forward"] = "-Y"
    rig["character_height_m"] = 1.64
    rig["contract"] = "CoffeeGAME heroine 20-bone v1-compatible names"
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
    bone("Pelvis", (0, 0, 0.76), (0, 0, 0.90), "Root")
    bone("Spine", (0, 0, 0.88), (0, 0, 1.08), "Pelvis")
    bone("Chest", (0, 0, 1.06), (0, 0, 1.24), "Spine")
    bone("Neck", (0, 0, 1.235), (0, 0, 1.325), "Chest")
    bone("Head", (0, 0, 1.315), (0, 0, 1.575), "Neck")
    for side, sign in (("L", 1), ("R", -1)):
        bone(f"Thigh.{side}", (0.068 * sign, 0, 0.80), (0.073 * sign, 0, 0.48), "Pelvis")
        bone(f"Shin.{side}", (0.073 * sign, 0, 0.48), (0.078 * sign, 0, 0.155), f"Thigh.{side}", True)
        bone(f"Foot.{side}", (0.078 * sign, 0, 0.155), (0.078 * sign, -0.140, 0.075), f"Shin.{side}", True)
        bone(f"UpperArm.{side}", (0.120 * sign, 0, 1.190), (0.190 * sign, 0, 1.035), "Chest")
        bone(f"Forearm.{side}", (0.190 * sign, 0, 1.035), (0.242 * sign, 0, 0.865), f"UpperArm.{side}", True)
        bone(f"Hand.{side}", (0.245 * sign, 0, 0.875), (0.260 * sign, -0.004, 0.785), f"Forearm.{side}", True)
    bone("Weapon", (0.155, -0.015, 0.88), (0.315, -0.015, 1.06), "Pelvis", deform=True)
    bone("Sheath", (0.155, 0.035, 0.83), (0.285, 0.035, 0.35), "Pelvis", deform=True)
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def egg_head(name, center, scale, mat, bone, segments=44, rings=28):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, location=center)
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        x, y, z = vertex.co
        # Slender egg: broad temples, restrained cheek, distinctly tapered jaw.
        if z < 0.05:
            taper = 0.77 + 0.23 * (z + 1.0) / 1.05
        else:
            taper = 1.0 - 0.035 * z
        vertex.co.x = x * scale[0] * taper
        vertex.co.y = y * scale[1] * (0.96 + 0.04 * max(z, 0.0))
        vertex.co.z = z * scale[2]
        if z < -0.45:
            vertex.co.y += 0.006 * (-z - 0.45)
    return v2.finish(obj, mat, bone)


def coat_shell(m):
    rows = [
        (0.705, 0.192, 0.074),
        (0.820, 0.182, 0.072),
        (1.000, 0.160, 0.068),
        (1.165, 0.146, 0.066),
        (1.245, 0.118, 0.058),
    ]
    angular = 30
    gap = 0.43
    vertices = []
    for z, rx, ry in rows:
        for index in range(angular):
            t = index / (angular - 1)
            phi = -math.pi / 2 + gap + (math.tau - 2 * gap) * t
            vertices.append((rx * math.cos(phi), ry * math.sin(phi) + 0.010, z))
    faces = []
    for row in range(len(rows) - 1):
        for index in range(angular - 1):
            start = row * angular + index
            faces.append((start, start + 1, start + 1 + angular, start + angular))
    mesh = bpy.data.meshes.new("HaoriDrapedBackMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("HaoriDrapedBack", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(m["red"])
    obj.data.materials.append(m["red_dark"])
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    solid = obj.modifiers.new("HaoriClothThickness", "SOLIDIFY")
    solid.thickness = 0.008
    solid.offset = 0.0
    solid.material_offset = 1
    v2.apply_modifier(obj, solid.name)
    bevel = obj.modifiers.new("HaoriSoftHem", "BEVEL")
    bevel.width = 0.0035
    bevel.segments = 2
    v2.apply_modifier(obj, bevel.name)
    v2.bind(obj, "Spine")

    # Narrow, curved front quarters instead of v3's shoulder boards.
    for side, sign in (("L", 1), ("R", -1)):
        v2.ribbon_mesh(
            f"HaoriFront.{side}",
            [(0.046 * sign, -0.078, 1.250), (0.070 * sign, -0.086, 1.155),
             (0.092 * sign, -0.091, 0.970), (0.118 * sign, -0.084, 0.730)],
            [0.036, 0.045, 0.052, 0.058], 0.010, m["red"], "Spine",
            outward=(0, -1, 0), bevel=0.003,
        )
        v2.ribbon_mesh(
            f"HaoriLapel.{side}",
            [(0.036 * sign, -0.095, 1.255), (0.050 * sign, -0.100, 1.180),
             (0.067 * sign, -0.103, 1.080), (0.082 * sign, -0.100, 0.930)],
            [0.014, 0.017, 0.016, 0.011], 0.006, m["red_dark"], "Chest",
            outward=(0, -1, 0), bevel=0.002,
        )


def pleated_skirt(m):
    # A darker, longer underlayer peeks below the apricot pleats.
    bpy.ops.mesh.primitive_cone_add(vertices=28, radius1=0.225, radius2=0.135, depth=0.310, location=(0, 0.010, 0.770))
    under = bpy.context.object
    under.name = "SkirtUnderLayer"
    # Elliptical depth keeps this layer behind the front pleats. In v4's first
    # render the circular cone sat in front and became a flat orange apron.
    under.scale.y = 0.42
    v2.finish(under, m["orange_dark"], "Pelvis", smooth=True, bevel=0.002)

    folds = 14
    segments = folds * 2
    rows = [(0.925, 0.145, 0.082), (0.835, 0.178, 0.092), (0.665, 0.238, 0.115)]
    vertices = []
    for row_index, (z, rx, ry) in enumerate(rows):
        for index in range(segments):
            angle = math.tau * index / segments
            fold = (0.009 if index % 2 == 0 else -0.010) * (0.45 + 0.40 * row_index)
            vertices.append((math.sin(angle) * (rx + fold), math.cos(angle) * (ry + fold * 0.62), z))
    faces = []
    for row in range(len(rows) - 1):
        for index in range(segments):
            start = row * segments + index
            faces.append((start, row * segments + (index + 1) % segments,
                          (row + 1) * segments + (index + 1) % segments,
                          (row + 1) * segments + index))
    mesh = bpy.data.meshes.new("ApricotPleatsMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("ApricotPleats", mesh)
    bpy.context.collection.objects.link(obj)
    for mat in (m["orange"], m["orange_light"], m["orange_dark"]):
        obj.data.materials.append(mat)
    for index, polygon in enumerate(obj.data.polygons):
        fold = index % segments
        polygon.material_index = 1 if fold % 7 == 0 else (2 if fold % 2 else 0)
    solid = obj.modifiers.new("PleatCloth", "SOLIDIFY")
    solid.thickness = 0.005
    solid.offset = 0
    v2.apply_modifier(obj, solid.name)
    v2.bind(obj, "Pelvis")


def seamless_leg(side, sign, mat):
    """One continuous skin mesh with blended thigh/shin weights at the knee."""
    rings = [
        (0.795, 0.066, 0.043), (0.690, 0.070, 0.040),
        (0.575, 0.073, 0.037), (0.495, 0.075, 0.035),
        (0.455, 0.076, 0.036), (0.365, 0.078, 0.040),
        (0.245, 0.079, 0.033), (0.135, 0.080, 0.029),
    ]
    segments = 16
    vertices = []
    for z, center_x, radius in rings:
        for segment in range(segments):
            angle = math.tau * segment / segments
            vertices.append((center_x * sign + math.cos(angle) * radius,
                             math.sin(angle) * radius * 0.78, z))
    top_center = len(vertices)
    vertices.append((rings[0][1] * sign, 0, rings[0][0]))
    bottom_center = len(vertices)
    vertices.append((rings[-1][1] * sign, 0, rings[-1][0]))
    faces = []
    for row in range(len(rings) - 1):
        for segment in range(segments):
            nxt = (segment + 1) % segments
            a, b = row * segments + segment, (row + 1) * segments + segment
            faces.append((a, row * segments + nxt, (row + 1) * segments + nxt, b))
    for segment in range(segments):
        nxt = (segment + 1) % segments
        faces.append((top_center, segment, nxt))
        last = (len(rings) - 1) * segments
        faces.append((bottom_center, last + nxt, last + segment))
    mesh = bpy.data.meshes.new(f"LegSkin.{side}Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(f"LegSkin.{side}", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    thigh = obj.vertex_groups.new(name=f"Thigh.{side}")
    shin = obj.vertex_groups.new(name=f"Shin.{side}")
    for row in range(len(rings)):
        ids = list(range(row * segments, (row + 1) * segments))
        if row <= 3:
            thigh.add(ids, 1.0, "REPLACE")
        elif row == 4:
            thigh.add(ids, 0.42, "REPLACE")
            shin.add(ids, 0.58, "REPLACE")
        else:
            shin.add(ids, 1.0, "REPLACE")
    thigh.add([top_center], 1.0, "REPLACE")
    shin.add([bottom_center], 1.0, "REPLACE")
    modifier = obj.modifiers.new("HeroRig", "ARMATURE")
    modifier.object = RIG
    modifier.use_deform_preserve_volume = True
    return obj


def build_body(m):
    # Fitted inner layer, no exaggerated chest sphere.
    v2.sweep_mesh("WhiteInner", [(0, 0.006, 0.930), (0, 0.004, 1.030), (0, 0.002, 1.150), (0, 0.002, 1.245)],
                  [0.100, 0.105, 0.112, 0.090], m["white"], "Spine", 24, 0.64)
    v2.sweep_mesh("InnerCollar", [(0, 0.006, 1.238), (0, 0.008, 1.285)],
                  [0.052, 0.046], m["white"], "Chest", 20, 0.72)
    for index, x in enumerate((-0.042, 0.0, 0.042)):
        base.curve_tube(f"InnerFold.{index}", [(x, -0.069, 1.205), (x * 0.75, -0.071, 1.090), (x * 0.45, -0.067, 0.980)],
                        0.0015, m["white_shadow"], "Spine")
    base.curve_tube("InnerNeckline", [(-0.062, -0.071, 1.235), (0, -0.078, 1.220), (0.062, -0.071, 1.235)],
                    0.0018, m["white_shadow"], "Chest")

    pleated_skirt(m)
    coat_shell(m)
    v2.rounded_box("ObiBand", (0, -0.001, 0.923), (0.145, 0.072, 0.032), m["black"], "Pelvis", bevel=0.012)
    # Soft bow loops and two long tails, matching the illustration's tied sash.
    for side, sign in (("L", 1), ("R", -1)):
        v2.ribbon_mesh(f"ObiLoop.{side}", [(0.010 * sign, -0.083, 0.925), (0.035 * sign, -0.093, 0.940),
                                           (0.056 * sign, -0.086, 0.920), (0.024 * sign, -0.086, 0.905)],
                       [0.012, 0.021, 0.013, 0.006], 0.007, m["black"], "Pelvis", outward=(0, -1, 0), bevel=0.002)
        v2.ribbon_mesh(f"ObiTail.{side}", [(0.015 * sign, -0.088, 0.910), (0.028 * sign, -0.091, 0.825), (0.035 * sign, -0.087, 0.730)],
                       [0.018, 0.017, 0.010], 0.007, m["black"], "Pelvis", outward=(0, -1, 0), bevel=0.002)

    for side, sign in (("L", 1), ("R", -1)):
        # One continuous, weighted leg removes the skin/stocking-looking seam
        # that separate rigid thigh and shin tubes produced in the review.
        seamless_leg(side, sign, m["skin"])
        v2.sweep_mesh(f"Sock.{side}", [(0.080 * sign, 0, 0.158), (0.080 * sign, 0, 0.112)],
                      [0.032, 0.035], m["black"], f"Shin.{side}", 14, 0.76)
        v2.rounded_box(f"BootAnkle.{side}", (0.080 * sign, -0.004, 0.086), (0.035, 0.035, 0.043), m["black"], f"Foot.{side}", bevel=0.014)
        v2.rounded_box(f"BootToe.{side}", (0.080 * sign, -0.064, 0.046), (0.045, 0.065, 0.024), m["black"], f"Foot.{side}", bevel=0.016)
        v2.rounded_box(f"BootSole.{side}", (0.080 * sign, -0.048, 0.013), (0.048, 0.087, 0.008), m["sole"], f"Foot.{side}", bevel=0.006)

        # Kimono sleeve: narrow shoulder, then a low, broad hanging cloth body.
        # The multi-point outline avoids both v3's shoulder board and the first
        # v4 render's rigid conical tube.
        sleeve_points = [
            (0.112 * sign, 1.190), (0.145 * sign, 1.200), (0.184 * sign, 1.135),
            (0.224 * sign, 1.020), (0.278 * sign, 0.915), (0.284 * sign, 0.823),
            (0.207 * sign, 0.806), (0.181 * sign, 0.925), (0.150 * sign, 1.075),
        ]
        v3.solid_panel(f"HaoriSleeve.{side}", sleeve_points, -0.046, 0.036, m["red"], f"UpperArm.{side}", bevel=0.007)
        base.curve_tube(f"SleeveCuff.{side}", [(0.207 * sign, -0.050, 0.810), (0.246 * sign, -0.052, 0.813), (0.284 * sign, -0.049, 0.826)],
                        0.0030, m["red_dark"], f"UpperArm.{side}")
        base.curve_tube(f"SleeveFold.{side}", [(0.142 * sign, -0.049, 1.155), (0.182 * sign, -0.052, 1.055), (0.226 * sign, -0.050, 0.900)],
                        0.0018, m["red_light"], f"UpperArm.{side}")

        # Fine forearms/hands remain close to the body in neutral pose.
        v2.sweep_mesh(f"ForearmSkin.{side}", [(0.195 * sign, 0, 1.025), (0.220 * sign, 0, 0.945), (0.244 * sign, 0, 0.865)],
                      [0.026, 0.024, 0.021], m["skin"], f"Forearm.{side}", 14, 0.75)
        v2.sweep_mesh(f"WristGuard.{side}", [(0.238 * sign, 0, 0.880), (0.250 * sign, 0, 0.830)],
                      [0.025, 0.023], m["black"], f"Hand.{side}", 14, 0.74)
        v2.sweep_mesh(f"HandSkin.{side}", [(0.248 * sign, 0, 0.836), (0.257 * sign, -0.003, 0.800), (0.260 * sign, -0.004, 0.770)],
                      [0.021, 0.018, 0.013], m["skin"], f"Hand.{side}", 12, 0.70)
        for finger in range(4):
            offset = (finger - 1.5) * 0.0075
            px = (0.258 + offset) * sign
            v2.sweep_mesh(f"Finger.{side}.{finger}", [(px, -0.008, 0.782), (px + 0.0025 * sign, -0.011, 0.752)],
                          [0.0036, 0.0025], m["skin"], f"Hand.{side}", 7, 0.80)


def build_face(m):
    egg_head("Head", (0, 0.002, 1.465), (0.118, 0.099, 0.116), m["skin"], "Head")
    v2.sweep_mesh("Neck", [(0, 0.008, 1.282), (0, 0.010, 1.322), (0, 0.007, 1.356)],
                  [0.028, 0.026, 0.029], m["skin"], "Neck", 16, 0.88)
    for side, sign in (("L", 1), ("R", -1)):
        v2.uv(f"Ear.{side}", (0.113 * sign, 0.003, 1.462), (0.012, 0.008, 0.023), m["skin"], "Head", 16, 10)
        tilt = math.radians(2.0 * sign)
        v3.almond(f"EyeInk.{side}", (0.041 * sign, -0.0983, 1.473), 0.0375, 0.0170, 0.0020, m["ink"], "Head", tilt=tilt)
        v3.almond(f"EyeWhite.{side}", (0.041 * sign, -0.1003, 1.473), 0.0342, 0.0137, 0.0017, m["white"], "Head", tilt=tilt)
        v2.uv(f"IrisOuter.{side}", (0.041 * sign, -0.1027, 1.472), (0.0115, 0.0018, 0.0130), m["amber"], "Head", 20, 12)
        v2.uv(f"IrisGold.{side}", (0.041 * sign, -0.1044, 1.469), (0.0075, 0.0011, 0.0070), m["gold"], "Head", 16, 10)
        v2.uv(f"Pupil.{side}", (0.041 * sign, -0.1058, 1.473), (0.0036, 0.0009, 0.0078), m["ink"], "Head", 14, 8)
        v2.uv(f"EyeSpark.{side}", (0.037 * sign, -0.1068, 1.480), (0.0022, 0.0005, 0.0025), m["white"], "Head", 10, 6)
        base.curve_tube(f"UpperLash.{side}", [(0.012 * sign, -0.104, 1.479), (0.041 * sign, -0.107, 1.488), (0.078 * sign, -0.102, 1.478)],
                        0.0017, m["ink"], "Head")
        # Two fine outer lashes give the eye a drawn rather than mechanical edge.
        outer = 0.079 * sign
        base.curve_tube(f"OuterLashA.{side}", [(outer, -0.102, 1.479), ((0.086) * sign, -0.101, 1.483)], 0.0012, m["ink"], "Head")
        base.curve_tube(f"Brow.{side}", [(0.014 * sign, -0.096, 1.506), (0.041 * sign, -0.099, 1.512), (0.070 * sign, -0.095, 1.508)],
                        0.0019, m["hair_shadow"], "Head")
    # Restrained nose and mouth preserve the reference's calm expression.
    v2.uv("NoseShadow", (0, -0.1005, 1.441), (0.0042, 0.0028, 0.0052), m["skin_shadow"], "Head", 12, 7)
    base.curve_tube("Mouth", [(-0.010, -0.101, 1.416), (0, -0.103, 1.414), (0.010, -0.101, 1.416)], 0.0012, m["lip"], "Head")


def hair_shell(m):
    # Open-front, solidified crown: guaranteed top coverage without a full
    # helmet sphere over the face.
    cx, cy, cz = 0.0, 0.018, 1.487
    sx, sy, sz = 0.127, 0.100, 0.137
    segments, rings = 40, 23
    vertices = []
    rows = []
    for ring in range(rings + 1):
        phi = math.pi * 0.78 * ring / rings
        row = []
        for segment in range(segments):
            theta = math.tau * segment / segments
            row.append(len(vertices))
            vertices.append((cx + math.sin(phi) * math.cos(theta) * sx,
                             cy + math.sin(phi) * math.sin(theta) * sy,
                             cz + math.cos(phi) * sz))
        rows.append(row)
    faces = []
    for ring in range(rings):
        for segment in range(segments):
            ids = (rows[ring][segment], rows[ring][(segment + 1) % segments],
                   rows[ring + 1][(segment + 1) % segments], rows[ring + 1][segment])
            avg_y = sum(vertices[index][1] for index in ids) / 4
            avg_z = sum(vertices[index][2] for index in ids) / 4
            # Front -Y opens below the curved hairline; crown remains complete.
            if avg_y < cy - 0.018 and avg_z < cz + 0.030:
                continue
            faces.append(ids)
    mesh = bpy.data.meshes.new("LayeredHairCrownMesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("LayeredHairCrown", mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(m["hair"])
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    solid = obj.modifiers.new("HairShellThickness", "SOLIDIFY")
    solid.thickness = 0.006
    solid.offset = 0.0
    v2.apply_modifier(obj, solid.name)
    bevel = obj.modifiers.new("HairShellSoftEdge", "BEVEL")
    bevel.width = 0.002
    bevel.segments = 2
    v2.apply_modifier(obj, bevel.name)
    v2.bind(obj, "Head")


def build_hair(m):
    hair_shell(m)
    # Broad side/back leaves flow from the crown toward the jaw. Alternating
    # length and a gentle +X bias avoid the radial helmet/combed fringe look.
    for index in range(11):
        theta = math.radians(index * 18)
        x = math.cos(theta) * 0.124
        y = math.sin(theta) * 0.088 + 0.020
        side_flow = 0.014 + 0.010 * math.sin(theta)
        end_z = 1.330 + 0.022 * (index % 3)
        outward = Vector((x, y - 0.018, 0))
        if outward.length < 1e-5:
            outward = Vector((0, 1, 0))
        outward.normalize()
        v2.ribbon_mesh(
            f"BobFlow.{index:02d}",
            [(x * 0.32, y * 0.34, 1.601), (x * 0.65 + side_flow * 0.25, y * 0.66, 1.555),
             (x * 0.92 + side_flow * 0.65, y * 0.94, 1.470), (x * 1.04 + side_flow, y * 1.04, end_z)],
            [0.021, 0.028, 0.026, 0.003], 0.008,
            m["hair_light"] if index in {2, 7} else (m["hair_shadow"] if index in {0, 10} else m["hair"]),
            "Head", outward=tuple(outward), bevel=0.0025,
        )

    # Side-swept front fringe with varied endpoints; no vertical comb teeth.
    xs = (-0.100, -0.072, -0.044, -0.015, 0.016, 0.048, 0.078, 0.105)
    for index, x in enumerate(xs):
        drift = 0.011 + (index - 3.5) * 0.0023
        end_z = [1.512, 1.486, 1.501, 1.478, 1.495, 1.482, 1.505, 1.520][index]
        v2.ribbon_mesh(
            f"FringeFlow.{index:02d}",
            [(x * 0.50, -0.064, 1.604), (x * 0.72 + drift * 0.25, -0.092, 1.570),
             (x + drift * 0.65, -0.104, 1.530), (x + drift, -0.106, end_z)],
            [0.018, 0.021, 0.014, 0.0014], 0.007,
            m["hair_light"] if index in {1, 4} else m["hair"], "Head",
            outward=(0, -1, 0), bevel=0.002,
        )

    for side, sign in (("L", 1), ("R", -1)):
        end_z = 1.320 if side == "L" else 1.348
        end_x = 0.132 if side == "L" else 0.118
        v2.ribbon_mesh(f"CheekLock.{side}", [(0.105 * sign, -0.056, 1.560), (0.125 * sign, -0.077, 1.505),
                                              (0.137 * sign, -0.083, 1.425), (end_x * sign, -0.084, end_z)],
                       [0.019, 0.023, 0.018, 0.003], 0.008, m["hair"], "Head", outward=(0, -1, 0), bevel=0.0025)
    # One broad crown sweep creates the reference's lateral motion and breaks
    # the remaining symmetric cap silhouette.
    v2.ribbon_mesh("CrownSideSweep", [(-0.038, -0.030, 1.614), (0.010, -0.070, 1.600),
                                       (0.074, -0.086, 1.548), (0.124, -0.076, 1.445)],
                   [0.019, 0.024, 0.020, 0.0025], 0.007, m["hair_light"], "Head", outward=(0, -1, 0), bevel=0.002)
    v2.sweep_mesh("Ahoge", [(0.0, 0.015, 1.624), (0.016, 0.006, 1.665), (0.054, 0.004, 1.675), (0.084, 0.016, 1.650)],
                  [0.0050, 0.0041, 0.0028, 0.0010], m["hair_light"], "Head", 9, 1.0)


def build_weapon(m):
    v2.sweep_mesh("Sheath", [(0.175, 0.082, 0.850), (0.205, 0.083, 0.710), (0.246, 0.085, 0.540), (0.292, 0.088, 0.350)],
                  [0.024, 0.023, 0.021, 0.017], m["black"], "Sheath", 14, 0.68)
    v2.sweep_mesh("SheathMouth", [(0.168, 0.082, 0.875), (0.180, 0.082, 0.835)],
                  [0.029, 0.027], m["steel_dark"], "Sheath", 14, 0.70)
    v2.sweep_mesh("KatanaBlade", [(0.175, 0.080, 0.845), (0.207, 0.081, 0.705), (0.248, 0.083, 0.535), (0.290, 0.086, 0.365)],
                  [0.008, 0.007, 0.0055, 0.0008], m["steel"], "Weapon", 10, 0.32)
    v2.sweep_mesh("KatanaGrip", [(0.174, -0.042, 0.885), (0.148, -0.045, 0.965), (0.120, -0.046, 1.050)],
                  [0.018, 0.017, 0.015], m["black"], "Weapon", 14, 0.76)
    for wrap in range(5):
        t = wrap / 4
        x = 0.167 + (0.126 - 0.167) * t
        z = 0.906 + (1.035 - 0.906) * t
        base.curve_tube(f"GripWrap.{wrap}", [(x - 0.012, -0.058, z - 0.005), (x, -0.062, z), (x + 0.012, -0.058, z + 0.005)],
                        0.0023, m["wrap"], "Weapon")
    bpy.ops.mesh.primitive_torus_add(major_radius=0.030, minor_radius=0.0045, major_segments=18, minor_segments=7,
                                     location=(0.176, -0.020, 0.872), rotation=(math.radians(76), 0, math.radians(-14)))
    guard = bpy.context.object
    guard.name = "KatanaGuard"
    v2.finish(guard, m["steel_dark"], "Weapon")


def build_model(m):
    build_body(m)
    build_face(m)
    build_hair(m)
    build_weapon(m)


def render_previews():
    FRONT_PREVIEW_PATH.parent.mkdir(parents=True, exist_ok=True)
    v3.render_studio(FRONT_PREVIEW_PATH, (0.0, -4.55, 1.18), (0, 0, 0.82), (900, 1200), 80)
    v3.render_studio(GAME_PREVIEW_PATH, (1.72, -3.08, 2.52), (0, 0, 0.80), (1100, 900), 65)


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


def write_manifest(validation=None):
    palette = {}
    for mat in bpy.data.materials:
        if "unity_base_color_srgb" in mat:
            palette[mat.name] = {
                "baseColor": mat["unity_base_color_srgb"],
                "role": mat["coffee_material_role"],
                "shader": mat["unity_shader_hint"],
            }
    data = {
        "schemaVersion": 3,
        "asset": "heroine-v4",
        "status": "slender-anime-production-candidate",
        "generator": str(Path(__file__).relative_to(ROOT)).replace("\\", "/"),
        "reference": str(REFERENCE_PATH.relative_to(ROOT)).replace("\\", "/"),
        "unityV3Comparison": str(UNITY_V3_REFERENCE.relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "fbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
        "previews": {
            "front": str(FRONT_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
            "gameCamera": str(GAME_PREVIEW_PATH.relative_to(ROOT)).replace("\\", "/"),
        },
        "fbxValidationReport": str(VALIDATION_PATH.relative_to(ROOT)).replace("\\", "/"),
        "heightMetersIncludingAhoge": 1.675,
        "visualProportionHeads": 6.7,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "runtimeMeshes": ["HeroineBody", "HeroineKatana", "HeroineSheath"],
        "boneContract": {"count": 20, "compatibleWith": "heroine-v1/v2/v3 bone names"},
        "requiredActions": ACTION_NAMES,
        "counts": counts(),
        "materialPalette": palette,
        "androidBudget": {"triangleMaximum": 40000, "runtimeMeshMaximum": 3},
        "validation": validation or {"fbxReimportPassed": False},
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
    scene["asset_status"] = "slender-anime-production-candidate"
    scene["source_forward"] = "-Y"
    scene["unity_forward"] = "+Z"
    scene["material_contract"] = "Use heroine-v4 manifest materialPalette with URP/Lit"

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
    RIG = create_rig_v4()
    base.RIG = RIG
    v2.RIG = RIG
    v3.RIG = RIG
    build_model(mats)
    normal_report = v3.recalculate_all_outside()
    RIG["outside_normal_qa_parts"] = len(normal_report)
    RIG["outside_normal_qa_passed"] = True
    v2.consolidate_for_runtime()
    base.build_actions()
    RIG.animation_data.action = None
    base.set_pose_defaults()
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    export_assets()
    result = write_manifest()
    if result["counts"]["triangles"] > 40000:
        raise RuntimeError("Heroine v4 exceeds 40k Android triangle budget: " + str(result["counts"]["triangles"]))
    print("HEROINE_V4_GENERATED=" + json.dumps(result, ensure_ascii=False))


if __name__ == "__main__":
    generate()
