"""Builds a human character to the HumanCharacters.md spec and renders preview views.

blender --background --factory-startup --python build_human.py -- [key=value ...]

Every knob in HumanCharacters.md is a key (see DEFAULTS), plus `look=<name>` for a preset from LOOKS and
`out=<path prefix>` for the renders. The script follows the bake_shade.py contract: parts are authored in
voxels under an empty called `root`, the root part is `Body`, there is a shadow-catching `ground`, and it
ends in the standard render loop, so

blender --background --factory-startup --python bake_shade.py -- build_human.py <fbx_out> Body [key=value ...]

bakes and exports the same character. Voxel coordinates are Unity's (x right, y up, z forward); `B` maps
them into Blender (front faces -Y). Colours are VoxelStyle.md palette indices.
"""
import bpy, bmesh, math, sys
from mathutils import Vector

# ---- knobs -------------------------------------------------------------------------------------------
DEFAULTS = dict(                                         # every dimension is in voxels (R1)
    leg_length=5, leg_width=3, leg_depth=3, leg_gap=-1,  # visible leg height; leg_gap -1 = derive from the hips (H6)
    shoe_length=1, shoe_height=1,                        # toe beyond the leg's front; sole rows
    belly_length=2, belly_width=8, belly_depth=4,        # lower torso band: hips, legs hang from it
    chest_length=4, chest_width=8, chest_depth=4,        # upper torso band: shoulders, arms hang from it
    shoulder_bevel=1,
    neck_length=1, neck_width=3, neck_depth=3,
    head_width=7, head_height=6, head_depth=6, head_bevel=1,
    eye_inset=1, eye_row=-1,                             # eye_row -1 = a third of the way up the head (H10)
    arm_length=7, arm_width=2, arm_depth=2, hand_length=2,
    hair="short",
    skin="P61", hair_colour="P15", top="P57", bottom="P56", shoes="P06",
    tie="", collar="",                                   # optional palette indices; a tie blade needs a leg gap of 2+
)
LOOKS = {   # the worked examples in HumanCharacters.md
    "standard": dict(),
    "long":     dict(hair="long",   skin="P60", hair_colour="P41", top="P45", bottom="P39", shoes="P00"),
    "afro":     dict(hair="afro",   skin="P62", hair_colour="P16", top="P42", bottom="P05", shoes="P45"),
    "mohawk":   dict(hair="mohawk", skin="P60", hair_colour="P46", top="P06", bottom="P04", shoes="P05"),
    "bun":      dict(hair="bun",    skin="P61", hair_colour="P06", top="P55", bottom="P40", shoes="P00"),
    "buzz":     dict(hair="buzz",   skin="P62", hair_colour="P06", top="P44", bottom="P20", shoes="P14"),
    "fringe":   dict(hair="fringe", skin="P60", hair_colour="P51", top="P19", bottom="P14", shoes="P15"),
    "bald":     dict(hair="bald",   skin="P61", hair_colour="P01", top="P48", bottom="P10", shoes="P26"),
    "suit":     dict(chest_width=10, belly_width=10, belly_length=4, chest_length=2, top="P56", bottom="P56",
                     shoes="P08", collar="P00", tie="P45", hair_colour="P13"),
}
PALETTE = {
    "P00": "f6f6f2", "P01": "dcdcd8", "P04": "66676c", "P05": "4a4a50", "P06": "2b2b30", "P08": "141414",
    "P10": "dcc08e", "P13": "8e5634", "P14": "6e3d22", "P15": "4a2818", "P16": "2e1810", "P19": "8fc23a",
    "P20": "9ea26a", "P26": "6b4528", "P39": "4a66b4", "P40": "3a4d8c", "P41": "ffd070", "P42": "f6d021",
    "P44": "f08236", "P45": "c83a2a", "P46": "e8899a", "P48": "7a3e86", "P51": "b4521e", "P55": "d4b0e0",
    "P56": "23305a", "P57": "2a8a8a", "P60": "f2d0b0", "P61": "c88a60", "P62": "6e4428",
}
INK = "P08"

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
kv = dict(a.split("=", 1) for a in argv if "=" in a)
OUT = kv.pop("out", "/tmp/human")
K = dict(DEFAULTS, **LOOKS[kv.pop("look", "standard")])
for k, v in kv.items():
    K[k] = type(DEFAULTS[k])(v)

# ---- coupling rules (HumanCharacters.md H5-H11): fail loudly rather than build an illegal body -------
lw, ld, aw, ad, nw, nd = K["leg_width"], K["leg_depth"], K["arm_width"], K["arm_depth"], K["neck_width"], K["neck_depth"]
leg_gap = K["leg_gap"] if K["leg_gap"] >= 0 else max(1, K["belly_width"] - 2 * lw - 2)   # H6
leg_out = leg_gap / 2 + lw
torso_bottom = K["leg_length"]
belly_top = torso_bottom + K["belly_length"]
torso_top = belly_top + K["chest_length"]
widest = max(K["chest_width"], K["belly_width"])
arm_top = torso_top - K["shoulder_bevel"]               # H8: arm top level with the shoulder bevel's foot
arm_bottom = arm_top - K["arm_length"]
eye_gap = K["head_width"] - 2 * K["eye_inset"] - 2
eye_h = 2 if K["head_height"] >= 8 else 1
eye_row = K["eye_row"] if K["eye_row"] >= 0 else round(K["head_height"] / 3)
problems = [msg for ok, msg in (
    (min(v for k, v in K.items() if isinstance(v, int) and k not in ("leg_gap", "eye_row", "shoe_length", "shoulder_bevel", "head_bevel")) >= 1,
     "every length, width and depth must be 1+"),
    (leg_gap >= 1, "leg_gap must be 1+: legs that touch share a plane through the whole stride (H6, R8)"),
    (leg_out <= K["belly_width"] / 2 - 0.5, "legs must sit inside the hips: leg_gap/2 + leg_width <= belly_width/2 - 0.5 (H6, R8)"),
    (ld < K["belly_depth"], "leg_depth must be less than belly_depth so the buried leg top stays inside the hips (H6, R15)"),
    (K["chest_width"] - 2 * K["shoulder_bevel"] >= nw, "the neck must stand on the flat between the shoulder bevels (H7)"),
    (nd < K["chest_depth"] and nw != K["head_width"] and nd != K["head_depth"], "neck must be shallower than the chest and differ from the head in width and depth (H9, R8)"),
    (ad < min(K["chest_depth"], K["belly_depth"]) and ad >= 1 and aw >= 1, "arm_depth must be less than both torso depths to hold its tenon (H8)"),
    (arm_bottom <= torso_bottom, "arm too short: hands must end at or below the hip line (H8)"),
    (arm_bottom >= 1, "arm too long: hands must clear the ground (H8)"),
    (1 <= K["hand_length"] < K["arm_length"], "hand_length must be 1+ and shorter than the arm (H8)"),
    (1 <= K["shoe_height"] < K["leg_length"], "shoe_height must be 1+ and below the hips (H6)"),
    (eye_gap >= 2, "head_width - 2*eye_inset - 2 must be 2+: two columns between the eyes (H10)"),
    (K["eye_inset"] >= 1, "eye_inset must be 1+ so an eye never wraps a corner (H10)"),
    (eye_row + eye_h <= K["head_height"] - 2, "eyes must sit below the fringe rows (H10)"),
    (K["shoulder_bevel"] >= 0 and K["head_bevel"] >= 0, "bevels are 0+ voxels"),
) if not ok]
assert not problems, "\n".join(problems)

# ---- scene and materials -----------------------------------------------------------------------------
for o in list(bpy.data.objects):
    bpy.data.objects.remove(o)
sc = bpy.context.scene
world = bpy.data.worlds.new("world")
sc.world = world

def lin(h):
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1]

MATS = {}
def mat(key):
    if key not in MATS:
        m = bpy.data.materials.new("Palette_" + PALETTE[key])
        m.use_nodes = True
        b = m.node_tree.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = lin(PALETTE[key])
        b.inputs["Roughness"].default_value = 0.9
        MATS[key] = m
    return list(MATS).index(key)

def B(p):
    return (p[0], -p[2], p[1])

# ---- parts: voxels on the half-voxel lattice, cut by 45 deg bevel planes ------------------------------
EPS = 1e-6
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]
def _area(poly):
    n = Vector((0, 0, 0))
    for i in range(len(poly)):
        n += Vector(poly[i]).cross(Vector(poly[(i + 1) % len(poly)]))
    return n.length / 2

def clip_voxel(k, planes):
    """Clip the unit voxel at min corner k by half-spaces n.p <= d (R7b bevels). None if no plane cuts
    it, [] if nothing is left, else [(polygon, tag)]: tag (axis, sign) for what remains of a cube face."""
    lo, hi = k, [c + 1 for c in k]
    corners = [(a, b, c) for a in (lo[0], hi[0]) for b in (lo[1], hi[1]) for c in (lo[2], hi[2])]
    act = [(n, d) for n, d in planes if any(_dot(n, p) > d + EPS for p in corners)]
    if not act:
        return None
    faces = []
    for ax in range(3):
        u, v = [a for a in range(3) if a != ax]
        for sg in (1, -1):
            pts = []
            for a, b in ((0, 0), (1, 0), (1, 1), (0, 1)):
                p = [0, 0, 0]
                p[ax] = hi[ax] if sg > 0 else lo[ax]; p[u] = (lo[u], hi[u])[a]; p[v] = (lo[v], hi[v])[b]
                pts.append(tuple(p))
            faces.append((pts if sg > 0 else pts[::-1], (ax, sg)))
    for n, d in act:
        out, cap = [], []
        for poly, tag in faces:
            res = []
            for i in range(len(poly)):
                p, q = poly[i], poly[(i + 1) % len(poly)]
                dp, dq = _dot(n, p) - d, _dot(n, q) - d
                if dp <= EPS:
                    res.append(p)
                if (dp < -EPS and dq > EPS) or (dp > EPS and dq < -EPS):
                    t = dp / (dp - dq)
                    res.append(tuple(p[j] + t * (q[j] - p[j]) for j in range(3)))
            cap += [p for p in res if abs(_dot(n, p) - d) <= 1e-5]
            if len(res) >= 3 and _area(res) > EPS:
                out.append((res, tag))
        uniq = []
        for p in cap:
            if all(sum((p[j] - q[j]) ** 2 for j in range(3)) > 1e-10 for q in uniq):
                uniq.append(p)
        if len(uniq) >= 3:
            c = sum((Vector(p) for p in uniq), Vector()) / len(uniq)
            e1 = (Vector(uniq[0]) - c).normalized(); e2 = Vector(n).normalized().cross(e1)
            uniq.sort(key=lambda p: math.atan2((Vector(p) - c).dot(e2), (Vector(p) - c).dot(e1)))
            if _area(uniq) > EPS:
                out.append((uniq, None))
        faces = out
    return faces

class Part:
    """A rigid block of voxels on one grid (R4), keyed by min corner on the half-voxel lattice (R5)."""
    def __init__(s, name):
        s.name, s.vox, s.planes = name, {}, []
    def box(s, x0, x1, y0, y1, z0, z1, col):
        for i in range(round(x1 - x0)):
            for j in range(round(y1 - y0)):
                for k in range(round(z1 - z0)):
                    s.vox[(x0 + i, y0 + j, z0 + k)] = col
    def paint(s, pred, col):
        for k in s.vox:
            if pred(*k):
                s.vox[k] = col
    def bevel(s, n, d, where=None):
        """A 45 deg bevel as the half-space n.p <= d, applied to voxels whose min corner passes `where`."""
        s.planes.append((n, d, where))
    def build(s, pivot=(0, 0, 0)):
        bm = bmesh.new()
        px, py, pz = pivot
        def face(pts, col):
            vs = [bm.verts.new(B((p[0] - px, p[1] - py, p[2] - pz))) for p in pts]
            for i in range(1, len(vs) - 1) if len(vs) > 4 else [None]:   # quads and triangles only
                f = bm.faces.new(vs if i is None else [vs[0], vs[i], vs[i + 1]])
                f.material_index = mat(col)
        clipped = {}
        for k in list(s.vox):
            pl = [(n, d) for n, d, w in s.planes if w is None or w(*k)]
            faces = clip_voxel(k, pl) if pl else None
            if faces == []:
                del s.vox[k]
            elif faces:
                clipped[k] = faces
        dirs = [((1, 0, 0), 0), ((-1, 0, 0), 0), ((0, 1, 0), 1), ((0, -1, 0), 1), ((0, 0, 1), 2), ((0, 0, -1), 2)]
        for k, col in s.vox.items():
            if k in clipped:
                for poly, tag in clipped[k]:
                    if tag is not None:
                        nb = list(k); nb[tag[0]] += tag[1]
                        if tuple(nb) in s.vox:
                            continue
                    face(poly, col)
                continue
            for d, ax in dirs:
                if (k[0] + d[0], k[1] + d[1], k[2] + d[2]) in s.vox:
                    continue
                lo, hi = list(k), [c + 1 for c in k]
                u, v = [a for a in range(3) if a != ax]
                pts = []
                for a, b in ((0, 0), (1, 0), (1, 1), (0, 1)):
                    p = [0, 0, 0]
                    p[ax] = hi[ax] if sum(d) > 0 else lo[ax]; p[u] = (lo[u], hi[u])[a]; p[v] = (lo[v], hi[v])[b]
                    pts.append(p)
                face(pts if sum(d) > 0 else pts[::-1], col)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        me = bpy.data.meshes.new(s.name)
        bm.to_mesh(me)
        for m in MATS.values():
            me.materials.append(m)
        ob = bpy.data.objects.new(s.name, me)
        sc.collection.objects.link(ob)
        ob.location = B(pivot)
        ob.parent = root
        return ob

root = bpy.data.objects.new("root", None)
sc.collection.objects.link(root)

# ---- Body: chest band over belly band (H5) -----------------------------------------------------------
cw, bw, cd, bd = K["chest_width"], K["belly_width"], K["chest_depth"], K["belly_depth"]
body = Part("Body")
body.box(-bw / 2, bw / 2, torso_bottom, belly_top, -bd / 2, bd / 2, K["bottom"])
body.box(-cw / 2, cw / 2, belly_top, torso_top, -cd / 2, cd / 2, K["top"])
chest = lambda x, y, z: y >= belly_top
sb = K["shoulder_bevel"]
for sx in (1, -1):
    if sb:
        body.bevel((sx, 1, 0), cw / 2 + torso_top - sb, chest)                    # shoulders
    if bw > cw:                                                                   # belly bulge, top corner
        body.bevel((sx, 1, 0), bw / 2 + belly_top - 1, lambda x, y, z: y < belly_top and (x >= cw / 2 or x < -cw / 2))
    if cw > bw:                                                                   # chest overhang, bottom corner
        body.bevel((sx, -1, 0), cw / 2 - belly_top - 1, lambda x, y, z: chest(x, y, z) and (x >= bw / 2 or x < -bw / 2))
    if leg_out <= bw / 2 - 1:                                                     # hips, bottom corner (H6)
        body.bevel((sx, -1, 0), bw / 2 - torso_bottom - 1, lambda x, y, z: y < belly_top)
front = cd / 2 - 1                                                                # min-corner z of the chest's front layer
if K["collar"]:
    body.paint(lambda x, y, z: z == front and y == torso_top - 1 and -2 <= x < 2, K["collar"])
    body.paint(lambda x, y, z: z == front and y == torso_top - 2 and x in (-2, 1), K["collar"])
if K["tie"]:
    body.paint(lambda x, y, z: z == front and y == torso_top - 2 and -1 <= x < 1, K["tie"])
body.build()

if K["tie"]:                                                                      # H12: a clothing shape, half proud
    blade = min(2, leg_gap)                                                       # never wider than the gap it hangs in
    tie = Part("Tie")
    tie.box(-blade / 2, blade / 2, max(1, torso_bottom - 2), torso_top - 2, max(cd, bd) / 2 - 0.5, max(cd, bd) / 2 + 0.5, K["tie"])
    tie.build()

# ---- Head: neck, head, eyes and hair, one part pivoting at the neck base (H9-H11) ---------------------
W, H, D = K["head_width"], K["head_height"], K["head_depth"]
hb = torso_top + K["neck_length"]
ht = hb + H
SK, HR = K["skin"], K["hair_colour"]
head = Part("Head")
head.box(-nw / 2, nw / 2, torso_top - 1, hb, -nd / 2, nd / 2, SK)                 # neck, 1 buried
head.box(-W / 2, W / 2, hb, ht, -D / 2, D / 2, SK)
xl, xr, zb, zf = -W / 2, W / 2 - 1, -D / 2, D / 2 - 1                             # min corners of the outer layers
in_head = lambda x, y, z: -W / 2 <= x < W / 2 and hb <= y < ht and -D / 2 <= z < D / 2
side = lambda x: x in (xl, xr)
short = lambda x, y, z: y >= ht - 1 or (z == zb and y >= hb + 2) or (side(x) and y >= hb + math.ceil(H / 2) and z < zf - 2) or (z == zf and y >= ht - 2)
top_bevel = True
style = K["hair"]
if style == "short":
    head.paint(lambda x, y, z: y >= hb and short(x, y, z), HR)
elif style == "buzz":
    head.paint(lambda x, y, z: y >= ht - 1 or (z == zb and y >= ht - 3) or (side(x) and y >= ht - 2 and z < 0), HR)
elif style == "bald":
    head.paint(lambda x, y, z: hb + 2 <= y < ht - 1 and (z == zb or (side(x) and z < 0)) and y < hb + 2 + max(2, H // 3), HR)
elif style == "fringe":
    head.paint(lambda x, y, z: y >= hb and (short(x, y, z) or (z == zf and y == ht - 3 and x < 0)), HR)
elif style == "long":
    head.paint(lambda x, y, z: y >= hb and (short(x, y, z) or (side(x) and y >= hb + 1 and z < zf - 2)), HR)
    head.box(-W / 2, W / 2, torso_top - 1, ht, zb - 1, zb, HR)                    # hangs down the back past the neck
    for sx in (1, -1):
        head.bevel((sx, 1, 0), W / 2 + ht - 1, lambda x, y, z: z < zb)
        head.bevel((sx, -1, 0), W / 2 - (torso_top - 1) - 1, lambda x, y, z: z < zb)
elif style == "mohawk":
    head.paint(lambda x, y, z: (y >= ht - 1 or z == zb) and -1 <= x < 1, HR)
    head.box(-1, 1, ht, ht + 2, zb, zf, HR)                                       # 2-wide crest, bevelled to a ridge
    for sx in (1, -1):
        head.bevel((sx, 1, 0), 1 + ht + 2 - 1, lambda x, y, z: y >= ht)
elif style == "bun":
    head.paint(lambda x, y, z: y >= hb and short(x, y, z), HR)
    head.box(-1.5, 1.5, ht - 2, ht + 1, zb - 2, zb + 1, HR)                       # 3 x 3 x 3 bun at the back of the crown
    out = lambda x, y, z: not in_head(x, y, z)
    for sx in (1, -1):
        head.bevel((sx, 1, 0), 1.5 + ht + 1 - 1, out); head.bevel((sx, -1, 0), 1.5 - (ht - 2) - 1, out)
elif style == "afro":
    head.paint(lambda x, y, z: z == zb and y >= hb + 1, HR)
    lo_y = ht - H // 2
    head.box(-W / 2 - 1, W / 2 + 1, lo_y, ht + 2, -D / 2 - 1, D / 2, HR)          # one voxel proud, 2 x 2 bevels
    for sx in (1, -1):
        head.bevel((sx, 1, 0), W / 2 + 1 + ht + 2 - 2, lambda x, y, z: y >= lo_y)
        head.bevel((sx, -1, 0), W / 2 + 1 - lo_y - 1, lambda x, y, z: y >= lo_y)
    top_bevel = False
ey, ei = hb + eye_row, K["eye_inset"]                                            # H10: 1 x 1, or 1 wide x 2 tall at 8+
head.paint(lambda x, y, z: z == zf and ey <= y < ey + eye_h and x in (xl + ei, xr - ei), INK)
hbv = K["head_bevel"]
for sx in (1, -1):
    if hbv and top_bevel:
        head.bevel((sx, 1, 0), W / 2 + ht - hbv, in_head)
    if hbv:
        head.bevel((sx, -1, 0), W / 2 - hb - hbv, in_head)
head.build(pivot=(0, torso_top, 0))

# ---- Arms: one width throughout, half a voxel clear of the widest band they pass (H8) ----------------
for sx in (1, -1):
    arm = Part("Arm" + ("R" if sx > 0 else "L"))
    inner = widest / 2 + 0.5
    x0 = inner if sx > 0 else -inner - aw
    arm.box(x0, x0 + aw, arm_bottom, arm_top, -ad / 2, ad / 2, K["top"])
    arm.paint(lambda x, y, z: y < arm_bottom + K["hand_length"], SK)
    if cw >= bw:                                                                  # tenon enters the shoulder
        ty = arm_top - 1
    else:                                                                         # tenon enters the belly's widest row
        ty = belly_top - 1 - K["belly_length"] // 2
    t0 = inner - 1 if sx > 0 else -inner                                          # 1 x 1 tenon, centred in depth
    tz = -0.5
    arm.box(t0, t0 + 1, ty, ty + 1, tz, tz + 1, K["top"])
    outer = inner + aw
    arm.bevel((sx, 1, 0), outer + arm_top - 1)
    arm.bevel((sx, -1, 0), outer - arm_bottom - 1)
    arm.build(pivot=(sx * cw / 2, arm_top - 0.5, 0))

# ---- Legs: 1 buried in the hips; shoe rows with a toe (H6) ---------------------------------------------
shh = K["shoe_height"]
for sx in (1, -1):
    cx = sx * (leg_gap / 2 + lw / 2)
    leg = Part("Leg" + ("R" if sx > 0 else "L"))
    leg.box(cx - lw / 2, cx + lw / 2, 0, torso_bottom + 1, -ld / 2, ld / 2, K["bottom"])
    if K["shoe_length"]:
        leg.box(cx - lw / 2, cx + lw / 2, 0, shh, ld / 2, ld / 2 + K["shoe_length"], K["shoes"])
    leg.paint(lambda x, y, z: y < shh, K["shoes"])
    for ex in (1, -1):
        leg.bevel((ex, -1, 0), ex * cx + lw / 2 - 1, lambda x, y, z: y < 1)       # sole, side edges
    leg.build(pivot=(cx, torso_bottom, 0))

# ---- preview: ground, light, orthographic camera ------------------------------------------------------
root.scale = (0.1, 0.1, 0.1)                                                      # R1: authored in voxels
BG = lin("e3e6eb")
sc.render.engine = "CYCLES"
sc.cycles.samples = 96
sc.cycles.use_denoising = True
sc.view_settings.view_transform = "Standard"
sc.render.resolution_x = sc.render.resolution_y = 800
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = BG
world.node_tree.nodes["Background"].inputs[1].default_value = 0.75
gme = bpy.data.meshes.new("ground")
gme.from_pydata([(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)], [], [(0, 1, 2, 3)])
ground = bpy.data.objects.new("ground", gme)
sc.collection.objects.link(ground)
gm = bpy.data.materials.new("ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = BG
gme.materials.append(gm)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 2.6
sun.data.angle = math.radians(12)
sun.rotation_euler = (math.radians(40), 0, math.radians(-35))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
sc.collection.objects.link(cam)
sc.camera = cam
target = Vector((0, 0, (ht + 2) * 0.05))
VIEWS = {"three": (-45, 30, 3.0), "front": (0, 10, 3.0), "side": (-90, 10, 3.0), "back": (135, 20, 3.0)}

for name, (az, el, scale) in VIEWS.items():
    a, e = math.radians(az), math.radians(el)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.location = target + d * 10
    cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
    cam.data.ortho_scale = scale
    sc.render.filepath = f"{OUT}_{name}.png"
    bpy.ops.render.render(write_still=True)
