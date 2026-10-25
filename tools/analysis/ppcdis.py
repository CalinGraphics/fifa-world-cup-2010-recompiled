"""Disassemble PowerPC (Xbox 360) code from an image written by xeximage.py.

Usage:
  python ppcdis.py <image.bin> <base> <addr> [count=40] [before=16]
  python ppcdis.py <image.bin> <base> --sections
  python ppcdis.py <image.bin> <base> --pdata <addr>     (which .pdata function covers addr)
"""
import struct
import sys

import capstone


def sections(img):
    pe = struct.unpack_from("<I", img, 0x3C)[0]
    n = struct.unpack_from("<H", img, pe + 6)[0]
    opt = struct.unpack_from("<H", img, pe + 20)[0]
    s = pe + 24 + opt
    out = []
    for i in range(n):
        name = img[s + i * 40: s + i * 40 + 8].rstrip(b"\0").decode()
        vsize, va = struct.unpack_from("<II", img, s + i * 40 + 8)
        out.append((name, va, vsize))
    return out


def pdata(img, base):
    for name, va, size in sections(img):
        if name == ".pdata":
            for o in range(va, va + size, 8):
                start, word = struct.unpack_from(">II", img, o)
                if start == 0:
                    break
                yield start, start + ((word >> 8) & 0x3FFFFF) * 4


def main():
    img = open(sys.argv[1], "rb").read()
    base = int(sys.argv[2], 16)
    if sys.argv[3] == "--sections":
        for name, va, size in sections(img):
            print(f"{name:8} 0x{base + va:08X}-0x{base + va + size:08X}")
        return
    if sys.argv[3] == "--pdata":
        a = int(sys.argv[4], 16)
        prev = None
        for s, e in pdata(img, base):
            if s <= a < e:
                print(f"in pdata function 0x{s:08X}-0x{e:08X}")
                return
            if s > a:
                print(f"no pdata entry: previous 0x{prev[0]:08X}-0x{prev[1]:08X}, next 0x{s:08X}-0x{e:08X}")
                return
            prev = (s, e)
        return
    addr = int(sys.argv[3], 16)
    count = int(sys.argv[4]) if len(sys.argv) > 4 else 40
    before = int(sys.argv[5]) if len(sys.argv) > 5 else 16
    start = addr - before * 4
    md = capstone.Cs(capstone.CS_ARCH_PPC, capstone.CS_MODE_32 | capstone.CS_MODE_BIG_ENDIAN)
    for i in range(count + before):
        a = start + i * 4
        word = img[a - base: a - base + 4]
        ins = next(md.disasm(word, a), None)
        text = f"{ins.mnemonic} {ins.op_str}" if ins else f".long 0x{word.hex()}"
        mark = ">>" if a == addr else "  "
        print(f"{mark} {a:08X}: {word.hex()}  {text}")


main()
