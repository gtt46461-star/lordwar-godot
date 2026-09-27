#!/usr/bin/env python3
"""Read-only WorldBox installation inventory for an APK or split-APK archive."""

import argparse
import hashlib
import json
import re
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path


def digest(path):
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def manifest_badging(path):
    aapt = shutil.which("aapt")
    if aapt is None:
        return {"status": "NOT_RUN", "reason": "aapt not installed"}
    result = subprocess.run(
        [aapt, "dump", "badging", str(path)],
        text=True,
        capture_output=True,
        check=False,
    )
    if result.returncode:
        return {"status": "FAIL", "reason": result.stderr.strip()[-500:]}
    package = re.search(r"^package: name='([^']+)' versionCode='([^']+)' versionName='([^']*)'", result.stdout, re.M)
    if package is None:
        return {"status": "FAIL", "reason": "package badging not found"}
    return {"status": "PASS", "package": package[1], "versionCode": package[2], "versionName": package[3]}


def inspect_apk(path):
    with zipfile.ZipFile(path) as archive:
        if archive.testzip() is not None:
            raise ValueError(f"APK CRC failure: {path}")
        names = archive.namelist()
        native_abis = sorted({part[1] for name in names if name.startswith("lib/") and name.endswith(".so") for part in [name.split("/")] if len(part) >= 3})
        managed = sorted(name for name in names if name.endswith("/Assembly-CSharp.dll"))
        il2cpp = sorted(name for name in names if name.endswith("/libil2cpp.so"))
        metadata = sorted(name for name in names if name.endswith("/global-metadata.dat"))
        if il2cpp and metadata:
            backend = "IL2CPP"
        elif managed and not il2cpp:
            backend = "Mono"
        else:
            backend = "UNDETERMINED"
    return {
        "filename": path.name,
        "sha256": digest(path),
        "sizeBytes": path.stat().st_size,
        "badging": manifest_badging(path),
        "nativeAbis": native_abis,
        "backend": backend,
        "managedAssemblies": managed,
        "il2cppLibraries": il2cpp,
        "metadataFiles": metadata,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("installation", type=Path, help="local .apk, .apks, or .xapk file")
    parser.add_argument("--output", type=Path, help="optional JSON report path")
    args = parser.parse_args()
    source = args.installation.resolve()
    if not source.is_file():
        parser.error(f"file not found: {source}")
    report = {"inputSha256": digest(source), "inputSizeBytes": source.stat().st_size, "packages": []}
    if source.suffix.lower() == ".apk":
        report["packages"].append(inspect_apk(source))
    elif source.suffix.lower() in {".apks", ".xapk"}:
        with tempfile.TemporaryDirectory(prefix="lordwar-target-") as directory:
            with zipfile.ZipFile(source) as bundle:
                if bundle.testzip() is not None:
                    raise ValueError("split archive CRC failure")
                entries = [item for item in bundle.infolist() if item.filename.lower().endswith(".apk") and not item.is_dir()]
                if not entries:
                    raise ValueError("no APK entries in archive")
                for index, entry in enumerate(entries):
                    output = Path(directory) / f"{index}.apk"
                    with bundle.open(entry) as stream, output.open("wb") as target:
                        shutil.copyfileobj(stream, target)
                    item = inspect_apk(output)
                    item["filename"] = entry.filename
                    report["packages"].append(item)
    else:
        parser.error("expected .apk, .apks, or .xapk")
    packages = {item["badging"].get("package") for item in report["packages"] if item["badging"]["status"] == "PASS"}
    report["worldBoxPackageVerified"] = ("com.mkarpenko.worldbox" in packages) if packages else None
    all_il2cpp = any(item["il2cppLibraries"] for item in report["packages"])
    all_metadata = any(item["metadataFiles"] for item in report["packages"])
    all_managed = any(item["managedAssemblies"] for item in report["packages"])
    report["aggregateBackend"] = "IL2CPP" if all_il2cpp and all_metadata else "Mono" if all_managed and not all_il2cpp else "UNDETERMINED"
    report["compatibilityStatus"] = "ANALYSIS_ONLY: loader startup and gameplay require a separate device test"
    output = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.write_text(output, encoding="utf-8")
    print(output, end="")


if __name__ == "__main__":
    main()
