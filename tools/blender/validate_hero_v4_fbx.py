"""Clean-reimport and backface QA for heroine-v4 FBX."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_hero_v3_fbx as qa  # noqa: E402


BLEND_PATH = ROOT / "art" / "3d" / "source" / "heroine-v4.blend"
FBX_PATH = ROOT / "unity" / "CoffeeGame" / "Assets" / "CoffeeGame" / "Resources" / "Models" / "Hero" / "heroine-v4.fbx"
MANIFEST_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v4.json"
REPORT_PATH = ROOT / "art" / "3d" / "manifests" / "heroine-v4-fbx-validation.json"
PREVIEWS = [
    ROOT / "art" / "3d" / "previews" / "heroine-v4-front.png",
    ROOT / "art" / "3d" / "previews" / "heroine-v4-game-camera.png",
]
REQUIRED_ACTIONS = qa.REQUIRED_ACTIONS


def preview_info():
    result = []
    for path in PREVIEWS:
        image = bpy.data.images.load(str(path), check_existing=False)
        result.append({
            "path": str(path.relative_to(ROOT)).replace("\\", "/"),
            "size": [int(image.size[0]), int(image.size[1])],
            "bytes": path.stat().st_size,
        })
        bpy.data.images.remove(image)
    return result


def validate():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND_PATH))
    source = qa.scene_counts()
    source_normals = qa.normal_qa()
    source_missing = sorted(REQUIRED_ACTIONS - set(source["actionNames"]))
    previews = preview_info()

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(FBX_PATH), use_anim=True)
    imported = qa.scene_counts()
    imported_normals = qa.normal_qa()
    imported_missing = sorted(REQUIRED_ACTIONS - set(imported["actionNames"]))

    checks = {
        "sourceMeshContract": source["meshObjects"] == 3,
        "sourceRigContract": source["armatures"] == 1 and source["bones"] == 20,
        "sourceActionContract": not source_missing,
        "sourceAndroidBudget": source["triangles"] <= 40000,
        "sourceOutsideNormals": source_normals["backfaceCullSafe"],
        "fbxMeshContract": imported["meshObjects"] == 3,
        "fbxRigContract": imported["armatures"] == 1 and imported["bones"] == 20,
        "fbxActionContract": not imported_missing,
        "fbxOutsideNormals": imported_normals["backfaceCullSafe"],
        "previewsExist": all(path.exists() and path.stat().st_size > 10000 for path in PREVIEWS),
    }
    passed = all(checks.values())
    report = {
        "asset": "heroine-v4",
        "passed": passed,
        "checks": checks,
        "source": source,
        "fbxReimport": imported,
        "missingActions": {"source": source_missing, "fbx": imported_missing},
        "outsideNormalQA": {"source": source_normals, "fbxReimport": imported_normals},
        "previews": previews,
    }
    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest["validation"] = {
        "reopened": True,
        "fbxReimportPassed": passed,
        "outsideNormalQAPassed": source_normals["backfaceCullSafe"] and imported_normals["backfaceCullSafe"],
        "missingRequiredActions": imported_missing,
        "report": str(REPORT_PATH.relative_to(ROOT)).replace("\\", "/"),
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print("HEROINE_V4_VALIDATION=" + json.dumps(report, ensure_ascii=False))
    if not passed:
        raise RuntimeError("Heroine v4 validation failed: " + json.dumps(checks))


if __name__ == "__main__":
    validate()
