#!/usr/bin/env python3
"""Add pre-mod startup markers to the exact MelonLoader net8 DLL in 0.50.6.

This diagnostic does not claim to fix a crash. It preserves original IL and
inserts synchronous MelonLogger.MsgDirect calls before the suspect stages.
It rejects every unknown loader hash and method layout.

Requires: python3 -m pip install dnfile dncil pefile
"""

import argparse
import hashlib
import struct
from pathlib import Path

import dnfile
import pefile
from dncil.cil.body import CilMethodBody
from dncil.cil.body.reader import CilMethodBodyReaderBytes


INPUT_SHA256 = "0d92b3d4850809c84a7dbd28466f2498728bd7742fe5bb0306cd198d4c3bff5f"
LOGGER_TOKEN = 0x060004D0  # MelonLogger.MsgDirect(string)
# Labels below already occur in the loader's user-string heap. A second,
# premature appearance in Latest.log is the diagnostic marker.
MARKERS = {
    0x060003AC: {  # MelonUtils.Setup(AppDomain)
        0x37: 0x7000A1C4,  # after WelcomeMessage
        0x77: 0x7000A1DC,  # before MelonHandler.Setup
        0x7C: 0x7000A1FE,  # before UnityInformationHandler.Setup
        0x81: 0x7000A21E,  # after UnityInformationHandler.Setup
    },
    0x06000C95: {  # UnityInformationHandler.Setup()
        0x0C: 0x70000C7D,  # entry
        0x34: 0x70000CA5,  # before ReadGameInfo (assets)
        0x3F: 0x70000CBD,  # after ReadGameInfo and UnloadAll
        0x73: 0x70000C95,  # after ReadVersionFallback
    },
}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def align(value, boundary):
    return (value + boundary - 1) // boundary * boundary


def method_body(pe, token):
    row = pe.net.mdtables.MethodDef.rows[(token & 0x00FFFFFF) - 1]
    offset = pe.get_offset_from_rva(row.Rva)
    body = CilMethodBody(CilMethodBodyReaderBytes(pe.__data__[offset:]))
    return row, offset, body


def splice_method(pe, token, inserts):
    row, offset, body = method_body(pe, token)
    if body.header_size != 12 or not body.flags.FatFormat:
        raise ValueError(f"Unexpected method header: {token:#x}")
    starts = {ins.offset for ins in body.instructions}
    if set(inserts) - starts:
        raise ValueError(f"Markers are not at IL instruction boundaries: {token:#x}")
    original = pe.__data__[offset:offset + body.size]
    code = original[12:12 + body.code_size]
    insertion = {at: b"\x72" + struct.pack("<I", string) + b"\x28" + struct.pack("<I", LOGGER_TOKEN)
                 for at, string in inserts.items()}
    result = bytearray()
    where = {}
    for ins in body.instructions:
        result.extend(insertion.get(ins.offset, b""))
        where[ins.offset] = 12 + len(result)
        result.extend(code[ins.offset - 12:ins.offset - 12 + ins.size])
    if len(result) != len(code) + sum(map(len, insertion.values())):
        raise ValueError("The input IL has uncovered bytes")
    for ins in body.instructions:
        if ins.opcode.name == "switch":
            raise ValueError("Switch relocation needs a separate implementation")
        if not ins.opcode.name.startswith(("br", "leave", "beq", "bge", "bgt", "ble", "blt", "bne")):
            continue
        if not isinstance(ins.operand, int) or ins.operand not in where:
            raise ValueError(f"Unsupported branch target in {token:#x}: {ins}")
        origin = where[ins.offset]
        displacement = where[ins.operand] - (origin + ins.size)
        operand_at = origin - 12 + ins.size - (1 if ins.size == 2 else 4)
        if ins.opcode.name.endswith(".s"):
            struct.pack_into("b", result, operand_at, displacement)
        else:
            struct.pack_into("<i", result, operand_at, displacement)
    header = bytearray(original[:12])
    struct.pack_into("<I", header, 4, len(result))
    rebuilt = header + result
    if body.exception_handlers:
        original_eh = align(12 + body.code_size, 4)
        rebuilt.extend(b"\0" * (align(len(rebuilt), 4) - len(rebuilt)))
        rebuilt.extend(original[original_eh:])
    return row, bytes(rebuilt)


def instrument(source: Path, target: Path):
    original = source.read_bytes()
    if sha256(original) != INPUT_SHA256:
        raise ValueError("Unrecognized loader DLL; refuse to patch another build")
    pe = dnfile.dnPE(data=original)
    if pe.net.struct.StrongNameSignatureRva or pe.OPTIONAL_HEADER.DATA_DIRECTORY[4].Size:
        raise ValueError("Signed DLL or unexpected PE certificate")
    blobs = [(token, *splice_method(pe, token, offsets)) for token, offsets in MARKERS.items()]
    text, resource, reloc = pe.sections
    if text.Name.rstrip(b"\0") != b".text" or resource.Name.rstrip(b"\0") != b".rsrc":
        raise ValueError("Unexpected PE section layout")
    old_end = text.PointerToRawData + text.SizeOfRawData
    if resource.PointerToRawData != old_end:
        raise ValueError("Cannot shift a PE overlay or section gap")
    extension = bytearray()
    addresses = {}
    for token, row, blob in blobs:
        extension.extend(b"\0" * (align(len(extension), 4) - len(extension)))
        addresses[token] = text.VirtualAddress + text.SizeOfRawData + len(extension)
        extension.extend(blob)
    new_raw_size = align(text.SizeOfRawData + len(extension), pe.OPTIONAL_HEADER.FileAlignment)
    increase = new_raw_size - text.SizeOfRawData
    if text.VirtualAddress + new_raw_size > resource.VirtualAddress:
        raise ValueError("No unused address space before resource section")
    extension.extend(b"\0" * (increase - len(extension)))
    patched = bytearray(original[:old_end] + extension + original[old_end:])
    for token, row, blob in blobs:
        struct.pack_into("<I", patched, row.struct.get_field_absolute_offset("Rva"), addresses[token])
    struct.pack_into("<I", patched, text.get_field_absolute_offset("SizeOfRawData"), new_raw_size)
    struct.pack_into("<I", patched, text.get_field_absolute_offset("Misc_VirtualSize"),
                     text.SizeOfRawData + len(extension))
    for section in (resource, reloc):
        struct.pack_into("<I", patched, section.get_field_absolute_offset("PointerToRawData"),
                         section.PointerToRawData + increase)
    # PE checksum is computed after the body and section directory edits.
    checked = pefile.PE(data=bytes(patched), fast_load=True)
    struct.pack_into("<I", patched, checked.OPTIONAL_HEADER.get_field_absolute_offset("CheckSum"),
                     checked.generate_checksum())
    verified = dnfile.dnPE(data=bytes(patched))
    for token, _, blob in blobs:
        row, at, body = method_body(verified, token)
        if row.Rva != addresses[token] or body.code_size != struct.unpack_from("<I", blob, 4)[0]:
            raise ValueError("Rewritten method RVA or code length did not survive PE reload")
        for ins in body.instructions:
            if ins.opcode.name.startswith(("br", "leave", "beq", "bge", "bgt", "ble", "blt", "bne")):
                if ins.operand not in {item.offset for item in body.instructions}:
                    raise ValueError(f"Invalid branch in rewritten {token:#x}")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(patched)
    return {"before_sha256": sha256(original), "after_sha256": sha256(patched),
            "before_size": len(original), "after_size": len(patched),
            "modified_method_rva": {f"{k:#x}": f"{v:#x}" for k, v in addresses.items()}}


if __name__ == "__main__":
    import json
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    print(json.dumps(instrument(args.input, args.output), indent=2))
