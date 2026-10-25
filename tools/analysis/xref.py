"""Find code that references a guest address through a lis + addi/load/store pair.

Usage: python xref.py <module.xex> <address> [--window 8]
Prints each reference with the instruction that uses it and the .pdata function around it.
"""
import argparse
import bisect
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xeximage  # noqa: E402

D_FORM = {14: "addi", 24: "ori", 32: "lwz", 33: "lwzu", 34: "lbz", 36: "stw", 37: "stwu",
          38: "stb", 40: "lhz", 42: "lha", 44: "sth", 48: "lfs", 50: "lfd", 52: "stfs",
          54: "stfd", 58: "ld", 62: "std"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("xex")
    ap.add_argument("address", type=lambda s: int(s, 16))
    ap.add_argument("--window", type=int, default=8)
    a = ap.parse_args()
    base, img = xeximage.load(a.xex)

    pe = struct.unpack_from("<I", img, 0x3C)[0]
    n = struct.unpack_from("<H", img, pe + 6)[0]
    opt = struct.unpack_from("<H", img, pe + 20)[0]
    s0 = pe + 24 + opt
    code, pdata = [], []
    for i in range(n):
        o = s0 + i * 40
        name = img[o: o + 8].rstrip(b"\0")
        vsize, va = struct.unpack_from("<II", img, o + 8)
        if struct.unpack_from("<I", img, o + 36)[0] & 0x20000000:
            code.append((va, va + vsize))
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
            return f"sub_{pdata[i][0]:08X}"
        return "(no pdata)"

    target = a.address
    for lo, hi in code:
        for off in range(lo, hi - 4, 4):
            w = struct.unpack_from(">I", img, off)[0]
            if w >> 26 != 15 or (w >> 16) & 0x1F != 0:
                continue
            rd, hi16 = (w >> 21) & 0x1F, w & 0xFFFF
            for k in range(1, a.window):
                w2 = struct.unpack_from(">I", img, off + 4 * k)[0]
                op = w2 >> 26
                if op not in D_FORM or (w2 >> 16) & 0x1F != rd:
                    continue
                d = w2 & 0xFFFF
                if op == 24:
                    v = (hi16 << 16) | d
                else:
                    v = ((hi16 << 16) + (d - 0x10000 if d & 0x8000 else d)) & 0xFFFFFFFF
                if op in (58, 62):
                    v &= ~3
                if v == target:
                    at = base + off + 4 * k
                    rt = (w2 >> 21) & 0x1F
                    print(f"0x{at:08X}  {D_FORM[op]:5} r{rt}  in {func_of(at)}")
                break


main()
