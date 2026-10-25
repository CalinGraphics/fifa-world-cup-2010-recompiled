"""Print the basic layout of an XEX2 file: base address, entry, encryption, compression, imports.

Usage: python xexinfo.py <file.xex>
"""
import struct
import sys

HEADER_NAMES = {
    0x000002FF: "resource_info", 0x000003FF: "file_format_info", 0x00010001: "original_base_address",
    0x00010100: "entry_point", 0x00010201: "image_base_address", 0x000103FF: "import_libraries",
    0x000183FF: "execution_info", 0x00040006: "game_ratings", 0x00030000: "system_flags",
    0x000080FF: "bounding_path", 0x00020104: "tls_info", 0x00020200: "default_stack_size",
    0x00040310: "lan_key", 0x00020301: "default_heap_size",
}


def u32(b, o):
    return struct.unpack_from(">I", b, o)[0]


def main(path):
    x = open(path, "rb").read()
    assert x[:4] == b"XEX2", "not XEX2"
    flags, pe_off, _, sec_off, count = struct.unpack_from(">IIIII", x, 4)
    print(f"module_flags=0x{flags:X} pe_data=0x{pe_off:X} security=0x{sec_off:X}")
    for i in range(count):
        key, val = struct.unpack_from(">II", x, 0x18 + i * 8)
        name = HEADER_NAMES.get(key, f"0x{key:08X}")
        if key == 0x000003FF:
            enc, comp = struct.unpack_from(">HH", x, val + 4)
            print(f"  {name}: encryption={enc} compression={comp} (0 none,1 basic,2 normal/LZX,3 delta)")
        elif key == 0x000183FF:
            media, ver, base_ver, tid = struct.unpack_from(">IIII", x, val)
            print(f"  {name}: title_id=0x{tid:08X} version=0x{ver:08X}")
        elif key == 0x000103FF:
            size = u32(x, val)
            blob = x[val: val + size]
            names = [s.decode() for s in blob[12:].split(b"\0") if s and s.isascii() and s.replace(b".", b"").replace(b"_", b"").isalnum()]
            print(f"  {name}: {names[:12]}")
        else:
            print(f"  {name}: 0x{val:08X}")
    image_size = u32(x, sec_off + 4)
    load_addr = u32(x, sec_off + 0x110)
    print(f"image_size=0x{image_size:X} load_address=0x{load_addr:08X}")


main(sys.argv[1])
