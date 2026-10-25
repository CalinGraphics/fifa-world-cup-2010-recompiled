"""Who calls a function: direct bl/b sites and pointers to it in data, as a tree.

Usage: python callers.py <module.xex> <address> [--depth 3]
"""
import argparse
import bisect
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xeximage  # noqa: E402


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("xex")
    ap.add_argument("address", type=lambda s: int(s, 16))
    ap.add_argument("--depth", type=int, default=3)
    a = ap.parse_args()
    base, img = xeximage.load(a.xex)

    pe = struct.unpack_from("<I", img, 0x3C)[0]
    n = struct.unpack_from("<H", img, pe + 6)[0]
    opt = struct.unpack_from("<H", img, pe + 20)[0]
    s0 = pe + 24 + opt
    code, data, pdata = [], [], []
    for i in range(n):
        o = s0 + i * 40
        name = img[o: o + 8].rstrip(b"\0")
        vsize, va = struct.unpack_from("<II", img, o + 8)
        if struct.unpack_from("<I", img, o + 36)[0] & 0x20000000:
            code.append((va, va + vsize))
        elif name not in (b".pdata", b".reloc"):
            data.append((va, va + vsize))
        if name == b".pdata":
            for p in range(va, va + vsize, 8):
                st, w = struct.unpack_from(">II", img, p)
                if st:
                    pdata.append((st, st + ((w >> 8) & 0x3FFFFF) * 4))
    pdata.sort()
    starts = [p[0] for p in pdata]

    def func_of(addr):
        i = bisect.bisect_right(starts, addr) - 1
        if i >= 0 and pdata[i][0] <= addr < pdata[i][1]:
            return pdata[i][0]
        return None

    calls = {}
    for lo, hi in code:
        for off in range(lo, hi, 4):
            x = struct.unpack_from(">I", img, off)[0]
            if x >> 26 == 18 and not x & 2:
                d = x & 0x3FFFFFC
                if d & 0x2000000:
                    d -= 0x4000000
                calls.setdefault(base + off + d, []).append((base + off, "bl" if x & 1 else "b"))
    ptrs = {}
    for lo, hi in data:
        for off in range(lo, hi - 3, 4):
            v = struct.unpack_from(">I", img, off)[0]
            if base <= v < base + len(img):
                ptrs.setdefault(v, []).append(base + off)

    seen = set()

    def walk(target, depth, indent):
        if depth < 0 or target in seen:
            return
        seen.add(target)
        for site, kind in calls.get(target, []):
            f = func_of(site)
            label = f"sub_{f:08X}" if f else "(no pdata)"
            print(f"{indent}{kind} from 0x{site:08X} in {label}")
            if f:
                walk(f, depth - 1, indent + "  ")
        for p in ptrs.get(target, []):
            print(f"{indent}pointer at 0x{p:08X}")

    print(f"sub_{a.address:08X}")
    walk(a.address, a.depth, "  ")


main()
