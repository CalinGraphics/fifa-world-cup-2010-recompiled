"""Run ReXGlue codegen and fix "target not in any function" errors automatically.

For every unresolved branch target it adds a function seed to the module's
config/<module>.toml under the "--- autofix ---" marker (the analyser finds the bounds). Repeats until codegen passes.

Usage: python autofix_codegen.py --rexglue <rexglue.exe> --project <x360 dir> --game <game dir>
                                 [--max-rounds 50]
"""
import argparse
import os
import re
import struct
import subprocess
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

MARKER = "# --- autofix ---"
BLR, BCTR = 0x4E800020, 0x4E800420
MAX_SIZE = 0x400

MODULES = {"default": "default.xex", "fifadllzf": "fifadllzf.xex"}


def load_image(game, module, cache):
    if module not in cache:
        import xeximage
        cache[module] = xeximage.load(os.path.join(game, MODULES[module]))
    return cache[module]


def pdata_starts(img):
    pe = struct.unpack_from("<I", img, 0x3C)[0]
    n = struct.unpack_from("<H", img, pe + 6)[0]
    opt = struct.unpack_from("<H", img, pe + 20)[0]
    s = pe + 24 + opt
    starts = set()
    for i in range(n):
        if img[s + i * 40: s + i * 40 + 8].rstrip(b"\0") == b".pdata":
            vsize, va = struct.unpack_from("<II", img, s + i * 40 + 8)
            for o in range(va, va + vsize, 8):
                a = struct.unpack_from(">I", img, o)[0]
                if a:
                    starts.add(a)
    return starts


def function_size(img, base, starts, addr):
    a = addr
    while a - addr < MAX_SIZE:
        w = struct.unpack_from(">I", img, a - base)[0]
        a += 4
        if w in (BLR, BCTR):
            return a - addr
        if w >> 26 == 18 and (w & 3) == 0:  # b (not bl/ba)
            return a - addr
        if a in starts:
            return a - addr
    return None


def add_entries(cfg, entries):
    text = open(cfg, encoding="utf-8").read()
    lines = []
    for addr, size in sorted(entries.items()):
        key = f"0x{addr:08X}"
        if re.search(rf"^{key}\s*=", text, re.M):
            continue
        body = f"size = 0x{size:X}" if size else f'name = "sub_{addr:08X}"'
        lines.append(f"{key} = {{ {body} }}")
    if not lines:
        return 0
    if MARKER not in text:
        text = text.rstrip() + "\n" + MARKER + "\n"
    text = text.rstrip() + "\n" + "\n".join(lines) + "\n"
    open(cfg, "w", encoding="utf-8", newline="\n").write(text)
    return len(lines)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--rexglue", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--game", required=True)
    ap.add_argument("--max-rounds", type=int, default=50)
    args = ap.parse_args()
    cache = {}
    for rnd in range(1, args.max_rounds + 1):
        r = subprocess.run([args.rexglue, "--log-level", "info", "codegen", "fifawc10_manifest.toml"],
                           cwd=args.project, capture_output=True, text=True, errors="replace")
        out = r.stdout + r.stderr
        if r.returncode == 0:
            print(f"round {rnd}: codegen OK")
            return 0
        m = re.search(r"Analysis failed for '([^']+)'", out)
        targets = [int(t, 16) for t in re.findall(r"^\s+0x([0-9A-Fa-f]{8}) from 0x[0-9A-Fa-f]+: .*target not in any function", out, re.M)]
        if not m or not targets:
            print(f"round {rnd}: codegen failed with something autofix doesn't handle:")
            print("\n".join(out.splitlines()[-30:]))
            return 1
        module = m.group(1)
        base, img = load_image(args.game, module, cache)
        starts = pdata_starts(img)
        # Seed only the start; the analyser finds the end (a loop's closing `b` is not one).
        entries = {t: None for t in targets}
        n = add_entries(os.path.join(args.project, "config", f"{module}.toml"), entries)
        print(f"round {rnd}: {module}: added {n} of {len(targets)} functions")
        if n == 0:
            print("no progress, stopping")
            print("\n".join(out.splitlines()[-30:]))
            return 1
    return 1


sys.exit(main())
