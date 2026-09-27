"""Replace the embedded game APK while retaining the supplied Android host.

Usage: python3 repack_outer_hook.py original_outer.apk modded_inner.apk unsigned_outer.apk [target_version_code]
The caller must zipalign and sign the output APK after this step.
"""
import shutil
import sys
import zipfile

from axml_version import patch_version


outer_path, inner_path, output_path = sys.argv[1:4]
target_version_code = int(sys.argv[4]) if len(sys.argv) > 4 else None
hook_name = "assets/hook.apk"

with zipfile.ZipFile(inner_path) as inner:
    if inner.testzip() is not None:
        raise ValueError("modified inner APK is corrupt")
    if "assets/MelonLoader/NMLMods/LordWarMod/mod.json" not in inner.namelist():
        raise ValueError("LordWar mod missing from inner APK")

with zipfile.ZipFile(outer_path) as source, zipfile.ZipFile(output_path, "w", allowZip64=True) as target:
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
            with open(inner_path, "rb") as replacement, target.open(info, "w", force_zip64=True) as dest:
                shutil.copyfileobj(replacement, dest, 1024 * 1024)
        elif info.filename == "AndroidManifest.xml" and target_version_code is not None:
            target.writestr(info, patch_version(source.read(info), 688, target_version_code))
        else:
            with source.open(info) as data, target.open(info, "w", force_zip64=info.file_size > 2**31) as dest:
                shutil.copyfileobj(data, dest, 1024 * 1024)

print("Embedded modified inner APK in original host:", output_path)
