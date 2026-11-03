# Regenerate Scratchpad/Assets/Scratchpad.ico with: python3 tools/make_icon.py [--preview DIR]
"""Draws the Scratchpad app icon (a yellow Post-it with a folded corner and three ink lines).

Stdlib only. Each size is drawn directly (no downscaling) so small tray sizes stay crisp.
Sizes up to 48 are stored as 32bpp DIBs, because .NET Framework's System.Drawing.Icon (used for
the tray icon) is unreliable with PNG frames. 256 is stored as PNG, as Windows expects.
"""
import math
import os
import struct
import sys
import zlib

SIZES = [16, 20, 24, 32, 40, 48, 256]
SUPERSAMPLE = 4

PAPER = (0xFF, 0xE0, 0x66)
FLAP = (0xF0, 0xC4, 0x19)
OUTLINE = (0xA8, 0x8A, 0x10)
INK = (0x5C, 0x4F, 0x1A)


def draw(s):
    """Returns s*s RGBA tuples, row by row from the top."""
    m = max(1.0, 0.0625 * s)
    f = 0.28 * s
    t = max(1.0, s / 32.0)
    far = s - m
    cut = 2 * far - f  # points with x + y > cut are past the fold
    flap_edge = far - f

    # Ink lines, snapped to whole pixels at small sizes so they don't blur.
    thickness = 0.07 * s
    lines = []
    for y, x_end in ((0.33, 0.76), (0.50, 0.76), (0.67, 0.56)):
        top = y * s - thickness / 2
        th = thickness
        if s <= 48:
            th = max(1.0, round(thickness))
            top = round(y * s - th / 2)
        lines.append((0.24 * s, x_end * s, top, top + th))

    def sample(x, y):
        if not (m <= x <= far and m <= y <= far) or x + y > cut:
            return None
        cut_distance = (cut - (x + y)) / math.sqrt(2)
        in_flap = x >= flap_edge and y >= flap_edge
        if in_flap:
            if x - flap_edge < t or y - flap_edge < t or cut_distance < t:
                return OUTLINE
            return FLAP
        if min(x - m, far - x, y - m, far - y) < t or cut_distance < t:
            return OUTLINE
        for x0, x1, y0, y1 in lines:
            if x0 <= x <= x1 and y0 <= y <= y1:
                return INK
        return PAPER

    pixels = []
    n = SUPERSAMPLE
    for py in range(s):
        for px in range(s):
            r = g = b = a = 0
            for sy in range(n):
                for sx in range(n):
                    c = sample(px + (sx + 0.5) / n, py + (sy + 0.5) / n)
                    if c is not None:
                        r += c[0]
                        g += c[1]
                        b += c[2]
                        a += 1
            if a == 0:
                pixels.append((0, 0, 0, 0))
            else:
                # Colour averaged over covered samples only (un-premultiplied), alpha = coverage.
                pixels.append((r // a, g // a, b // a, round(255 * a / (n * n))))
    return pixels


def png(s, pixels):
    raw = b"".join(b"\x00" + b"".join(bytes(p) for p in pixels[y * s:(y + 1) * s]) for y in range(s))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", s, s, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def dib(s, pixels):
    header = struct.pack("<IiiHHIIiiII", 40, s, 2 * s, 1, 32, 0, 0, 0, 0, 0, 0)
    colour = b"".join(
        bytes((p[2], p[1], p[0], p[3])) for y in reversed(range(s)) for p in pixels[y * s:(y + 1) * s])
    stride = (s + 31) // 32 * 4
    mask = bytearray()
    for y in reversed(range(s)):
        row = bytearray(stride)
        for x in range(s):
            if pixels[y * s + x][3] == 0:
                row[x // 8] |= 0x80 >> (x % 8)
        mask += row
    return header + colour + bytes(mask)


def main():
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    out = os.path.join(root, "Scratchpad", "Assets", "Scratchpad.ico")
    preview_dir = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None

    images = []
    for s in SIZES:
        pixels = draw(s)
        images.append(png(s, pixels) if s >= 256 else dib(s, pixels))
        if preview_dir:
            with open(os.path.join(preview_dir, "icon-%d.png" % s), "wb") as fh:
                fh.write(png(s, pixels))

    data = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for s, image in zip(SIZES, images):
        data += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(image), offset)
        offset += len(image)
    data += b"".join(images)

    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "wb") as fh:
        fh.write(data)
    print("wrote %s (%d bytes)" % (out, len(data)))


if __name__ == "__main__":
    main()
