"""Replace the embedded game APK while retaining the supplied Android host.

Usage: python3 repack_outer_hook.py original_outer.apk modded_inner.apk unsigned_outer.apk [target_version_code]
The caller must zipalign and sign the output APK after this step.
"""
import shutil
import sys
import zipfile
from copy import copy
from datetime import datetime, timedelta

from axml_version import patch_version


outer_path, inner_path, output_path = sys.argv[1:4]
target_version_code = int(sys.argv[4]) if len(sys.argv) > 4 else None
hook_name = "assets/hook.apk"
loader_asset_roots = ("assets/MelonLoader/", "assets/dotnet/", "assets/copyToData/")
loader_native_names = {
    "lib/arm64-v8a/libil2cpp.so",
    "lib/arm64-v8a/libmain.so",
    "lib/arm64-v8a/libBootstrap.so",
    "lib/arm64-v8a/libcrypto.so",
    "lib/arm64-v8a/libdobby.so",
    "lib/arm64-v8a/libssl.so",
}


def runtime_overlay(name):
    return name.startswith(loader_asset_roots) or name in loader_native_names

with zipfile.ZipFile(inner_path) as inner, zipfile.ZipFile(outer_path) as source, zipfile.ZipFile(output_path, "w", allowZip64=True) as target:
    if inner.testzip() is not None:
        raise ValueError("modified inner APK is corrupt")
    if "assets/MelonLoader/NMLMods/LordWarMod/mod.json" not in inner.namelist():
        raise ValueError("LordWar mod missing from inner APK")
    if not loader_native_names.issubset(inner.namelist()):
        raise ValueError("inner APK is missing a required arm64 loader library")
    overlay = {name for name in inner.namelist() if runtime_overlay(name)}
    if not any(name.startswith("assets/MelonLoader/net8/") for name in overlay):
        raise ValueError("inner APK is missing the MelonLoader runtime assets")
    if hook_name not in source.namelist():
        raise ValueError("original outer APK has no embedded hook.apk")
    for info in source.infolist():
        # Only remove invalid v1 signature files; META-INF/services entries
        # are runtime metadata and must remain in the host.
        entry = info.filename.rsplit("/", 1)[-1].upper()
        if info.filename.startswith("META-INF/") and (
            entry == "MANIFEST.MF" or entry.endswith((".SF", ".RSA", ".DSA", ".EC"))
        ):
            continue
        if info.filename == hook_name:
            updated = copy(info)
            # The host compares its extracted hook.apk with the ZIP entry's time.
            # Give each version a distinct timestamp without deleting app data.
            day = datetime(2026, 1, 1) + timedelta(days=(target_version_code or 688) - 688)
            updated.date_time = (day.year, day.month, day.day, 0, 0, 0)
            with open(inner_path, "rb") as replacement, target.open(updated, "w", force_zip64=True) as dest:
                shutil.copyfileobj(replacement, dest, 1024 * 1024)
        elif info.filename == "AndroidManifest.xml" and target_version_code is not None:
            target.writestr(copy(info), patch_version(source.read(info), 688, target_version_code))
        elif info.filename in overlay:
            with inner.open(info.filename) as data, target.open(copy(info), "w", force_zip64=info.file_size > 2**31) as dest:
                shutil.copyfileobj(data, dest, 1024 * 1024)
            overlay.remove(info.filename)
        else:
            with source.open(info) as data, target.open(copy(info), "w", force_zip64=info.file_size > 2**31) as dest:
                shutil.copyfileobj(data, dest, 1024 * 1024)

    for name in sorted(overlay):
        info = inner.getinfo(name)
        with inner.open(info) as data, target.open(copy(info), "w", force_zip64=info.file_size > 2**31) as dest:
            shutil.copyfileobj(data, dest, 1024 * 1024)

print("Embedded modified inner APK in original host:", output_path)
