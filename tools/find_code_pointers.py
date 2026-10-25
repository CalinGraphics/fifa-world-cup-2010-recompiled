"""Find functions the analyser can't see: code reached only through pointers.

Virtual methods, callbacks and small helpers often have no .pdata entry and are never
the target of a direct call, so static analysis misses them and the game later dies
with "Call to invalid or unregistered function". This scans the module for
addresses that point into code not covered by .pdata:
  - 32-bit values in the data sections (vtables, callback tables)
  - lis/addi and lis/ori pairs in the code
and keeps the ones that look like a function start (preceded by a return, a branch,
padding, or the end of a .pdata function). Each gets a size from the code itself.

Usage: python find_code_pointers.py <module.xex> <out.toml>
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import xeximage  # noqa: E402

BLR, BCTR = 0x4E800020, 0x4E800420
MAX_SIZE = 0x800


def sections(img):
    pe = struct.unpack_from("<I", img, 0x3C)[0]
    n = struct.unpack_from("<H", img, pe + 6)[0]
    opt = struct.unpack_from("<H", img, pe + 20)[0]
    s = pe + 24 + opt
    out = []
    for i in range(n):
        o = s + i * 40
        name = img[o: o + 8].rstrip(b"\0").decode("ascii", "replace")
        vsize, va = struct.unpack_from("<II", img, o + 8)
        flags = struct.unpack_from("<I", img, o + 36)[0]
        out.append((name, va, vsize, bool(flags & 0x20000000)))  # IMAGE_SCN_MEM_EXECUTE
    return out


def is_terminator(w):
    return w in (BLR, BCTR, 0) or (w >> 26 == 18 and (w & 1) == 0)  # b / ba, not bl


def main(xex, out_path):
    base, img = xeximage.load(xex)
    secs = sections(img)
    code = [(base + va, base + va + size) for name, va, size, x in secs if x]
    data = [(va, size) for name, va, size, x in secs if not x and name not in (".pdata", ".reloc")]
    pdata = []
    for name, va, size, _ in secs:
        if name == ".pdata":
            for o in range(va, va + size, 8):
                start, word = struct.unpack_from(">II", img, o)
                if start:
                    pdata.append((start, start + ((word >> 8) & 0x3FFFFF) * 4))
    pdata.sort()
    starts = {s for s, _ in pdata}
    ends = {e for _, e in pdata}

    import bisect
    pstarts = [s for s, _ in pdata]

    def covered(a):
        i = bisect.bisect_right(pstarts, a) - 1
        return i >= 0 and pdata[i][0] <= a < pdata[i][1]

    def region(a):
        """Index of the last .pdata function starting at or before a."""
        return bisect.bisect_right(pstarts, a) - 1

    def in_code(a):
        return a % 4 == 0 and any(lo <= a < hi for lo, hi in code)

    def word(a):
        return struct.unpack_from(">I", img, a - base)[0]

    def is_import_thunk(a):
        """Import table entry: two record words, then mtctr r11; bctr. The SDK owns these."""
        return word(a + 8) == 0x7D6903A6 and word(a + 12) == BCTR and word(a) >> 24 in (0, 1, 2)

    def looks_like_start(a):
        if not in_code(a) or covered(a) or word(a) == 0 or is_import_thunk(a):
            return False
        return a in ends or is_terminator(word(a - 4))

    found = {}
    for va, size in data:
        for o in range(va, va + size - 3, 4):
            v = struct.unpack_from(">I", img, o)[0]
            if looks_like_start(v):
                found.setdefault(v, f"data @0x{base + o:08X}")
    for lo, hi in code:
        for a in range(lo, hi - 4, 4):
            w = word(a)
            if w >> 26 != 15 or (w >> 16) & 0x1F != 0:  # lis rD, imm  (addis rD, 0, imm)
                continue
            rd, hi16 = (w >> 21) & 0x1F, w & 0xFFFF
            for k in range(1, 6):
                w2 = word(a + 4 * k)
                op = w2 >> 26
                if op in (14, 24) and (w2 >> 16) & 0x1F == rd:  # addi / ori rX, rD, lo
                    lo16 = w2 & 0xFFFF
                    v = ((hi16 << 16) + (lo16 - 0x10000 if op == 14 and lo16 & 0x8000 else lo16)) & 0xFFFFFFFF
                    # A lis/addi target inside the same .pdata-less stretch of code as the
                    # load is a computed jump within that function, not a new function.
                    same_gap = not covered(a) and region(a) == region(v)
                    if looks_like_start(v) and not same_gap:
                        found.setdefault(v, f"code lis @0x{a:08X}")
                    break

    allstarts = sorted(set(found) | starts)
    lines = []
    for a in sorted(found):
        nxt = allstarts[bisect.bisect_right(allstarts, a)] if bisect.bisect_right(allstarts, a) < len(allstarts) else a + MAX_SIZE
        size, p = None, a
        while p - a < MAX_SIZE and p < nxt:
            w = word(p)
            p += 4
            if w in (BLR, BCTR) or (w >> 26 == 18 and (w & 1) == 0):
                size = p - a
                break
        if size is None and nxt - a <= MAX_SIZE:
            size = nxt - a
        body = f'name = "sub_{a:08X}"'  # seed only: the analyser finds the bounds
        lines.append(f"0x{a:08X} = {{ {body} }} # {found[a]}")

    with open(out_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("# Generated by tools/find_code_pointers.py - functions reached only through\n")
        f.write("# pointers (vtables, callback tables, lis/addi loads). Regenerate, don't hand-edit.\n\n")
        f.write("[functions]\n")
        f.write("\n".join(lines) + "\n")
    print(f"{len(lines)} functions -> {out_path}")


main(sys.argv[1], sys.argv[2])
