"""Prepare the approved Meshy Snow Kimono GLB for CoffeeGAME.

The high-resolution GLB remains the immutable appearance source. This script
creates an isolated textured runtime derivative, rig, sixteen actions, rear obi
bow, katana and saya, then performs an FBX reimport motion check.

Run with Blender 4.5 LTS from the repository root:

  blender -b --python tools/blender/prepare_meshy_snow_kimono.py
"""

from __future__ import annotations

import importlib.util
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
TOOLS = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))
import generate_hero as base  # noqa: E402
import generate_hero_v2 as v2  # noqa: E402


TRIAL = ROOT / "art" / "3d" / "trials" / "meshy-snow-kimono"
SOURCE_GLB = TRIAL / "drop" / "approved-highres.glb"
BLEND_PATH = TRIAL / "source" / "meshy-snow-kimono.blend"
EXPORT_FBX = TRIAL / "export" / "meshy-snow-kimono.fbx"
MANIFEST_PATH = TRIAL / "manifests" / "meshy-snow-kimono.json"
REIMPORT_REPORT = TRIAL / "manifests" / "meshy-snow-kimono-fbx-validation.json"
PREVIEWS = TRIAL / "previews"
UNITY_DIR = (
    ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources"
    / "Models" / "Hero" / "MeshySnowKimono"
)
UNITY_FBX = UNITY_DIR / "meshy-snow-kimono.fbx"
TARGET_HEIGHT = 1.68
TARGET_TRIANGLES = 240_000
ACTION_NAMES = [*base.ACTION_NAMES, "Dodge"]
RIG = None


def reset() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.fps = 30
    world = bpy.data.worlds.new("MeshySnowKimonoWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.035, 0.045, 0.065, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.22
    scene.world = world


def bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def import_and_optimize() -> bpy.types.Object:
    if not SOURCE_GLB.is_file():
        raise FileNotFoundError(SOURCE_GLB)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE_GLB))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one approved Meshy body, found {len(meshes)} meshes")
    body = meshes[0]
    body.name = "MeshySnowKimonoBody"
    body.data.name = "MeshySnowKimonoBody"

    lo, hi = bounds([body])
    scale = TARGET_HEIGHT / max(0.001, hi.z - lo.z)
    body.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    lo, hi = bounds([body])
    body.location.x -= (lo.x + hi.x) * 0.5
    body.location.y -= (lo.y + hi.y) * 0.5
    body.location.z -= lo.z
    bpy.context.view_layer.objects.active = body
    body.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    source_triangles = sum(len(poly.vertices) - 2 for poly in body.data.polygons)
    ratio = min(1.0, TARGET_TRIANGLES / max(1, source_triangles))
    modifier = body.modifiers.new("RuntimeTopology", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    body.select_set(False)
    bpy.context.view_layer.update()
    return body


def create_rig() -> bpy.types.Object:
    armature = bpy.data.armatures.new("MeshySnowKimonoRig")
    rig = bpy.data.objects.new("MeshySnowKimonoRig", armature)
    bpy.context.collection.objects.link(rig)
    rig.show_in_front = True
    rig["unity_forward"] = "+Z"
    rig["source_forward"] = "-Y"
    rig["character_height_m"] = TARGET_HEIGHT
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")

    def add(name, head, tail, parent=None, connected=False, deform=True):
        bone = armature.edit_bones.new(name)
        bone.head = head
        bone.tail = tail
        bone.use_deform = deform
        if parent:
            bone.parent = armature.edit_bones[parent]
            bone.use_connect = connected
        return bone

    add("Root", (0, 0, 0), (0, 0, 0.08), deform=False)
    add("Pelvis", (0, 0, 0.82), (0, 0, 0.96), "Root")
    add("Spine", (0, 0, 0.95), (0, 0, 1.14), "Pelvis")
    add("Chest", (0, 0, 1.13), (0, 0, 1.31), "Spine")
    add("Neck", (0, 0, 1.30), (0, 0, 1.39), "Chest")
    add("Head", (0, 0, 1.38), (0, 0, 1.65), "Neck")
    for side, sign in (("L", 1), ("R", -1)):
        add(f"Thigh.{side}", (0.085 * sign, 0, 0.84), (0.085 * sign, 0, 0.48), "Pelvis")
        add(f"Shin.{side}", (0.085 * sign, 0, 0.48), (0.085 * sign, 0, 0.16), f"Thigh.{side}", True)
        add(f"Foot.{side}", (0.085 * sign, 0, 0.16), (0.085 * sign, -0.14, 0.07), f"Shin.{side}", True)
        add(f"UpperArm.{side}", (0.16 * sign, 0, 1.28), (0.31 * sign, 0, 1.10), "Chest")
        add(f"Forearm.{side}", (0.31 * sign, 0, 1.10), (0.43 * sign, 0, 0.91), f"UpperArm.{side}", True)
        add(f"Hand.{side}", (0.43 * sign, 0, 0.91), (0.47 * sign, -0.01, 0.82), f"Forearm.{side}", True)
    add("Weapon", (-0.45, -0.01, 0.88), (-0.47, -0.02, 0.70), "Hand.R")
    add("Sheath", (0.17, 0.07, 0.92), (0.29, 0.09, 0.36), "Pelvis")
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    return rig


def bind_automatic(body: bpy.types.Object, rig: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    try:
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    except RuntimeError as error:
        print("AUTOMATIC_WEIGHTS_FAILED=" + repr(error))
    weighted = sum(1 for vertex in body.data.vertices if vertex.groups)
    coverage = weighted / max(1, len(body.data.vertices))
    print("AUTOMATIC_WEIGHT_COVERAGE={:.6f}".format(coverage))
    if coverage < 0.999:
        print("USING_COORDINATE_WEIGHT_FALLBACK=coverage-below-0.999")
        for modifier in list(body.modifiers):
            if modifier.type == "ARMATURE":
                body.modifiers.remove(modifier)
        body.vertex_groups.clear()
        bind_coordinate_fallback(body, rig)
    body.parent = rig
    bpy.context.view_layer.update()


def bind_coordinate_fallback(body: bpy.types.Object, rig: bpy.types.Object) -> None:
    """Smooth position-derived fallback; never uses another mesh's vertex data."""
    buckets: dict[str, dict[float, list[int]]] = {
        bone.name: {} for bone in rig.data.bones if bone.use_deform
    }

    def clamp(value: float) -> float:
        return max(0.0, min(1.0, value))

    def vertical_weights(z: float) -> dict[str, float]:
        anchors = (
            (0.00, "Pelvis"), (0.88, "Pelvis"), (1.04, "Spine"),
            (1.22, "Chest"), (1.34, "Neck"), (1.46, "Head"), (1.68, "Head"),
        )
        for index in range(len(anchors) - 1):
            za, first = anchors[index]
            zb, second = anchors[index + 1]
            if za <= z <= zb:
                if first == second:
                    return {first: 1.0}
                t = clamp((z - za) / max(0.001, zb - za))
                return {first: 1.0 - t, second: t}
        return {"Pelvis": 1.0} if z < anchors[0][0] else {"Head": 1.0}

    def arm_weights(ax: float, side: str) -> dict[str, float]:
        t = clamp((ax - 0.16) / (0.47 - 0.16))
        upper = f"UpperArm.{side}"
        forearm = f"Forearm.{side}"
        hand = f"Hand.{side}"
        if t < 0.50:
            blend = clamp((t - 0.30) / 0.20)
            return {upper: 1.0 - blend, forearm: blend}
        blend = clamp((t - 0.66) / 0.24)
        return {forearm: 1.0 - blend, hand: blend}

    def add(weights: dict[str, float], name: str, amount: float) -> None:
        if amount > 1e-5:
            weights[name] = weights.get(name, 0.0) + amount

    for vertex in body.data.vertices:
        x, _, z = vertex.co
        ax = abs(x)
        side = "L" if x >= 0 else "R"
        weights = vertical_weights(z)

        # Blend into the arm chain instead of cutting the connected sleeve mesh
        # at a single threshold. This keeps the large kimono sleeve continuous.
        arm_boundary = 0.15 + max(0.0, 1.12 - z) * 0.18
        armness = clamp((ax - arm_boundary) / 0.065) if 0.76 < z < 1.38 else 0.0
        if armness > 0.0:
            body_weights = {name: weight * (1.0 - armness) for name, weight in weights.items()}
            for name, weight in arm_weights(ax, side).items():
                add(body_weights, name, weight * armness)
            weights = body_weights

        # Only the bottom of each foot is fully foot-driven. A broad transition
        # prevents the connected ankle-length hem from tearing into long spikes.
        footness = clamp((0.18 - z) / 0.08)
        if footness > 0.0:
            weights = {name: weight * (1.0 - footness) for name, weight in weights.items()}
            add(weights, f"Foot.{side}", footness)

        total = sum(weights.values()) or 1.0
        for name, value in weights.items():
            normalized = value / total
            quantized = round(normalized / 0.025) * 0.025
            if quantized <= 0.0:
                continue
            buckets[name].setdefault(quantized, []).append(vertex.index)

    for name, weight_buckets in buckets.items():
        if not weight_buckets:
            continue
        group = body.vertex_groups.new(name=name)
        for weight, indices in weight_buckets.items():
            group.add(indices, weight, "REPLACE")
    modifier = body.modifiers.new("MeshySnowKimonoRig", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = True


def material(name, color, metallic=0.0, roughness=0.5):
    return base.material(name, color, metallic=metallic, roughness=roughness)


def create_bow_and_props(rig: bpy.types.Object) -> list[bpy.types.Object]:
    v2.RIG = rig
    base.RIG = rig
    obi = material("MSK_Obi_BlackCloth", (0.0025, 0.0035, 0.006, 1), roughness=0.76)
    steel = material("MSK_Katana_Steel", (0.46, 0.58, 0.67, 1), metallic=0.86, roughness=0.16)
    edge = material("MSK_Katana_Edge", (0.82, 0.90, 0.95, 1), metallic=0.92, roughness=0.10)
    fitting = material("MSK_Katana_Fitting", (0.07, 0.045, 0.035, 1), metallic=0.62, roughness=0.28)
    wrap = material("MSK_Katana_Wrap", (0.18, 0.012, 0.024, 1), roughness=0.52)

    created = []
    created.append(v2.uv(
        "MeshySnowKimonoObiBow.L",
        (-0.076, 0.125, 0.992), (0.078, 0.023, 0.046), obi, "Pelvis",
        28, 16, rotation=(math.radians(5), math.radians(-12), math.radians(-10)),
    ))
    created.append(v2.uv(
        "MeshySnowKimonoObiBow.R",
        (0.076, 0.125, 0.992), (0.078, 0.023, 0.046), obi, "Pelvis",
        28, 16, rotation=(math.radians(-5), math.radians(12), math.radians(10)),
    ))
    created.append(v2.uv(
        "MeshySnowKimonoObiKnot", (0, 0.142, 0.990), (0.043, 0.028, 0.038),
        obi, "Pelvis", 24, 14,
    ))
    created.append(v2.ribbon_mesh(
        "MeshySnowKimonoObiTail.L",
        [(-0.020, 0.121, 0.970), (-0.040, 0.136, 0.900), (-0.060, 0.138, 0.820), (-0.078, 0.124, 0.710)],
        [0.024, 0.033, 0.030, 0.010], 0.012, obi, "Pelvis", outward=(0, 1, 0), bevel=0.006,
    ))
    created.append(v2.ribbon_mesh(
        "MeshySnowKimonoObiTail.R",
        [(0.020, 0.121, 0.970), (0.040, 0.136, 0.895), (0.060, 0.138, 0.815), (0.080, 0.124, 0.715)],
        [0.024, 0.033, 0.030, 0.010], 0.012, obi, "Pelvis", outward=(0, 1, 0), bevel=0.006,
    ))

    created.append(v2.sweep_mesh(
        "MeshySnowKimonoKatana",
        [(-0.46, -0.02, 0.86), (-0.50, -0.01, 0.62), (-0.53, 0.01, 0.36), (-0.54, 0.02, 0.16)],
        [0.012, 0.010, 0.007, 0.0015], steel, "Weapon", 14, 0.24,
    ))
    created.append(v2.ribbon_mesh(
        "MeshySnowKimonoKatanaEdge",
        [(-0.468, -0.032, 0.855), (-0.508, -0.022, 0.615), (-0.538, -0.002, 0.355), (-0.545, 0.008, 0.16)],
        [0.0035, 0.0030, 0.0022, 0.0004], 0.002, edge, "Weapon", outward=(0, -1, 0), bevel=0.0008,
    ))
    created.append(v2.sweep_mesh(
        "MeshySnowKimonoKatanaGrip",
        [(-0.470, -0.015, 0.81), (-0.425, -0.015, 1.04)],
        [0.019, 0.015], wrap, "Weapon", 18, 0.78,
    ))
    bpy.ops.mesh.primitive_torus_add(
        major_radius=0.036, minor_radius=0.005, major_segments=24, minor_segments=8,
        location=(-0.46, -0.018, 0.875), rotation=(math.radians(7), 0, math.radians(7)),
    )
    guard = bpy.context.object
    guard.name = "MeshySnowKimonoKatanaGuard"
    v2.finish(guard, fitting, "Weapon")
    created.append(guard)

    created.append(v2.sweep_mesh(
        "MeshySnowKimonoSaya",
        [(0.17, 0.10, 0.92), (0.20, 0.11, 0.74), (0.25, 0.12, 0.54), (0.30, 0.13, 0.34)],
        [0.025, 0.023, 0.019, 0.013], obi, "Sheath", 18, 0.72,
    ))
    created.append(v2.sweep_mesh(
        "MeshySnowKimonoSayaMouth",
        [(0.164, 0.098, 0.945), (0.177, 0.102, 0.895)],
        [0.030, 0.027], fitting, "Sheath", 18, 0.74,
    ))
    return created


def build_actions(rig: bpy.types.Object) -> None:
    base.RIG = rig
    base.build_actions()
    # Replace the generator's near-rest A-pose with a relaxed authored Idle.
    bpy.data.actions.remove(bpy.data.actions["Idle"])
    relaxed_arms = {
        "UpperArm.L": base.pose(rot=(0, -6, 12)),
        "UpperArm.R": base.pose(rot=(0, 6, -12)),
    }
    base.create_action("Idle", 48, [
        (1, {**relaxed_arms, "Chest": base.pose(loc=(0, 0, 0)), "Head": base.pose(rot=(0, 0, 0))}),
        (24, {**relaxed_arms, "Chest": base.pose(loc=(0, 0, 0.008)), "Head": base.pose(rot=(1, 0, 1))}),
        (48, {**relaxed_arms, "Chest": base.pose(loc=(0, 0, 0)), "Head": base.pose(rot=(0, 0, 0))}),
    ], True)
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
    missing = sorted(set(ACTION_NAMES) - {action.name for action in bpy.data.actions})
    if missing:
        raise RuntimeError("Missing actions: " + ", ".join(missing))
    bake_complete_actions(rig)


def clear_pose(rig: bpy.types.Object) -> None:
    """Reset the legacy Euler source actions before sampling them for baking."""
    for bone in rig.pose.bones:
        bone.rotation_mode = "XYZ"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_euler = (0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def clear_baked_pose(rig: bpy.types.Object) -> None:
    """Reset the complete baked actions without disabling quaternion channels."""
    for bone in rig.pose.bones:
        bone.rotation_mode = "QUATERNION"
        bone.location = (0.0, 0.0, 0.0)
        bone.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        bone.scale = (1.0, 1.0, 1.0)


def bake_complete_actions(rig: bpy.types.Object) -> None:
    """Bake every source clip to full quaternion channels in its action slot."""
    scene = bpy.context.scene
    for source in list(bpy.data.actions):
        base.RIG = rig
        clear_pose(rig)
        assign_action(rig, source)
        start, end = (int(round(value)) for value in source.frame_range)
        frames = []
        for frame in range(start, end + 1):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            frames.append({
                bone.name: bone.matrix_basis.decompose()
                for bone in rig.pose.bones
            })

        baked = bpy.data.actions.new(source.name + "__baked")
        baked.use_fake_user = True
        assign_action(rig, baked)
        for frame, snapshot in zip(range(start, end + 1), frames):
            for bone in rig.pose.bones:
                location, rotation, scale = snapshot[bone.name]
                bone.rotation_mode = "QUATERNION"
                bone.location = location
                bone.rotation_quaternion = rotation
                bone.scale = scale
                bone.keyframe_insert("location", frame=frame, group=bone.name)
                bone.keyframe_insert("rotation_quaternion", frame=frame, group=bone.name)
                bone.keyframe_insert("scale", frame=frame, group=bone.name)
        name = source.name
        bpy.data.actions.remove(source)
        baked.name = name
    rig.animation_data.action = None


def pose_signature(rig: bpy.types.Object) -> dict[str, list[float]]:
    return {
        bone.name: [round(value, 6) for row in bone.matrix for value in row]
        for bone in rig.pose.bones
    }


def signature_delta(first: dict[str, list[float]], second: dict[str, list[float]]) -> float:
    return sum(
        abs(first[bone][index] - second[bone][index])
        for bone in first
        for index in range(len(first[bone]))
    )


def verify_action_switching(rig: bpy.types.Object) -> dict:
    scene = bpy.context.scene
    base.RIG = rig
    clear_baked_pose(rig)

    run = bpy.data.actions["Run"]
    assign_action(rig, run)
    scene.frame_set(int(round(run.frame_range[0])))
    bpy.context.view_layer.update()
    run_start = pose_signature(rig)
    scene.frame_set(int(round(sum(run.frame_range) * 0.5)))
    bpy.context.view_layer.update()
    run_middle = pose_signature(rig)
    run_delta = signature_delta(run_start, run_middle)
    if run_delta <= 0.01:
        raise RuntimeError(f"Baked Run has no meaningful motion: {run_delta}")

    clear_baked_pose(rig)
    assign_action(rig, bpy.data.actions["Idle"])
    scene.frame_set(1)
    bpy.context.view_layer.update()
    cold_idle = pose_signature(rig)
    results = {"runPoseDelta": run_delta, "runHasMotion": True}
    for prior_name in ("Run", "Dodge"):
        prior = bpy.data.actions[prior_name]
        assign_action(rig, prior)
        scene.frame_set(int(round(sum(prior.frame_range) * 0.5)))
        bpy.context.view_layer.update()
        assign_action(rig, bpy.data.actions["Idle"])
        scene.frame_set(1)
        bpy.context.view_layer.update()
        results["idleAfter" + prior_name] = pose_signature(rig) == cold_idle
    if not results["idleAfterRun"] or not results["idleAfterDodge"]:
        raise RuntimeError("Action-switch contamination remains: " + json.dumps(results))
    return results


def extract_textures() -> list[Path]:
    UNITY_DIR.mkdir(parents=True, exist_ok=True)
    outputs = []
    images = [image for image in bpy.data.images if image.source != "VIEWER"]
    for index, image in enumerate(images):
        name = "meshy-snow-kimono-texture-{}.png".format(index)
        output = UNITY_DIR / name
        image.save_render(str(output))
        image.filepath = str(output)
        image.filepath_raw = str(output)
        outputs.append(output)
    # Meshy glTF packs metallic in blue and roughness in green. URP Lit expects
    # metallic in red and smoothness in alpha, so derive that map losslessly.
    orm = next((image for image in images if image.name == "Image_1"), None)
    if orm is None:
        raise RuntimeError("Meshy ORM image Image_1 was not found")
    pixels = np.empty(len(orm.pixels), dtype=np.float32)
    orm.pixels.foreach_get(pixels)
    packed = np.zeros_like(pixels)
    packed[0::4] = pixels[2::4]
    packed[3::4] = 1.0 - pixels[1::4]
    metallic = bpy.data.images.new(
        "MeshySnowKimonoMetallicSmoothness", width=orm.size[0], height=orm.size[1], alpha=True,
    )
    metallic.colorspace_settings.name = "Non-Color"
    metallic.pixels.foreach_set(packed)
    metallic_output = UNITY_DIR / "meshy-snow-kimono-metallic-smoothness.png"
    metallic.filepath_raw = str(metallic_output)
    metallic.file_format = "PNG"
    metallic.save()
    outputs.append(metallic_output)
    return outputs


def assign_action(rig: bpy.types.Object, action: bpy.types.Action) -> None:
    rig.animation_data_create()
    rig.animation_data.action = action
    if hasattr(rig.animation_data, "action_slot") and hasattr(action, "slots"):
        for slot in action.slots:
            identifier = getattr(slot, "identifier", "") or ""
            if rig.name in identifier or slot.target_id_type == "OBJECT":
                rig.animation_data.action_slot = slot
                break


def add_studio() -> list[bpy.types.Object]:
    helpers = []
    floor_mat = material("PreviewOnly_Ground", (0.10, 0.11, 0.13, 1), roughness=0.92)
    bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.004))
    floor = bpy.context.object
    floor.name = "PreviewOnly_Ground"
    floor.data.materials.append(floor_mat)
    helpers.append(floor)
    for name, location, energy, size, color in (
        ("Key", (-2.2, -3.2, 3.2), 260, 3.5, (1.0, 0.94, 0.90)),
        ("Fill", (2.4, -2.0, 2.2), 120, 3.0, (0.72, 0.82, 1.0)),
        ("Rim", (0.4, 2.8, 2.7), 220, 2.4, (0.55, 0.70, 1.0)),
    ):
        data = bpy.data.lights.new("PreviewOnly_" + name, "AREA")
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        data.color = color
        obj = bpy.data.objects.new("PreviewOnly_" + name, data)
        bpy.context.collection.objects.link(obj)
        obj.location = location
        base.point_camera(obj, (0, 0, 0.88))
        helpers.append(obj)
    return helpers


def render_view(path: Path, rig: bpy.types.Object, action_name: str, frame: int, camera_location) -> None:
    base.RIG = rig
    clear_baked_pose(rig)
    action = bpy.data.actions[action_name]
    assign_action(rig, action)
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    camera_data = bpy.data.cameras.new("PreviewOnly_Camera")
    camera_data.lens = 58
    camera = bpy.data.objects.new("PreviewOnly_Camera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera.location = camera_location
    base.point_camera(camera, (0, 0, 0.84))
    bpy.context.scene.camera = camera
    scene = bpy.context.scene
    scene.render.resolution_x = 768
    scene.render.resolution_y = 1152
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)
    bpy.data.cameras.remove(camera_data)


def render_previews(rig: bpy.types.Object) -> None:
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    helpers = add_studio()
    render_view(PREVIEWS / "front.png", rig, "Idle", 1, (0, -3.4, 0.88))
    render_view(PREVIEWS / "side.png", rig, "Idle", 1, (3.4, 0, 0.88))
    render_view(PREVIEWS / "back.png", rig, "Idle", 1, (0, 3.4, 0.88))
    for name in ("Run", "Sword", "Dodge"):
        action = bpy.data.actions[name]
        frame = int(round((action.frame_range[0] + action.frame_range[1]) * 0.5))
        render_view(PREVIEWS / f"{name.lower()}.png", rig, name, frame, (2.1, -3.0, 1.12))
    rig.animation_data.action = None
    for obj in helpers:
        bpy.data.objects.remove(obj, do_unlink=True)


def export_fbx(rig: bpy.types.Object) -> None:
    EXPORT_FBX.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.context.scene.objects:
        if obj.type in {"MESH", "ARMATURE"} and not obj.name.startswith("PreviewOnly"):
            obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(
        filepath=str(EXPORT_FBX), use_selection=True,
        object_types={"ARMATURE", "MESH"}, axis_forward="-Z", axis_up="Y",
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", use_space_transform=True,
        add_leaf_bones=False, use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0,
        path_mode="COPY", embed_textures=False,
    )
    shutil.copy2(EXPORT_FBX, UNITY_FBX)


def motion_sample(rig: bpy.types.Object, action: bpy.types.Action) -> dict:
    base.RIG = rig
    clear_baked_pose(rig)
    assign_action(rig, action)
    scene = bpy.context.scene
    start = int(round(action.frame_range[0]))
    middle = int(round((action.frame_range[0] + action.frame_range[1]) * 0.5))
    end = int(round(action.frame_range[1]))
    bones = ("Pelvis", "Chest", "UpperArm.L", "UpperArm.R", "Thigh.L", "Thigh.R", "Weapon")
    poses = []
    roots = []
    for frame in (start, middle, end):
        scene.frame_set(frame)
        bpy.context.view_layer.update()
        poses.append({name: list(rig.pose.bones[name].matrix.translation) for name in bones})
        roots.append(list(rig.pose.bones["Root"].matrix.translation))
    motion = sum((Vector(poses[0][name]) - Vector(poses[1][name])).length for name in bones)
    root_displacement = (Vector(roots[0]) - Vector(roots[2])).length
    return {
        "action": action.name.split("|")[-1],
        "frames": [start, middle, end],
        "motionMagnitude": motion,
        "rootDisplacement": root_displacement,
    }


def validate_reimport() -> dict:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(EXPORT_FBX), use_anim=True, automatic_bone_orientation=True)
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    if len(rigs) != 1:
        raise RuntimeError(f"FBX reimport has {len(rigs)} armatures")
    rig = rigs[0]
    actions = {action.name.split("|")[-1]: action for action in bpy.data.actions}
    missing = sorted(set(ACTION_NAMES) - set(actions))
    samples = [motion_sample(rig, actions[name]) for name in ("Walk", "Run", "Sword", "Dodge")]
    checks = {
        "oneArmature": len(rigs) == 1,
        "allActions": not missing,
        "sampledActionsMove": all(sample["motionMagnitude"] > 0.01 for sample in samples),
        "locomotionRootInPlace": all(
            sample["rootDisplacement"] < 0.002 for sample in samples if sample["action"] in {"Walk", "Run"}
        ),
    }
    report = {
        "passed": all(checks.values()),
        "checks": checks,
        "actions": sorted(actions),
        "missingActions": missing,
        "samples": samples,
        "meshNames": sorted(obj.name for obj in bpy.context.scene.objects if obj.type == "MESH"),
    }
    REIMPORT_REPORT.parent.mkdir(parents=True, exist_ok=True)
    REIMPORT_REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    if not report["passed"]:
        raise RuntimeError("FBX validation failed: " + json.dumps(report))
    return report


def write_manifest(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    textures: list[Path],
    validation: dict,
    action_switching: dict,
) -> None:
    body.data.calc_loop_triangles()
    unweighted = sum(1 for vertex in body.data.vertices if not vertex.groups)
    data = {
        "schemaVersion": 1,
        "taskId": "ORC-20260905-001",
        "asset": "meshy-snow-kimono",
        "appearanceSource": str(SOURCE_GLB.relative_to(ROOT)).replace("\\", "/"),
        "source": str(BLEND_PATH.relative_to(ROOT)).replace("\\", "/"),
        "fbx": str(EXPORT_FBX.relative_to(ROOT)).replace("\\", "/"),
        "unityFbx": str(UNITY_FBX.relative_to(ROOT)).replace("\\", "/"),
        "textures": [str(path.relative_to(ROOT)).replace("\\", "/") for path in textures],
        "heightMeters": TARGET_HEIGHT,
        "sourceAxes": {"up": "+Z", "forward": "-Y"},
        "unityAxes": {"up": "+Y", "forward": "+Z"},
        "origin": "ground-center",
        "vertices": len(body.data.vertices),
        "triangles": len(body.data.loop_triangles),
        "unweightedVertices": unweighted,
        "bones": len(rig.data.bones),
        "actions": sorted(action.name for action in bpy.data.actions),
        "requiredActions": ACTION_NAMES,
        "separateProps": ["MeshySnowKimonoKatana", "MeshySnowKimonoSaya"],
        "fbxValidation": validation,
        "actionSwitching": action_switching,
        "notes": (
            "One approved Meshy body is used for every action; no dual-body swap. "
            "The 240k-triangle derivative is a Windows prototype/runtime budget; "
            "Android and performance validation are not claimed."
        ),
    }
    MANIFEST_PATH.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST_PATH.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    global RIG
    reset()
    body = import_and_optimize()
    RIG = create_rig()
    v2.RIG = RIG
    base.RIG = RIG
    bind_automatic(body, RIG)
    create_bow_and_props(RIG)
    build_actions(RIG)
    action_switching = verify_action_switching(RIG)
    textures = extract_textures()
    BLEND_PATH.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    render_previews(RIG)
    export_fbx(RIG)
    validation = validate_reimport()
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    body = bpy.data.objects["MeshySnowKimonoBody"]
    rig = bpy.data.objects["MeshySnowKimonoRig"]
    write_manifest(body, rig, textures, validation, action_switching)
    print("MESHY_SNOW_KIMONO_COMPLETE=" + json.dumps({
        "blend": str(BLEND_PATH), "fbx": str(EXPORT_FBX), "unityFbx": str(UNITY_FBX),
        "validation": validation,
        "actionSwitching": action_switching,
    }))


if __name__ == "__main__":
    main()
