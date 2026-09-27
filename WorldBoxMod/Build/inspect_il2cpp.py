#!/usr/bin/env python3
"""Check whether a game's native IL2CPP API is visible to the current loader.

The bundled LemonLoader bootstrap resolves these names with dlsym. A valid APK,
working game, and intact metadata do not imply that dlsym can find the API.
"""

import argparse
import hashlib
import json
import subprocess
import tempfile
import zipfile
from pathlib import Path


LIB_NAME = "lib/arm64-v8a/libil2cpp.so"
BOOTSTRAP_EXPORTS = (
    "il2cpp_init",
    "il2cpp_domain_get",
    "il2cpp_thread_attach",
    "il2cpp_runtime_invoke",
    "il2cpp_string_new",
    "il2cpp_add_internal_call",
)


def inspect(apk):
    with zipfile.ZipFile(apk) as archive:
        with archive.open(LIB_NAME) as src, tempfile.NamedTemporaryFile() as tmp:
            digest = hashlib.sha256()
            while chunk := src.read(1024 * 1024):
                tmp.write(chunk)
                digest.update(chunk)
            tmp.flush()
            proc = subprocess.run(
                ["readelf", "--dyn-syms", "--wide", tmp.name],
                check=True, text=True, capture_output=True,
            )

    exported = set()
    for line in proc.stdout.splitlines():
        columns = line.split()
        if len(columns) >= 8 and columns[3] == "FUNC" and columns[6] != "UND":
            exported.add(columns[7].split("@", 1)[0])
    missing = sorted(set(BOOTSTRAP_EXPORTS) - exported)
    return {
        "apk": str(apk),
        "libil2cpp_sha256": digest.hexdigest(),
        "required_exports": list(BOOTSTRAP_EXPORTS),
        "missing_exports": missing,
        "status": "BLOCKED_LOADER_API" if missing else "EXPORT_PREFLIGHT_PASS_RUNTIME_NOT_TESTED",
    }


def require_compatible(apk):
    report = inspect(apk)
    if report["missing_exports"]:
        raise ValueError(
            "The bundled LemonLoader cannot load this IL2CPP binary: "
            + ", ".join(report["missing_exports"])
            + "; sha256=" + report["libil2cpp_sha256"]
            + ". The previous APK failed at dlsym(il2cpp_init). "
            + "Do not produce another candidate from this loader/game pair."
        )
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    args = parser.parse_args()
    result = inspect(args.apk)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    raise SystemExit(2 if result["missing_exports"] else 0)
