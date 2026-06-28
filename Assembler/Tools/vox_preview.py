#!/usr/bin/env python3
"""
Standalone MagicaVoxel .vox previewer — renders a .vox to a PNG with simple, NEUTRAL
isometric shading so the voxel *albedo* is shown faithfully (no coloured lights, no
ambient tint). Use it to check what a converted model actually contains, independent
of any engine's lighting/material setup.

Usage:
    python3 Tools/vox_preview.py <file.vox> [out.png]

Prints colour stats and writes <file>.preview.png (or the given out path).
"""
import struct
import sys
import math
import collections
from PIL import Image, ImageDraw


def parse_vox(path):
    buf = open(path, "rb").read()
    assert buf[:4] == b"VOX ", "not a .vox file"
    palette = None
    voxels = None

    def walk(o, end):
        nonlocal palette, voxels
        while o < end:
            cid = buf[o:o + 4]
            n, m = struct.unpack("<ii", buf[o + 4:o + 12])
            o += 12
            content = buf[o:o + n]
            if cid == b"RGBA":
                palette = [tuple(content[i * 4:i * 4 + 4]) for i in range(256)]
            elif cid == b"XYZI":
                cnt = struct.unpack("<i", content[:4])[0]
                voxels = [tuple(content[4 + i * 4:4 + i * 4 + 4]) for i in range(cnt)]
            elif cid == b"MAIN":
                walk(o + n, o + n + m)
            o += n + m

    walk(8, len(buf))
    if palette is None:
        # default greyscale-ish; shouldn't happen for our writer
        palette = [(200, 200, 200, 255)] * 256
    return voxels, palette


# Isometric projection: yaw 45° about Z (up), then pitch ~35.26° → true 2:1 iso.
_A = math.radians(45.0)
_B = math.radians(35.264)


def project(x, y, z):
    x1 = x * math.cos(_A) - y * math.sin(_A)
    y1 = x * math.sin(_A) + y * math.cos(_A)
    z1 = z
    y2 = y1 * math.cos(_B) - z1 * math.sin(_B)
    z2 = y1 * math.sin(_B) + z1 * math.cos(_B)
    return x1, -z2, y2          # screen x, screen y (down), depth (into screen)


# Cube faces: (normal, 4 corner offsets) in voxel-local {0,1} coords.
_FACES = [
    ((1, 0, 0), [(1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1)]),
    ((-1, 0, 0), [(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)]),
    ((0, 1, 0), [(0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0)]),
    ((0, -1, 0), [(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)]),
    ((0, 0, 1), [(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)]),
    ((0, 0, -1), [(0, 0, 0), (0, 1, 0), (1, 1, 0), (1, 0, 0)]),
]

# Neutral directional light (white) + ambient. Top-ish, slightly toward viewer.
_L = (0.35, -0.45, 0.82)
_Llen = math.sqrt(sum(c * c for c in _L))
_L = tuple(c / _Llen for c in _L)
_AMBIENT = 0.45


def shade(normal):
    d = max(0.0, sum(n * l for n, l in zip(normal, _L)))
    return _AMBIENT + (1.0 - _AMBIENT) * d


def classify(r, g, b):
    if r - g > 20 and b - g > 5 and r < 210:
        return "purple"
    if g - r > 20 and g - b > 20:
        return "green"
    if abs(r - g) < 18 and abs(g - b) < 18:
        return "grey/white"
    if b - r > 15 and b - g > 10:
        return "blue"
    return "other-tint"


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    path = sys.argv[1]
    out = sys.argv[2] if len(sys.argv) > 2 else path.rsplit(".", 1)[0] + ".preview.png"

    voxels, palette = parse_vox(path)
    occ = set((v[0], v[1], v[2]) for v in voxels)

    # ---- colour stats (faithful albedo) ----
    cls = collections.Counter()
    alpha_lt255 = 0
    for x, y, z, idx in voxels:
        r, g, b, a = palette[idx - 1]
        cls[classify(r, g, b)] += 1
        if a < 255:
            alpha_lt255 += 1
    tot = len(voxels)
    print(f"{path}")
    print(f"  voxels: {tot:,}   distinct palette idx: {len(set(v[3] for v in voxels))}")
    print(f"  voxels with palette alpha<255: {alpha_lt255} ({100*alpha_lt255/tot:.1f}%)")
    for k, c in cls.most_common():
        print(f"    {k:<11} {100*c/tot:5.1f}%")

    # ---- collect visible surface faces ----
    faces = []  # (depth, polygon-points, fill rgb)
    for x, y, z, idx in voxels:
        r, g, b, a = palette[idx - 1]
        for normal, corners in _FACES:
            nx, ny, nz = normal
            if (x + nx, y + ny, z + nz) in occ:
                continue  # interior face, skip
            pts3 = [(x + cx, y + cy, z + cz) for cx, cy, cz in corners]
            proj = [project(*p) for p in pts3]
            depth = sum(p[2] for p in proj) / 4.0
            s = shade(normal)
            fill = (int(r * s), int(g * s), int(b * s))
            faces.append((depth, [(p[0], p[1]) for p in proj], fill))

    # ---- fit to image ----
    xs = [pt[0] for _, poly, _ in faces for pt in poly]
    ys = [pt[1] for _, poly, _ in faces for pt in poly]
    minx, maxx, miny, maxy = min(xs), max(xs), min(ys), max(ys)
    W, H, pad = 900, 900, 40
    sc = min((W - 2 * pad) / (maxx - minx), (H - 2 * pad) / (maxy - miny))
    ox = pad - minx * sc + (W - 2 * pad - (maxx - minx) * sc) / 2
    oy = pad - miny * sc + (H - 2 * pad - (maxy - miny) * sc) / 2

    img = Image.new("RGB", (W, H), (90, 90, 95))
    draw = ImageDraw.Draw(img)
    faces.sort(key=lambda f: f[0], reverse=True)  # far first
    for _, poly, fill in faces:
        sp = [(p[0] * sc + ox, p[1] * sc + oy) for p in poly]
        edge = tuple(max(0, c - 18) for c in fill)
        draw.polygon(sp, fill=fill, outline=edge)

    img.save(out)
    print(f"  wrote {out}  ({len(faces):,} faces)")


if __name__ == "__main__":
    main()
