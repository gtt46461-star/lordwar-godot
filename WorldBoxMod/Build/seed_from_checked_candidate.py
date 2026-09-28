#!/usr/bin/env python3
"""Prepare an unsigned 688 loader seed from the user's original and 692 candidate.

Only the original game's IL2CPP library and versionCode are restored. The
loader/native patches from the previous candidate remain explicit inputs to
build_candidate_apk.py. The output is private input, not a redistributable APK.
"""

import argparse
from copy import copy
import hashlib
import shutil
import zipfile
from pathlib import Path

from axml_version import patch_version


EXPECTED_ORIGINAL = "77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5"
EXPECTED_CANDIDATE = "7393781d0534737156081b87270105ecab5743abe0b0c3c37dc851273b1b8fbe"
IL2CPP = "lib/arm64-v8a/libil2cpp.so"


def digest(path):
    with path.open("rb") as stream:
        h = hashlib.sha256()
        for buf in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(buf)
        return h.hexdigest()


def seed(original: Path, prior: Path, target: Path):
    if digest(original) != EXPECTED_ORIGINAL or digest(prior) != EXPECTED_CANDIDATE:
        raise ValueError("Inputs differ from the supplied WorldBox 0.50.6 and checked 692 APKs")
    target.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(original) as a, zipfile.ZipFile(prior) as b:
        with zipfile.ZipFile(a.open("assets/hook.apk")) as base, zipfile.ZipFile(b.open("assets/hook.apk")) as mod:
            if base.testzip() or mod.testzip():
                raise ValueError("Corrupt embedded APK")
            with zipfile.ZipFile(target, "w", allowZip64=True) as output:
                for entry in mod.infolist():
                    if entry.filename == IL2CPP:
                        with base.open(IL2CPP) as stream, output.open(copy(entry), "w", force_zip64=True) as dest:
                            shutil.copyfileobj(stream, dest, 1024 * 1024)
                    elif entry.filename == "AndroidManifest.xml":
                        output.writestr(copy(entry), patch_version(mod.read(entry), 692, 688))
                    else:
                        with mod.open(entry) as stream, output.open(copy(entry), "w", force_zip64=entry.file_size > 2**31) as dest:
                            shutil.copyfileobj(stream, dest, 1024 * 1024)
    with zipfile.ZipFile(target) as output:
        with zipfile.ZipFile(original) as a, zipfile.ZipFile(a.open("assets/hook.apk")) as base:
            for name in (IL2CPP, "assets/bin/Data/globalgamemanagers",
                         "assets/bin/Data/Managed/Metadata/global-metadata.dat"):
                if hashlib.sha256(output.read(name)).digest() != hashlib.sha256(base.read(name)).digest():
                    raise ValueError("A game anchor differs from the original: " + name)
        if output.testzip():
            raise ValueError("Output seed has a corrupt ZIP member")
    return {"loader_seed": str(target), "size": target.stat().st_size, "sha256": digest(target)}


if __name__ == "__main__":
    import json
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original", type=Path)
    parser.add_argument("prior_candidate", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    print(json.dumps(seed(args.original, args.prior_candidate, args.output), indent=2))
