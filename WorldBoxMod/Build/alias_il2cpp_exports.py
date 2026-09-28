#!/usr/bin/env python3
"""Add standard IL2CPP symbol aliases to this *specific* user supplied game.

0.22.21 supplies the unrenamed Unity API order. 0.50.6 retains the API in
the same order, except for two extra four-byte functions just before the
bounded-array API. Every one of the 239 mapped functions has the same size.
This is a static inference, not proof of loader or game runtime compatibility.
The old and current game binaries are private build inputs, never Git files.
"""

import argparse
import hashlib
import json
import re
import subprocess
import zipfile
from pathlib import Path


REFERENCE_APK_SHA256 = "e4b37e1ffdc27fba37e1fcd9390787a248dcdc5764d123da5a47df0681ab6bd5"
REFERENCE_LIB_SHA256 = "a539e70a1bbbd081c53b186c861c6a01bd1b2bc6f3448bfd76e04d808eab0013"
TARGET_LIB_SHA256 = "88a9d6e7af066e77de1a60426e8e77f331ec3b4f26c3e2044201f45dbb4a08d3"
TARGET_FIRST = 0x18B39E8
TARGET_LAST = 0x18B47D4
LIB_NAME = "lib/arm64-v8a/libil2cpp.so"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def exports(path):
    output = subprocess.check_output(["readelf", "--dyn-syms", "--wide", str(path)], text=True)
    result = []
    for line in output.splitlines():
        columns = line.split()
        if len(columns) >= 8 and columns[3:6] == ["FUNC", "GLOBAL", "DEFAULT"]:
            result.append((int(columns[1], 16), int(columns[2]), columns[7]))
    return sorted(result)


def build_aliases(reference, target, output, report_path):
    try:
        import lief
    except ImportError as error:
        raise RuntimeError("Requires the Python package lief==1.0.0") from error

    if sha256(reference.read_bytes()) != REFERENCE_APK_SHA256:
        raise ValueError("Unrecognized reference APK; refusing speculative mapping")
    with zipfile.ZipFile(reference) as apk:
        old_binary = apk.read(LIB_NAME)
    if sha256(old_binary) != REFERENCE_LIB_SHA256:
        raise ValueError("Reference game library differs")
    if sha256(target.read_bytes()) != TARGET_LIB_SHA256:
        raise ValueError("Target game library differs")

    # LIEF parses the old binary from bytes; readelf sees the same immutable
    # reference via a private temporary file, without modifying either APK.
    import tempfile
    with tempfile.NamedTemporaryFile(suffix=".so") as old_file:
        old_file.write(old_binary)
        old_file.flush()
        old = [(address, size, name) for address, size, name in exports(old_file.name)
               if name.startswith("il2cpp_")]
    current = [(address, size, name) for address, size, name in exports(target)
               if TARGET_FIRST <= address <= TARGET_LAST and re.fullmatch(r"[A-Za-z_]{11}", name)]
    if len(old) != 239 or len(current) != 241:
        raise ValueError("Unexpected Unity exported API count")
    if old[21][2] != "il2cpp_array_new_full" or old[22][2] != "il2cpp_bounded_array_class_get":
        raise ValueError("Unity API insertion point changed")
    if current[22][1:2] != (4,) or current[23][1:2] != (4,):
        raise ValueError("Unexpected extra target APIs")

    binary = lief.parse(str(target))
    original_text_hash = sha256(bytes(binary.get_section(".text").content))
    mapping = []
    for index, (old_address, old_size, api_name) in enumerate(old):
        address, size, obfuscated = current[index + (2 if index >= 22 else 0)]
        if old_size != size or binary.get_dynamic_symbol(api_name) is not None:
            raise ValueError("API alignment or symbol table changed: " + api_name)
        original_symbol = binary.get_dynamic_symbol(obfuscated)
        if original_symbol is None or original_symbol.value != address or original_symbol.size != size:
            raise ValueError("Target ELF symbol changed: " + obfuscated)
        alias = lief.ELF.Symbol()
        alias.name = api_name
        alias.value = original_symbol.value
        alias.size = original_symbol.size
        alias.type = original_symbol.type
        alias.binding = original_symbol.binding
        alias.visibility = original_symbol.visibility
        alias.shndx = original_symbol.shndx
        binary.add_dynamic_symbol(alias)
        mapping.append({"api": api_name, "renamed": obfuscated, "original_address": address,
                        "function_size": size})

    binary.write(str(output))
    patched = lief.parse(str(output))
    if sha256(bytes(patched.get_section(".text").content)) != original_text_hash:
        raise ValueError("Executable .text bytes changed during alias insertion")
    if patched.header.machine_type != lief.ELF.ARCH.AARCH64:
        raise ValueError("Patched binary is not arm64")
    for pair in mapping:
        named = patched.get_dynamic_symbol(pair["api"])
        renamed = patched.get_dynamic_symbol(pair["renamed"])
        if named is None or renamed is None or named.value != renamed.value or named.size != renamed.size:
            raise ValueError("Alias and game symbol diverged: " + pair["api"])
    report = {
        "status": "STATIC_SYMBOL_ALIASES_VALIDATED; ANDROID_RUNTIME_NOT_TESTED",
        "reference_apk_sha256": REFERENCE_APK_SHA256,
        "target_so_sha256": TARGET_LIB_SHA256,
        "patched_so_sha256": sha256(output.read_bytes()),
        "text_sha256_before_and_after": original_text_hash,
        "mapped_count": len(mapping),
        "inference": "Same export order and all 239 function sizes; two extra target APIs before bounded-array; not a runtime guarantee",
        "aliases": mapping,
    }
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-apk", type=Path, required=True)
    parser.add_argument("--target-so", type=Path, required=True)
    parser.add_argument("--output-so", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    result = build_aliases(args.reference_apk, args.target_so, args.output_so, args.report)
    print("IL2CPP_EXPORT_ALIASES_OK count=%s sha256=%s" % (result["mapped_count"], result["patched_so_sha256"]))
