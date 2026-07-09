#!/usr/bin/env python3
"""
Standalone replica of the VoxelsFromMeshSpike COLOUR pipeline, for testing palette/
quantisation choices without booting Unity. Samples the OBJ surface the same way the
C# does (back-face cull → supersample outward triangles → per-voxel dominant), then
quantises to a palette and renders an isometric PNG under neutral light.

    python3 Tools/vox_sim.py <model.obj> --maxdim 48 --quant median --out /tmp/x.png
    --quant : median (median-cut) | p332 (old 3-3-2) | none
    --neutralise N : grey out voxels with chroma (max-min) < N before quantising
"""
import sys
import os
import math
import argparse
import collections
from PIL import Image, ImageDraw
import numpy as np


def load_obj(path):
    V, VT, F = [], [], []
    for line in open(path):
        t = line.split()
        if not t:
            continue
        if t[0] == "v":
            V.append([float(x) for x in t[1:4]])
        elif t[0] == "vt":
            VT.append([float(t[1]), float(t[2])])
        elif t[0] == "f":
            idx = []
            for c in t[1:]:
                p = c.split("/")
                vi = int(p[0])
                ti = int(p[1]) if len(p) > 1 and p[1] else 0
                idx.append((vi, ti))
            for k in range(1, len(idx) - 1):
                F.append((idx[0], idx[k], idx[k + 1]))
    return np.array(V), np.array(VT), F


def find_texture(obj):
    d = os.path.dirname(obj)
    for f in os.listdir(d):
        if f.lower().endswith(".png"):
            return os.path.join(d, f)
    return None


def sample_voxels(obj, maxdim):
    V, VT, F = load_obj(obj)
    tex = np.asarray(Image.open(find_texture(obj)).convert("RGB")).astype(float)
    TH, TW, _ = tex.shape
    mn, mx = V.min(0), V.max(0)
    vox = (mx - mn).max() / maxdim
    center = (mn + mx) / 2

    def rv(i, n):
        return i - 1 if i > 0 else n + i

    def texel(u, v):
        x = int(np.clip(u * TW, 0, TW - 1))
        y = int(np.clip((1 - v) * TH, 0, TH - 1))
        return tex[y, x]

    acc = collections.defaultdict(lambda: collections.defaultdict(lambda: [0, 0, 0, 0]))
    for tri in F:
        vs = np.array([V[rv(vi, len(V))] for vi, ti in tri])
        uvs = np.array([VT[rv(ti, len(VT))] if ti else [0, 0] for vi, ti in tri])
        nrm = np.cross(vs[1] - vs[0], vs[2] - vs[0])
        cen = vs.mean(0)
        if np.dot(nrm, cen - center) <= 0:
            continue  # back-face cull
        nlen = np.linalg.norm(nrm)
        nudge = 0.5 * vox / nlen if nlen > 1e-12 else 0.0
        maxedge = max(np.linalg.norm(vs[1] - vs[0]), np.linalg.norm(vs[2] - vs[1]), np.linalg.norm(vs[0] - vs[2]))
        n = int(np.clip(math.ceil(maxedge / (0.5 * vox)), 1, 64))
        samples = [(1 / 3, 1 / 3, 1 / 3)]
        if n >= 2:
            for i in range(n):
                for j in range(n):
                    a, b = (i + 0.5) / n, (j + 0.5) / n
                    if a + b < 1:
                        samples.append((a, b, 1 - a - b))
        for (a, b, c) in samples:
            w = a * vs[0] + b * vs[1] + c * vs[2] - nrm * nudge
            uv = a * uvs[0] + b * uvs[1] + c * uvs[2]
            col = texel(uv[0], uv[1])
            gi = tuple(np.clip(((w - mn) / vox).astype(int), 0, maxdim))
            key = (int(col[0]) >> 3, int(col[1]) >> 3, int(col[2]) >> 3)
            bins = acc[gi]
            slot = bins[key]
            slot[0] += col[0]; slot[1] += col[1]; slot[2] += col[2]; slot[3] += 1

    voxels = {}
    for gi, bins in acc.items():
        bestk = max(bins, key=lambda k: bins[k][3])
        s = bins[bestk]
        voxels[gi] = (int(s[0] / s[3]), int(s[1] / s[3]), int(s[2] / s[3]))
    return voxels


def neutralise(voxels, thresh):
    out = {}
    for gi, (r, g, b) in voxels.items():
        if max(r, g, b) - min(r, g, b) < thresh:
            y = int(round(0.299 * r + 0.587 * g + 0.114 * b))
            out[gi] = (y, y, y)
        else:
            out[gi] = (r, g, b)
    return out


def quant_p332(voxels):
    def f(c):
        r, g, b = c
        code = (r & 0xE0) | ((g & 0xE0) >> 3) | ((b & 0xC0) >> 6)
        code = max(1, min(255, 1 if code == 0 else code))
        return (((code >> 5) & 7) * 255 // 7, ((code >> 2) & 7) * 255 // 7, (code & 3) * 255 // 3)
    return {gi: f(c) for gi, c in voxels.items()}


def quant_median(voxels, maxc=255):
    counts = collections.Counter(voxels.values())
    if len(counts) <= maxc:
        return dict(voxels)
    boxes = [[(r, g, b, n) for (r, g, b), n in counts.items()]]
    while len(boxes) < maxc:
        ti, trange, tch = -1, 0, 0
        for i, box in enumerate(boxes):
            if len(box) < 2:
                continue
            rs = [c[0] for c in box]; gs = [c[1] for c in box]; bs = [c[2] for c in box]
            rr, gr, br = max(rs) - min(rs), max(gs) - min(gs), max(bs) - min(bs)
            rng, ch = rr, 0
            if gr > rng: rng, ch = gr, 1
            if br > rng: rng, ch = br, 2
            if rng > trange: trange, ti, tch = rng, i, ch
        if ti < 0:
            break
        box = boxes[ti]; box.sort(key=lambda c: c[tch])
        total = sum(c[3] for c in box); acc = 0; at = 1
        for k, c in enumerate(box):
            acc += c[3]
            if acc * 2 >= total:
                at = max(1, min(len(box) - 1, k + 1)); break
        boxes[ti] = box[:at]; boxes.append(box[at:])
    rep = {}
    for box in boxes:
        ns = sum(c[3] for c in box)
        avg = (sum(c[0] * c[3] for c in box) // ns, sum(c[1] * c[3] for c in box) // ns, sum(c[2] * c[3] for c in box) // ns)
        for c in box:
            rep[(c[0], c[1], c[2])] = avg
    return {gi: rep[c] for gi, c in voxels.items()}


# ---- isometric render (same as vox_preview) ----
_A, _B = math.radians(45), math.radians(35.264)
def project(x, y, z):
    x1 = x * math.cos(_A) - y * math.sin(_A); y1 = x * math.sin(_A) + y * math.cos(_A)
    y2 = y1 * math.cos(_B) - z * math.sin(_B); z2 = y1 * math.sin(_B) + z * math.cos(_B)
    return x1, -z2, y2
_FACES = [((1,0,0),[(1,0,0),(1,1,0),(1,1,1),(1,0,1)]),((-1,0,0),[(0,0,0),(0,0,1),(0,1,1),(0,1,0)]),
          ((0,1,0),[(0,1,0),(0,1,1),(1,1,1),(1,1,0)]),((0,-1,0),[(0,0,0),(1,0,0),(1,0,1),(0,0,1)]),
          ((0,0,1),[(0,0,1),(1,0,1),(1,1,1),(0,1,1)]),((0,0,-1),[(0,0,0),(0,1,0),(1,1,0),(1,0,0)])]
_L = (0.35,-0.45,0.82); _Ll = math.sqrt(sum(c*c for c in _L)); _L = tuple(c/_Ll for c in _L)
def render(voxels, out):
    occ = set(voxels)
    faces = []
    for (x,y,z),(r,g,b) in voxels.items():
        for normal,corners in _FACES:
            if (x+normal[0],y+normal[1],z+normal[2]) in occ: continue
            proj=[project(x+cx,y+cy,z+cz) for cx,cy,cz in corners]
            d=sum(p[2] for p in proj)/4
            s=0.45+0.55*max(0,sum(n*l for n,l in zip(normal,_L)))
            faces.append((d,[(p[0],p[1]) for p in proj],(int(r*s),int(g*s),int(b*s))))
    xs=[p[0] for _,poly,_ in faces for p in poly]; ys=[p[1] for _,poly,_ in faces for p in poly]
    W=H=900; pad=40
    sc=min((W-2*pad)/(max(xs)-min(xs)),(H-2*pad)/(max(ys)-min(ys)))
    ox=pad-min(xs)*sc+(W-2*pad-(max(xs)-min(xs))*sc)/2; oy=pad-min(ys)*sc+(H-2*pad-(max(ys)-min(ys))*sc)/2
    img=Image.new("RGB",(W,H),(90,90,95)); dr=ImageDraw.Draw(img)
    faces.sort(key=lambda f:f[0],reverse=True)
    for _,poly,fill in faces:
        sp=[(p[0]*sc+ox,p[1]*sc+oy) for p in poly]
        dr.polygon(sp,fill=fill,outline=tuple(max(0,c-18) for c in fill))
    img.save(out); print("wrote",out,len(faces),"faces")


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("obj"); ap.add_argument("--maxdim",type=int,default=48)
    ap.add_argument("--quant",default="median",choices=["median","p332","none"])
    ap.add_argument("--neutralise",type=int,default=0)
    ap.add_argument("--out",default="/tmp/vox_sim.png")
    a=ap.parse_args()
    vox=sample_voxels(a.obj,a.maxdim)
    print("surface voxels:",len(vox),"distinct colours:",len(set(vox.values())))
    if a.neutralise>0: vox=neutralise(vox,a.neutralise)
    vox={"median":quant_median,"p332":quant_p332,"none":lambda v:v}[a.quant](vox)
    cl=collections.Counter()
    for r,g,b in vox.values():
        cl["blue/lavender" if b>r and b>g else "grey" if abs(r-g)<18 and abs(g-b)<18 else "green" if g-r>20 and g-b>20 else "other"]+=1
    t=len(vox); print("after quant=%s:"%a.quant,{k:"%.1f%%"%(100*c/t) for k,c in cl.most_common()})
    render(vox,a.out)


if __name__=="__main__":
    main()
