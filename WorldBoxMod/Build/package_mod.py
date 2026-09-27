#!/usr/bin/env python3
"""Package the NML source mod directory without game binaries or APK contents."""

import argparse
import hashlib
import json
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile, ZipInfo


MOD_DIR = Path(__file__).resolve().parents[1] / "LordWarMod"
ACTIVE_FILES = ("mod.json", "LordWarMod.cs")


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def package(output, manifest_path):
    declaration = json.loads((MOD_DIR / "mod.json").read_text(encoding="utf-8"))
    if declaration["GUID"] != "LordWar.Android.Core":
        raise ValueError("Unexpected mod GUID")

    # R30 Core and CSV are retained in the rebuildable source tree, but they
    # have no native WorldBox consumer yet and must not run in the installed mod.
    entries = [(f"LordWarMod/{name}", (MOD_DIR / name).read_bytes()) for name in ACTIVE_FILES]

    output.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(output, "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
        for name, data in entries:
            info = ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            info.compress_type = ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, data, compress_type=ZIP_DEFLATED, compresslevel=9)

    manifest = {
        "kind": "NeoModLoader Android source mod (runtime loading NOT_RUN)",
        "name": declaration["name"],
        "version": declaration["version"],
        "target_game": "WorldBox Android 0.50.6 candidate",
        "archive": output.name,
        "archive_sha256": sha256(output.read_bytes()),
        "files": [{"path": name, "size": len(data), "sha256": sha256(data)} for name, data in entries],
    }
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    with ZipFile(output) as archive:
        for item in manifest["files"]:
            if sha256(archive.read(item["path"])) != item["sha256"]:
                raise ValueError(f"Archive differs from source: {item['path']}")
    print(f"MOD_PACKAGE_OK files={len(entries)} sha256={manifest['archive_sha256']} output={output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    args = parser.parse_args()
    package(args.output, args.manifest)
