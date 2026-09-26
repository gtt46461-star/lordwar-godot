#!/usr/bin/env python3
"""Reduce Godot GLES canvas global uniforms below Android SwiftShader's ES3 limit.

Godot 4.7 embeds MAX_GLOBAL_SHADER_UNIFORMS=256 in libgodot_android.so.
On GLES implementations exposing the ES3 minimum of 256 fragment uniform
vectors, the default canvas shader needs 261 and fails to compile. This patch
keeps a conservative 128-slot global shader array in the packaged runtime.
"""
from __future__ import annotations

import os
from pathlib import Path
import shutil
import tempfile
import zipfile

apk = Path(os.environ["LORDWAR_APK"])
needle = b"#define MAX_GLOBAL_SHADER_UNIFORMS 256\n"
replacement = b"#define MAX_GLOBAL_SHADER_UNIFORMS 128\n"
patched = []
fd, tmp_name = tempfile.mkstemp(prefix=apk.stem + "-patched-", suffix=".apk", dir=apk.parent)
os.close(fd)
tmp = Path(tmp_name)
try:
    with zipfile.ZipFile(apk, "r") as source, zipfile.ZipFile(tmp, "w") as target:
        for info in source.infolist():
            data = source.read(info.filename)
            if info.filename.startswith("lib/") and info.filename.endswith("/libgodot_android.so"):
                count = data.count(needle)
                if count != 1:
                    raise RuntimeError(f"{info.filename}: expected one shader uniform limit, found {count}")
                data = data.replace(needle, replacement, 1)
                patched.append(info.filename)
            target.writestr(info, data)
    if not patched:
        raise RuntimeError("APK has no libgodot_android.so libraries to patch")
    shutil.move(tmp, apk)
finally:
    tmp.unlink(missing_ok=True)
print("Patched MAX_GLOBAL_SHADER_UNIFORMS=128 in " + ", ".join(patched))
