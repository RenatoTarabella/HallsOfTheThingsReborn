"""
rendermap.py - Draws a map saved by the game (Halls$Debug) with the 1983 characters.

    python tools/rendermap.py Map1 out.png

The map is BigM: 128x128 character codes, rows 0-112 used (the floor in
columns 0-112, the sanctuary in 113-127). Colours as on the Spectrum.
"""
import glob
import os
import struct
import sys
import zlib

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
chars = open(os.path.join(ROOT, "riscos", "!Halls", "Chars,ffd"), "rb").read()


def colour(attr):
    v = 0xFF if attr & 0x40 else 0xD7
    ink = attr & 7
    return (v if ink & 2 else 0, v if ink & 4 else 0, v if ink & 1 else 0)


def load(name):
    paths = glob.glob(name + ",*") + ([name] if os.path.exists(name) else [])
    return open(paths[0], "rb").read()


def render(mp, out, rows=113):
    w, h = 128 * 8, rows * 8
    img = bytearray(w * h * 3)
    for r in range(rows):
        for c in range(128):
            code = mp[r * 128 + c]
            col = colour(chars[code])
            for y in range(8):
                bits = chars[code * 8 + y] if code >= 32 else 0
                for x in range(8):
                    if bits & (0x80 >> x):
                        o = ((r * 8 + y) * w + c * 8 + x) * 3
                        img[o:o + 3] = bytes(col)
    raw = b"".join(b"\0" + bytes(img[y * w * 3:(y + 1) * w * 3]) for y in range(h))

    def chunk(t, d):
        return struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    open(out, "wb").write(png)


if __name__ == "__main__":
    render(load(sys.argv[1]), sys.argv[2])
    print(sys.argv[2])
