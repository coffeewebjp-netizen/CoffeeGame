"""Reimport heroine-v2.fbx in an empty Blender scene and write a QA report."""

from __future__ import annotations

import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v2.fbx"
REPORT_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v2-fbx-validation.json"
REQUIRED = [
    "Idle", "Walk", "Run", "Jump", "Fall", "Land", "Sword", "AirSlash",
    "Plunge", "SpinCharge", "SpinRelease", "MagicCharge", "MagicRelease",
    "Hurt", "Defeated",
]


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(FBX_PATH))

meshes = [obj for obj in bpy.data.objects if obj.type == "MESH"]
armatures = [obj for obj in bpy.data.objects if obj.type == "ARMATURE"]
actions = sorted(action.name.split("|")[-1] for action in bpy.data.actions)
missing = sorted(set(REQUIRED) - set(actions))
materials = {
    slot.material.name
    for obj in meshes
    for slot in obj.material_slots
    if slot.material is not None
}

report = {
    "asset": "heroine-v2",
    "sourceFbx": str(FBX_PATH.relative_to(ROOT)).replace("\\", "/"),
    "importer": bpy.app.version_string,
    "meshObjects": len(meshes),
    "meshObjectNames": sorted(obj.name for obj in meshes),
    "vertices": sum(len(obj.data.vertices) for obj in meshes),
    "materials": len(materials),
    "armatures": len(armatures),
    "bones": len(armatures[0].data.bones) if armatures else 0,
    "actions": len(actions),
    "actionNames": actions,
    "missingRequiredActions": missing,
}
report["passed"] = (
    report["meshObjects"] == 3
    and report["armatures"] == 1
    and report["bones"] >= 20
    and report["actions"] == 15
    and not missing
)
REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("HERO_V2_FBX_VALIDATION=" + json.dumps(report, sort_keys=True))
if not report["passed"]:
    raise RuntimeError("Heroine v2 FBX reimport validation failed")
