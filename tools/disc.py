"""Detect, list and extract a game disc image: Xbox 360 (XDVDFS) or PS3 (ISO 9660).

Usage:
  python disc.py detect  <image.iso>
  python disc.py list    <image.iso> [--dkey FILE|HEX]
  python disc.py extract <image.iso> <out_dir> [--dkey FILE|HEX] [path_prefix ...]

PS3 images from your own disc are encrypted. Give the disc key you dumped with the
disc (a .dkey file or 32 hex digits), or pass an image that is already decrypted.
"""
import os
import struct
import sys

SECTOR = 2048

# ---------------------------------------------------------------- detection

X360_MAGIC = b"MICROSOFT*XBOX*MEDIA"
# Game partition offsets: plain XDVDFS, XGD3, XGD2, XGD1
X360_PARTITIONS = [0x0, 0x2080000, 0xFD90000, 0x18300000]


def detect(f):
    """Return ("x360", info) or ("ps3", info)."""
    for base in X360_PARTITIONS:
        f.seek(base + 32 * SECTOR)
        if f.read(20) == X360_MAGIC:
            root_sector, root_size = struct.unpack("<II", f.read(8))
            return "x360", {"base": base, "root_sector": root_sector, "root_size": root_size}
    f.seek(SECTOR)
    s1 = f.read(SECTOR)
    if s1.startswith(b"PlayStation3"):
        title = s1[0x10:0x20].split(b"\0")[0].decode("ascii", "replace").strip()
        return "ps3", {"title_id": title, "plain": ps3_plain_regions(f)}
    raise SystemExit("unknown disc image: neither Xbox 360 nor PS3")


# ---------------------------------------------------------------- Xbox 360

def x360_walk(f, base, sector, size, prefix=""):
    if size == 0:
        return
    f.seek(base + sector * SECTOR)
    data = f.read(size)
    stack, seen = [0], set()
    while stack:
        off = stack.pop()
        if off in seen or off + 14 > len(data):
            continue
        seen.add(off)
        left, right, start, length, attr, nlen = struct.unpack_from("<HHIIBB", data, off)
        if left == 0xFFFF and right == 0xFFFF:
            continue
        name = data[off + 14: off + 14 + nlen].decode("latin-1")
        if left:
            stack.append(left * 4)
        if right:
            stack.append(right * 4)
        path = prefix + name
        if attr & 0x10:
            yield from x360_walk(f, base, start, length, path + "/")
        else:
            yield path, base + start * SECTOR, length


# ---------------------------------------------------------------- PS3

def ps3_plain_regions(f):
    """Sector 0 lists the unencrypted ranges; everything between them is encrypted."""
    f.seek(0)
    s0 = f.read(SECTOR)
    count = struct.unpack(">I", s0[:4])[0]
    bounds = struct.unpack_from(f">{2 * count}I", s0, 8)
    return [(bounds[i], bounds[i + 1]) for i in range(0, 2 * count, 2)]


def load_dkey(arg):
    if arg is None:
        return None
    if os.path.isfile(arg):
        with open(arg, "rb") as k:
            raw = k.read()
        text = raw.decode("ascii", "ignore").strip()
        key = bytes.fromhex(text) if len(text) == 32 else raw[:16]
    else:
        key = bytes.fromhex(arg)
    if len(key) != 16:
        raise SystemExit("disc key must be 16 bytes (32 hex digits)")
    return key


class Ps3Image:
    """Reads sectors of a PS3 disc image, decrypting the encrypted ranges."""

    def __init__(self, f, plain, key):
        self.f, self.plain, self.key = f, plain, key

    def is_plain(self, lba):
        return any(a <= lba <= b for a, b in self.plain)

    def read_sector(self, lba):
        self.f.seek(lba * SECTOR)
        data = self.f.read(SECTOR)
        if self.is_plain(lba):
            return data
        if self.key is None:
            raise SystemExit("this PS3 image is encrypted: pass --dkey with your disc key")
        from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
        iv = b"\0" * 12 + struct.pack(">I", lba)
        dec = Cipher(algorithms.AES(self.key), modes.CBC(iv)).decryptor()
        return dec.update(data) + dec.finalize()

    def read(self, lba, size):
        out = bytearray()
        while len(out) < size:
            out += self.read_sector(lba)
            lba += 1
        return bytes(out[:size])


def ps3_walk(img, lba, size, prefix=""):
    data = img.read(lba, size)
    off = 0
    while off < len(data):
        rec_len = data[off]
        if rec_len == 0:  # records never cross a sector: skip to the next one
            off = (off // SECTOR + 1) * SECTOR
            continue
        extent, length = struct.unpack_from("<I", data, off + 2)[0], struct.unpack_from("<I", data, off + 10)[0]
        flags, nlen = data[off + 25], data[off + 32]
        name = data[off + 33: off + 33 + nlen]
        off += rec_len
        if name in (b"\0", b"\1"):
            continue
        name = name.decode("ascii", "replace").split(";")[0]
        if flags & 0x02:
            yield from ps3_walk(img, extent, length, prefix + name + "/")
        else:
            yield prefix + name, extent, length


def ps3_entries(img):
    pvd = img.read_sector(16)
    if pvd[1:6] != b"CD001":
        raise SystemExit("no ISO 9660 volume (wrong disc key?)")
    root = pvd[156:156 + 34]
    lba, size = struct.unpack_from("<I", root, 2)[0], struct.unpack_from("<I", root, 10)[0]
    return list(ps3_walk(img, lba, size))


# ---------------------------------------------------------------- commands

def parse_args(argv):
    dkey, rest = None, []
    it = iter(argv)
    for a in it:
        if a == "--dkey":
            dkey = next(it)
        else:
            rest.append(a)
    return dkey, rest


def files(f, kind, info, dkey):
    """Yield (path, [(read_fn, size), ...]); a PS3 file over 4 GiB spans several extents."""
    if kind == "x360":
        for path, offset, size in x360_walk(f, info["base"], info["root_sector"], info["root_size"]):
            yield path, [(lambda o=offset, n=size: chunks_raw(f, o, n), size)]
        return
    img = Ps3Image(f, info["plain"], load_dkey(dkey))
    grouped = {}
    for path, lba, size in ps3_entries(img):
        grouped.setdefault(path, []).append((lambda l=lba, n=size: chunks_ps3(img, l, n), size))
    yield from grouped.items()


def chunks_raw(f, offset, size, step=1 << 22):
    f.seek(offset)
    while size:
        b = f.read(min(size, step))
        size -= len(b)
        yield b


def chunks_ps3(img, lba, size, step_sectors=2048):
    while size:
        n = min(size, step_sectors * SECTOR)
        b = img.read(lba, n)
        lba += step_sectors
        size -= len(b)
        yield b


def main():
    dkey, args = parse_args(sys.argv[1:])
    cmd, iso = args[0], args[1]
    with open(iso, "rb") as f:
        kind, info = detect(f)
        if cmd == "detect":
            if kind == "x360":
                print(f"Xbox 360 disc, game partition at 0x{info['base']:X}")
            else:
                print(f"PS3 disc {info['title_id']}, {len(info['plain'])} unencrypted ranges")
            return
        entries = list(files(f, kind, info, dkey))
        if cmd == "list":
            total = 0
            for path, parts in sorted(entries):
                size = sum(n for _, n in parts)
                total += size
                print(f"{size:>12}  {path}")
            print(f"# {kind}: {len(entries)} files, {total / 2**30:.2f} GiB")
            return
        out, prefixes = args[2], [p.lower() for p in args[3:]]
        for path, parts in entries:
            if prefixes and not any(path.lower().startswith(p) for p in prefixes):
                continue
            dst = os.path.join(out, *path.split("/"))
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, "wb") as o:
                for read, _ in parts:
                    for b in read():
                        o.write(b)
            print(f"extracted {path}")


if __name__ == "__main__":
    main()
