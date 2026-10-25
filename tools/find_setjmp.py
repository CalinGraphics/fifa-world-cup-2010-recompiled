"""Find the CRT's setjmp/longjmp in a module, for the setjmp_address / longjmp_address
codegen options.

setjmp stores the non-volatile registers (r13/r14-r31, LR, CR, f14-f31) into the buffer
in r3 and returns 0; longjmp loads them back from r3 and returns r4 (or 1).
Usage: python find_setjmp.py <module.xex>
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xeximage  # noqa: E402

base, img = xeximage.load(sys.argv[1])
pe = struct.unpack_from("<I", img, 0x3C)[0]
n = struct.unpack_from("<H", img, pe + 6)[0]
opt = struct.unpack_from("<H", img, pe + 20)[0]
s0 = pe + 24 + opt
code = []
for i in range(n):
    o = s0 + i * 40
    vsize, va = struct.unpack_from("<II", img, o + 8)
    if struct.unpack_from("<I", img, o + 36)[0] & 0x20000000:
        code.append((va, va + vsize))

w = lambda off: struct.unpack_from(">I", img, off)[0]
STORES = (36, 62, 54)   # stw, std, stfd
LOADS = (32, 58, 50)    # lwz, ld, lfd
for lo, hi in code:
    for start in range(lo, hi - 4, 4):
        prev = w(start - 4)
        if prev not in (0x4E800020, 0) and prev >> 26 != 18:
            continue  # functions start after a return, padding or branch
        stores = loads = 0
        regs_s, regs_l = set(), set()
        end = None
        for k in range(80):
            x = w(start + 4 * k)
            op, rt, ra = x >> 26, (x >> 21) & 31, (x >> 16) & 31
            if x == 0x4E800020:
                end = start + 4 * k
                break
            if ra == 3 and op in STORES and rt >= 14:
                stores += 1
                regs_s.add((op, rt))
            if ra == 3 and op in LOADS and rt >= 14:
                loads += 1
                regs_l.add((op, rt))
        if end is None:
            continue
        gpr_s = {r for op, r in regs_s if op in (36, 62)}
        gpr_l = {r for op, r in regs_l if op in (32, 58)}
        if len(gpr_s) >= 17 and stores > loads:
            print(f"setjmp candidate:  0x{base + start:08X} ({len(gpr_s)} GPRs saved, ends 0x{base + end:08X})")
        if len(gpr_l) >= 17 and loads > stores:
            print(f"longjmp candidate: 0x{base + start:08X} ({len(gpr_l)} GPRs restored, ends 0x{base + end:08X})")
