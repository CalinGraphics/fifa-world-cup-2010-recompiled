"""Write the in-memory image of a retail XEX2 (as the console maps it at its base address).

Usage: python xeximage.py <file.xex> <out.bin> [path\\to\\lzxdec.exe]
As a module: xeximage.load(path) -> (base_address, image_bytes)
Handles encryption and none/basic/normal (LZX) compression. LZX needs lzxdec.exe
(tools/lzxdec, built with clang against libmspack); it is looked up next to this file,
then in the LZXDEC environment variable.
"""
import os
import struct
import subprocess
import sys
import tempfile

RETAIL_KEY = bytes.fromhex("20B185A59D28FDC340583FBB0896BF91")
HERE = os.path.dirname(os.path.abspath(__file__))


def u32(b, o):
    return struct.unpack_from(">I", b, o)[0]


def aes(key, data, ecb=False):
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    mode = modes.ECB() if ecb else modes.CBC(b"\0" * 16)
    d = Cipher(algorithms.AES(key), mode).decryptor()
    return d.update(data) + d.finalize()


def find_lzxdec():
    for p in (os.path.join(HERE, "lzxdec.exe"), os.environ.get("LZXDEC", "")):
        if p and os.path.isfile(p):
            return p
    raise SystemExit("lzxdec.exe not found: build tools/lzxdec or set LZXDEC")


def load(src, lzxdec=None):
    x = open(src, "rb").read()
    assert x[:4] == b"XEX2", "not XEX2"
    _, pe_off, _, sec, count = struct.unpack_from(">IIIII", x, 4)
    hdrs = {u32(x, 0x18 + i * 8): u32(x, 0x1C + i * 8) for i in range(count)}
    ffi = hdrs[0x000003FF]
    info_size = u32(x, ffi)
    enc, comp = struct.unpack_from(">HH", x, ffi + 4)
    image_size = u32(x, sec + 4)
    base = hdrs.get(0x00010201, u32(x, sec + 0x110))
    data = x[pe_off:]
    if enc == 1:
        session = aes(RETAIL_KEY, x[sec + 0x150: sec + 0x160], ecb=True)
        data = aes(session, data[: len(data) // 16 * 16])

    if comp == 0:
        return base, data[:image_size]
    if comp == 1:  # basic: (data_size, zero_size) pairs
        out, p = bytearray(), 0
        for i in range((info_size - 8) // 8):
            dsz, zsz = struct.unpack_from(">II", x, ffi + 8 + i * 8)
            out += data[p: p + dsz] + b"\0" * zsz
            p += dsz
        return base, bytes(out)
    if comp != 2:
        raise SystemExit(f"unsupported compression {comp}")

    # normal: chained blocks, each a list of LZX chunks
    window = u32(x, ffi + 8)
    block_size = u32(x, ffi + 12)
    stream, p = bytearray(), 0
    while block_size:
        nxt = p + block_size
        next_size = u32(data, p)
        q = p + 24  # next block size + SHA-1
        while True:
            n = struct.unpack_from(">H", data, q)[0]
            q += 2
            if n == 0:
                break
            stream += data[q: q + n]
            q += n
        p, block_size = nxt, next_size
    with tempfile.TemporaryDirectory() as t:
        a, b = os.path.join(t, "in.lzx"), os.path.join(t, "out.bin")
        open(a, "wb").write(stream)
        r = subprocess.run([lzxdec or find_lzxdec(), a, b, str(window), str(image_size)],
                           capture_output=True, text=True)
        if r.returncode:
            raise SystemExit(f"lzxdec failed ({r.returncode}): {r.stderr}")
        return base, open(b, "rb").read()


if __name__ == "__main__":
    base, image = load(sys.argv[1], sys.argv[3] if len(sys.argv) > 3 else None)
    open(sys.argv[2], "wb").write(image)
    print(f"base=0x{base:08X} size=0x{len(image):X}")
