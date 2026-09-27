"""Change only android:versionCode in a compiled AndroidManifest.xml.

ResXMLTree resource map identifies versionCode by Android framework ID
0x0101021b. This rejects unsupported or ambiguous layouts instead of
rebuilding the game's resources table.
"""

import struct


VERSION_CODE_ID = 0x0101021B


def patch_version(manifest, old_code, new_code):
    data = bytearray(manifest)
    if len(data) < 8 or struct.unpack_from("<HHI", data) != (3, 8, len(data)):
        raise ValueError("Not a complete binary Android XML document")

    offset = 8
    version_name_index = None
    matches = []
    while offset < len(data):
        if offset + 8 > len(data):
            raise ValueError("Truncated Android XML chunk")
        chunk_type, header_size, chunk_size = struct.unpack_from("<HHI", data, offset)
        if header_size < 8 or chunk_size < header_size or offset + chunk_size > len(data):
            raise ValueError("Invalid Android XML chunk size")
        if chunk_type == 0x0180:
            if (chunk_size - header_size) % 4:
                raise ValueError("Malformed Android resource map")
            resource_ids = struct.unpack_from(
                "<" + "I" * ((chunk_size - header_size) // 4), data, offset + header_size
            )
            indices = [index for index, value in enumerate(resource_ids) if value == VERSION_CODE_ID]
            if len(indices) != 1 or version_name_index is not None:
                raise ValueError("Ambiguous android:versionCode resource ID")
            version_name_index = indices[0]
        elif chunk_type == 0x0102:
            if header_size < 16 or offset + 36 > offset + chunk_size:
                raise ValueError("Malformed Android start element")
            attribute_start, attribute_size, count = struct.unpack_from("<HHH", data, offset + 24)
            if attribute_size != 20 or offset + 16 + attribute_start + count * attribute_size > offset + chunk_size:
                raise ValueError("Malformed Android attributes")
            for index in range(count):
                attribute = offset + 16 + attribute_start + index * attribute_size
                name = struct.unpack_from("<I", data, attribute + 4)[0]
                if name != version_name_index:
                    continue
                value_size, _, value_type, code = struct.unpack_from("<HBBI", data, attribute + 12)
                if value_size != 8 or value_type != 0x10 or code != old_code:
                    raise ValueError("Unexpected android:versionCode value or type")
                matches.append(attribute + 16)
        offset += chunk_size

    if offset != len(data) or version_name_index is None or len(matches) != 1:
        raise ValueError("android:versionCode must appear exactly once")
    struct.pack_into("<I", data, matches[0], new_code)
    return bytes(data)
