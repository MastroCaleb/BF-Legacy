#!/usr/bin/env python3
"""One-shot asmdef setup for BF-Legacy (PLAN.md Phase 4).

Actions:
  1. git mv Assets/Scripts/Import/Particles/ParticleEffect.cs (+ .meta)
        -> Assets/Scripts/Particles/ParticleEffect.cs
     (runtime data type referenced by Ability.cs / UnitBehaviour.cs)
  2. Write Assets/Scripts/BF.Runtime.asmdef        (all platforms, no refs)
  3. Write Assets/Scripts/Import/BF.Import.asmdef   (Editor-only, refs BF.Runtime)
  4. Write Assets/Editor/BF.Editor.asmdef           (Editor-only, no refs)

Idempotent: skips anything that already exists; safe to re-run.
Usage:  python3 asmdef.py [--dry-run]
"""

import argparse
import json
import subprocess
import sys
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parent

MOVE_SRC = "Assets/Scripts/Import/Particles/ParticleEffect.cs"
MOVE_DST = "Assets/Scripts/Particles/ParticleEffect.cs"

# (rel_path, assembly_name, editor_only, referenced_assembly_names)
SPECS = [
    ("Assets/Scripts/BF.Runtime.asmdef", "BF.Runtime", False, []),
    ("Assets/Scripts/Import/BF.Import.asmdef", "BF.Import", True, ["BF.Runtime"]),
    ("Assets/Editor/BF.Editor.asmdef", "BF.Editor", True, []),
]

ASMDEF_TEMPLATE = {
    "name": "",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": False,
    "overrideReferences": False,
    "precompiledReferences": [],
    "autoReferenced": True,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": False,
}

META_TEMPLATE = (
    "fileFormatVersion: 2\n"
    "guid: {guid}\n"
    "AssemblyDefinitionImporter:\n"
    "  externalObjects: {{}}\n"
    "  userData: \n"
    "  assetBundleName: \n"
    "  assetBundleVariant: \n"
)


def sh(*cmd):
    return subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)


def move_particle_effect(dry):
    print("[1/3] Moving ParticleEffect.cs to runtime tree")
    src, dst = ROOT / MOVE_SRC, ROOT / MOVE_DST
    if dst.exists():
        print(f"  SKIP: already at {MOVE_DST}")
        return
    if not src.exists():
        print(f"  ERROR: {MOVE_SRC} not found (and not at destination either)")
        sys.exit(1)

    if dry:
        print(f"  DRY: git mv {MOVE_SRC} -> {MOVE_DST} (+ .meta)")
        return

    dst.parent.mkdir(parents=True, exist_ok=True)
    r = sh("git", "mv", MOVE_SRC, MOVE_DST)
    if r.returncode != 0:
        print(f"  ERROR: git mv failed:\n{r.stderr.strip()}")
        sys.exit(1)

    src_meta = Path(str(src) + ".meta")
    dst_meta = Path(str(dst) + ".meta")
    if src_meta.exists():
        r = sh("git", "mv", str(src_meta), str(dst_meta))
        if r.returncode != 0:
            src_meta.rename(dst_meta)
            print("  NOTE: .meta moved via filesystem (was untracked)")
    print(f"  OK: {MOVE_DST}")


def get_or_create_guid(asset_path, dry):
    """Return the asset's .meta guid, generating (and writing) the .meta if absent."""
    meta = Path(str(asset_path) + ".meta")
    if meta.exists():
        for line in meta.read_text(encoding="utf-8").splitlines():
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    guid = uuid.uuid4().hex
    if not dry:
        meta.write_text(META_TEMPLATE.format(guid=guid), encoding="utf-8")
    return guid


def write_asmdefs(dry):
    print("[2/3] Writing asmdefs")
    guids = {}
    for rel, name, _, _ in SPECS:
        guids[name] = get_or_create_guid(ROOT / rel, dry)

    for rel, name, editor_only, refs in SPECS:
        path = ROOT / rel
        if path.exists():
            print(f"  SKIP: {rel} already exists")
            continue
        cfg = dict(ASMDEF_TEMPLATE)
        cfg["name"] = name
        if editor_only:
            cfg["includePlatforms"] = ["Editor"]
        cfg["references"] = [f"GUID:{guids[r]}" for r in refs]
        if dry:
            print(f"  DRY: would write {rel} ({name}, "
                  f"{'Editor-only' if editor_only else 'all platforms'})")
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(cfg, indent=4) + "\n", encoding="utf-8")
        print(f"  OK: {rel}")


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--dry-run", action="store_true", help="preview only, no changes")
    dry = ap.parse_args().dry_run

    if not (ROOT / "Assets").is_dir():
        print(f"ERROR: {ROOT} does not look like the Unity project root (no Assets/)")
        sys.exit(1)

    move_particle_effect(dry)
    write_asmdefs(dry)

    print("[3/3] Done." if not dry else "[3/3] Dry run complete - nothing written.")
    print("\nNext:")
    print("  1. Open the project in Unity -> expect zero Console errors")
    print("  2. Play a battle + one menu scene")
    print("  3. Player builds now exclude Import/ and Editor/ code automatically")


if __name__ == "__main__":
    main()
