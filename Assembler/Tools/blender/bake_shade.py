"""Bakes the voxel-style shading channels into a built model and exports it as FBX for Unity.

blender --background --factory-startup --python bake_shade.py -- <build_script> <fbx_out> [root_part] [build args...]

<build_script> is a model build script in the style of the VoxelStyle.md worked examples: it builds the
parts under an empty called `root`, a shadow-catching `ground`, and ends in a render loop beginning with
`for name, (az, el, scale) in VIEWS.items():`. Everything before that loop is run; the loop is not.

Per face corner, the export carries:
- vertex colour R: AO, a Cycles bake at rest with every part present and the ground hidden (R23, R26);
- vertex colour G: Tone, model-space height at rest, 0..1 over the root part's height, clamped beyond it;
- UV0 / UV1: distance in voxels from the corner to each of its face's edges (UV0.xy edges 0-1, UV1.xy
  edges 2-3), or NOT_CONVEX when that edge is not a convex edge of the part. Distance to a line is
  affine across a planar face, so the GPU interpolates it exactly; the shader takes the minimum.
Palette colours stay on the materials, one per colour, named by hex.
"""
import bpy, bmesh, sys, os, math

argv = sys.argv[sys.argv.index("--") + 1:]
BUILD, FBX_OUT = argv[0], argv[1]
ROOT_PART = argv[2] if len(argv) > 2 else "Body"
BUILD_ARGS = argv[3:]
VOXEL = 0.1                      # R1: one voxel is 0.1 m
NOT_CONVEX = 99.0
CONVEX_MIN = math.radians(20)    # dihedral angle above which an edge counts as convex
AO_REACH_VOXELS = 2.0            # R23

sys.argv = ["x", "--"] + BUILD_ARGS
for k in ("ANIM", "BAKE_AO", "EEVEE", "FRAMES"):
    os.environ.pop(k, None)
src = open(BUILD).read()
MARK = "for name, (az, el, scale) in VIEWS.items():"
assert src.count(MARK) == 1, "build script must end in the standard render loop"
exec(compile(src[:src.index(MARK)], BUILD, "exec"))

parts = [o for o in bpy.data.objects if o.type == 'MESH' and o is not ground]
root.scale = (VOXEL, VOXEL, VOXEL)          # parts are authored in voxels; export at real size
bpy.context.view_layer.update()

# ---- edge distances, per face corner, in the part's own (voxel) units ---------------------------------
edge_uvs = {}
for o in parts:
    bm = bmesh.new()
    bm.from_mesh(o.data)
    convex = {e.index for e in bm.edges
              if len(e.link_faces) == 2 and e.calc_face_angle_signed(0.0) > CONVEX_MIN}
    corner = {}
    for f in bm.faces:
        assert len(f.loops) <= 4, (o.name, "faces must be quads or triangles")
        lines = []
        for lp in f.loops:                  # loop i's edge runs from loop i's vertex to loop i+1's
            e = lp.edge
            lines.append((e.verts[0].co.copy(), e.verts[1].co.copy()) if e.index in convex else None)
        for lp in f.loops:
            p = lp.vert.co
            d = []
            for ln in lines + [None] * (4 - len(lines)):
                if ln is None:
                    d.append(NOT_CONVEX)
                    continue
                a, b = ln
                ab = (b - a).normalized()
                d.append((p - a - ab * (p - a).dot(ab)).length)
            corner[lp.index] = d
    bm.free()
    edge_uvs[o.name] = corner

# ---- AO (R23, R26): rest pose, every part present, ground excluded ------------------------------------
sc.render.engine = 'CYCLES'
sc.cycles.samples = int(os.environ.get("AO_SAMPLES", "256"))
sc.cycles.use_denoising = False
world.light_settings.distance = AO_REACH_VOXELS * VOXEL
ground.hide_render = True
bpy.ops.object.select_all(action='DESELECT')
for o in parts:
    a = o.data.color_attributes.new("AO", 'FLOAT_COLOR', 'CORNER')
    o.data.color_attributes.active_color = a
    o.select_set(True)
bpy.context.view_layer.objects.active = parts[0]
bpy.ops.object.bake(type='AO', target='VERTEX_COLORS')

# ---- Tone: height at rest over the root part's height -----------------------------------------------
ref = [o for o in parts if o.name == ROOT_PART]
assert ref, f"no part called {ROOT_PART}"
zs = [(ref[0].matrix_world @ v.co).z for v in ref[0].data.vertices]
zmin, zmax = min(zs), max(zs)

# ---- pack: one colour set (Unity imports one), two UV sets ------------------------------------------
for o in parts:
    me = o.data
    ao = me.color_attributes["AO"]
    shade = me.color_attributes.new("Shade", 'FLOAT_COLOR', 'CORNER')
    uv0 = me.uv_layers.new(name="EdgeA")
    uv1 = me.uv_layers.new(name="EdgeB")
    mw = o.matrix_world
    corner = edge_uvs[o.name]
    for li, loop in enumerate(me.loops):
        z = (mw @ me.vertices[loop.vertex_index].co).z
        tone = min(1.0, max(0.0, (z - zmin) / (zmax - zmin)))
        shade.data[li].color = (ao.data[li].color[0], tone, 0.0, 1.0)
        d = corner[li]
        uv0.data[li].uv = (d[0], d[1])
        uv1.data[li].uv = (d[2], d[3])
    me.color_attributes.remove(ao)
    me.color_attributes.active_color = me.color_attributes["Shade"]
    me.color_attributes.render_color_index = me.color_attributes.active_color_index

bpy.ops.object.select_all(action='DESELECT')
for o in parts + [root]:
    o.select_set(True)
for o in bpy.data.objects:          # build helpers (joints etc.) that are not parts stay out of the export
    if o.type == 'EMPTY' and o is not root:
        o.select_set(True)
os.makedirs(os.path.dirname(os.path.abspath(FBX_OUT)), exist_ok=True)
bpy.ops.export_scene.fbx(filepath=FBX_OUT, use_selection=True, object_types={'EMPTY', 'MESH'},
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', colors_type='LINEAR',
                         prioritize_active_color=True, bake_anim=False, add_leaf_bones=False)
print("SHADE_EXPORTED", FBX_OUT, {o.name: len(o.data.loops) for o in parts})
