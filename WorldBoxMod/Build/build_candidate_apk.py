#!/usr/bin/env python3
"""Rebuild the user supplied 0.50.6 host with the active WorldBox NML mod.

The patched loader seed is an input, never a claim of runtime compatibility.
Neither the game APK, loader binaries nor the signing key belong in git.
"""

import argparse
import hashlib
import json
import os
import shutil
import subprocess
import tempfile
import zipfile
from pathlib import Path

from package_mod import MOD_DIR, package
from axml_version import patch_version


EXPECTED_OUTER = "77c31e2f6a063754aad809c4b43ed03844ba3e2de80b66706938736fa4e456e5"
SOURCE_VERSION_CODE = 688
TARGET_VERSION_CODE = 689
MOD_PREFIX = "assets/MelonLoader/NMLMods/LordWarMod/"
NML_DLL = "assets/MelonLoader/Mods/NeoModLoader_mobile.dll"
GAME_ANCHORS = (
    "lib/arm64-v8a/libil2cpp.so",
    "assets/bin/Data/globalgamemanagers",
    "assets/bin/Data/Managed/Metadata/global-metadata.dat",
)
SIGNATURE_NAMES = ("META-INF/MANIFEST.MF",)


def sha_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def sha_entry(archive, name):
    digest = hashlib.sha256()
    with archive.open(name) as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_signature(name):
    return name in SIGNATURE_NAMES or (
        name.startswith("META-INF/") and name.rsplit("/", 1)[-1].upper().endswith((".SF", ".RSA", ".DSA", ".EC"))
    )


def run(*argv, env=None):
    subprocess.run(argv, check=True, env=env, stdout=subprocess.DEVNULL)


def badging(path, version_code=SOURCE_VERSION_CODE):
    line = subprocess.check_output(["aapt", "dump", "badging", str(path)], text=True).splitlines()[0]
    required = ("name='com.mkarpenko.worldbox'", f"versionCode='{version_code}'", "versionName='0.50.6'")
    if not all(field in line for field in required):
        raise ValueError("Unexpected app identity: " + line)
    return line


def verify_game_anchors(original_inner, loader_seed):
    with zipfile.ZipFile(original_inner) as original, zipfile.ZipFile(loader_seed) as seeded:
        if original.testzip() or seeded.testzip():
            raise ValueError("Corrupt original or loader seed")
        if NML_DLL not in seeded.namelist() or "assets/MelonLoader/net8/MelonLoader.dll" not in seeded.namelist():
            raise ValueError("The patched seed lacks the mobile NML/MelonLoader files")
        for name in GAME_ANCHORS:
            if sha_entry(original, name) != sha_entry(seeded, name):
                raise ValueError("Loader seed contains a different game anchor: " + name)


def replace_mod(loader_seed, mod_zip, unsigned_inner):
    with zipfile.ZipFile(loader_seed) as source, zipfile.ZipFile(mod_zip) as mod, zipfile.ZipFile(unsigned_inner, "w", allowZip64=True) as target:
        for info in source.infolist():
            if info.filename.startswith(MOD_PREFIX) or is_signature(info.filename):
                continue
            if info.filename == "AndroidManifest.xml":
                target.writestr(info, patch_version(source.read(info), SOURCE_VERSION_CODE, TARGET_VERSION_CODE))
                continue
            with source.open(info) as content, target.open(info, "w", force_zip64=info.file_size > 2**31) as output:
                shutil.copyfileobj(content, output, 1024 * 1024)
        for info in mod.infolist():
            name = "assets/MelonLoader/NMLMods/" + info.filename
            with mod.open(info) as content, target.open(name, "w") as output:
                shutil.copyfileobj(content, output, 1024 * 1024)


def verify_mod(inner_path, mod_zip):
    with zipfile.ZipFile(inner_path) as inner, zipfile.ZipFile(mod_zip) as mod:
        assert inner.testzip() is None
        names = set(inner.namelist())
        installed = {name for name in names if name.startswith(MOD_PREFIX)}
        expected = {"assets/MelonLoader/NMLMods/" + name for name in mod.namelist()}
        if installed != expected:
            raise ValueError("Dormant game core or files from the old mod remain in the APK")
        for name in mod.namelist():
            if sha_entry(inner, "assets/MelonLoader/NMLMods/" + name) != sha_entry(mod, name):
                raise ValueError("Installed mod differs from the active source: " + name)


def verify_outer(original_path, candidate_path, inner_path):
    with zipfile.ZipFile(original_path) as original, zipfile.ZipFile(candidate_path) as candidate:
        assert candidate.testzip() is None
        old = {n for n in original.namelist() if not is_signature(n)}
        new = {n for n in candidate.namelist() if not is_signature(n)}
        if old != new:
            raise ValueError("Outer host entries changed beyond signatures")
        for name in old:
            if name == "assets/hook.apk":
                continue
            expected = (patch_version(original.read(name), SOURCE_VERSION_CODE, TARGET_VERSION_CODE)
                        if name == "AndroidManifest.xml" else None)
            if (expected is not None and candidate.read(name) != expected) or (
                expected is None and (original.getinfo(name).file_size != candidate.getinfo(name).file_size
                or sha_entry(original, name) != sha_entry(candidate, name))
            ):
                raise ValueError("Outer host file changed: " + name)
        with candidate.open("assets/hook.apk") as stream, open(inner_path, "rb") as expected:
            while True:
                a, b = stream.read(1024 * 1024), expected.read(1024 * 1024)
                if a != b:
                    raise ValueError("Outer host embeds a different inner APK")
                if not a:
                    break


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-outer", type=Path, required=True)
    parser.add_argument("--loader-seed-inner", type=Path, required=True)
    parser.add_argument("--keystore", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    if not os.environ.get("LORDWAR_KEYSTORE_PASS"):
        parser.error("Set LORDWAR_KEYSTORE_PASS in the local environment; never commit the key or password")
    if sha_file(args.original_outer) != EXPECTED_OUTER:
        raise ValueError("Not the user supplied 0.50.6 APK")
    badging(args.original_outer)
    badging(args.loader_seed_inner)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    mod_version = json.loads((MOD_DIR / "mod.json").read_text(encoding="utf-8"))["version"]
    mod_zip = args.output_dir / ("LordWarMod-" + mod_version + ".zip")
    mod_manifest = args.output_dir / "mod-manifest.json"
    package(mod_zip, mod_manifest)

    with tempfile.TemporaryDirectory(prefix="lordwar-apk-", dir=args.output_dir) as temporary:
        temp = Path(temporary)
        original_inner = temp / "original-hook.apk"
        with zipfile.ZipFile(args.original_outer) as outer, outer.open("assets/hook.apk") as data, open(original_inner, "wb") as output:
            shutil.copyfileobj(data, output, 1024 * 1024)
        badging(original_inner)
        verify_game_anchors(original_inner, args.loader_seed_inner)

        unsigned_inner = temp / "inner-unsigned.apk"
        aligned_inner = temp / "inner-aligned.apk"
        signed_inner = args.output_dir / "LordWar-0.50.6-inner-mod-candidate.apk"
        replace_mod(args.loader_seed_inner, mod_zip, unsigned_inner)
        run("zipalign", "-f", "-p", "4", str(unsigned_inner), str(aligned_inner))
        run("apksigner", "sign", "--ks", str(args.keystore), "--ks-key-alias", "lordwar", "--ks-pass", "env:LORDWAR_KEYSTORE_PASS", "--key-pass", "env:LORDWAR_KEYSTORE_PASS", "--out", str(signed_inner), str(aligned_inner))
        run("apksigner", "verify", "--verbose", str(signed_inner))
        run("zipalign", "-c", "-p", "4", str(signed_inner))
        verify_mod(signed_inner, mod_zip)
        badging(signed_inner, TARGET_VERSION_CODE)

        unsigned_outer = temp / "outer-unsigned.apk"
        aligned_outer = temp / "outer-aligned.apk"
        signed_outer = args.output_dir / "LordWar-WorldBox-0.50.6-host-mod-candidate.apk"
        run("python3", str(Path(__file__).with_name("repack_outer_hook.py")), str(args.original_outer), str(signed_inner), str(unsigned_outer), str(TARGET_VERSION_CODE))
        run("zipalign", "-f", "-p", "4", str(unsigned_outer), str(aligned_outer))
        run("apksigner", "sign", "--ks", str(args.keystore), "--ks-key-alias", "lordwar", "--ks-pass", "env:LORDWAR_KEYSTORE_PASS", "--key-pass", "env:LORDWAR_KEYSTORE_PASS", "--out", str(signed_outer), str(aligned_outer))
        run("apksigner", "verify", "--verbose", str(signed_outer))
        run("zipalign", "-c", "-p", "4", str(signed_outer))
        verify_outer(args.original_outer, signed_outer, signed_inner)
        badging(signed_outer, TARGET_VERSION_CODE)

    report = {
        "status": "SIGNED_STATIC_CANDIDATE; install, launch and gameplay NOT_RUN",
        "original_outer_sha256": EXPECTED_OUTER,
        "loader_seed_inner_sha256": sha_file(args.loader_seed_inner),
        "active_source_sha256": sha_file(MOD_DIR / "LordWarMod.cs"),
        "mod_zip_sha256": sha_file(mod_zip),
        "inner_apk_sha256": sha_file(signed_inner),
        "outer_apk_sha256": sha_file(signed_outer),
        "package": "com.mkarpenko.worldbox",
        "versionCode": TARGET_VERSION_CODE,
        "versionName": "0.50.6",
        "signer": "lordwar candidate test certificate; does not match original",
        "device": "NOT_RUN: no adb connected Android arm64 device or applicable emulator",
    }
    (args.output_dir / "build-evidence.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
