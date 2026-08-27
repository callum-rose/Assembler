"""Authors Assembler's nine primitive meshes and exports one FBX each.

Run it inside Blender (via the Blender MCP, or `blender --background --python`). It rebuilds the
`Assembler Primitives` collection from scratch every time, so it is safe to re-run.

Every mesh is authored so that its **native bounding box is its true world size**: the cube, sphere,
cylinder, plane, quad, wedge, cone and hemisphere are all 1 x 1 x 1, which makes `Size` a plain world
measurement in metres with nothing to divide out. The capsule is the one exception and cannot be
otherwise: a capsule of radius 0.5 and total height 1 *is* a sphere, so it is authored 1 x 2 x 1 (a
1-tall cylinder body between two radius-0.5 hemispheres) and `ModelGeometry.Normalise` halves its Y.

Geometry is written in **Unity coordinates** (Y up, +Z forward) and converted on the way into
Blender's (Z up) space by `U`, so the numbers here read the same as the ones documented in
`Assets/docs/Models.md`.

Normals are set as explicit per-loop custom split normals rather than left to smoothing flags: the
round shapes get their exact analytic normal, and the rim where a cap meets a curved side gets a
hard edge without depending on a smoothing-angle setting surviving the FBX round trip.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

COLLECTION = "Assembler Primitives"

# Longitude divisions on every round shape, and latitude divisions on the domed ones. 24 matches the
# silhouette quality of Unity's own built-in sphere and cylinder closely enough to be a drop-in.
SEGMENTS = 24
SPHERE_RINGS = 16
DOME_RINGS = 8


def U(x, y, z):
    """Unity (X right, Y up, Z forward) -> Blender (X right, Y forward, Z up)."""
    return (x, -z, y)


def UV3(v):
    """The same map applied to a direction."""
    return Vector(U(v[0], v[1], v[2])).normalized()


# ---------------------------------------------------------------------------------------- mesh --


def add_mesh(name, verts, faces, normal_of, uv_of):
    """Builds one object from Unity-space vertices, faces and per-corner normal/uv functions.

    `normal_of(face_index, position)` and `uv_of(face_index, position)` both take the *Unity-space*
    vertex position, so every formula below is written in the coordinates the shape is documented in.
    """
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([U(*v) for v in verts], [], faces)
    mesh.validate()

    for poly in mesh.polygons:
        poly.use_smooth = True

    uv_layer = mesh.uv_layers.new(name="UVMap")
    loop_normals = []

    for poly in mesh.polygons:
        for loop_index in poly.loop_indices:
            position = verts[mesh.loops[loop_index].vertex_index]
            loop_normals.append(UV3(normal_of(poly.index, position)))
            uv_layer.data[loop_index].uv = uv_of(poly.index, position)

    mesh.normals_split_custom_set(loop_normals)

    obj = bpy.data.objects.new(name, mesh)
    bpy.data.collections[COLLECTION].objects.link(obj)
    return obj


def ring(count, radius, y):
    """`count` points anticlockwise around +Y, starting at +X."""
    return [(radius * math.cos(2 * math.pi * i / count), y, radius * math.sin(2 * math.pi * i / count))
            for i in range(count)]


def radial_uv(position, y_min, y_span):
    return (math.atan2(position[2], position[0]) / (2 * math.pi) + 0.5,
            (position[1] - y_min) / y_span)


def planar_uv(normal, position):
    """Projects along whichever axis the face normal is most aligned with."""
    ax, ay, az = abs(normal[0]), abs(normal[1]), abs(normal[2])
    if ax >= ay and ax >= az:
        return (position[2] + 0.5, position[1] + 0.5)
    if ay >= az:
        return (position[0] + 0.5, position[2] + 0.5)
    return (position[0] + 0.5, position[1] + 0.5)


def flat_faces(verts, faces):
    """Per-face normals for a flat-shaded mesh, from the first three corners of each face."""
    normals = []
    for face in faces:
        a, b, c = (Vector(verts[i]) for i in face[:3])
        normals.append((b - a).cross(c - a).normalized())
    return normals


# ------------------------------------------------------------------------------------- shapes --


def build_cube():
    v = [(-.5, -.5, -.5), (.5, -.5, -.5), (.5, -.5, .5), (-.5, -.5, .5),
         (-.5, .5, -.5), (.5, .5, -.5), (.5, .5, .5), (-.5, .5, .5)]
    f = [(0, 3, 2, 1),          # -Y
         (4, 5, 6, 7),          # +Y
         (0, 1, 5, 4),          # -Z
         (2, 3, 7, 6),          # +Z
         (0, 4, 7, 3),          # -X
         (1, 2, 6, 5)]          # +X
    n = flat_faces(v, f)
    return add_mesh("Cube", v, f, lambda i, p: n[i], lambda i, p: planar_uv(n[i], p))


def build_wedge():
    """A cube with one edge sliced off: the upright face is at +Z and the slope falls away to -Z,
    so the sloped face is the one a default camera (sitting at -Z, looking toward +Z) sees."""
    v = [(-.5, -.5, -.5), (.5, -.5, -.5), (.5, -.5, .5), (-.5, -.5, .5), (-.5, .5, .5), (.5, .5, .5)]
    f = [(0, 3, 2, 1),          # base    -Y
         (3, 4, 5, 2),          # upright +Z
         (0, 1, 5, 4),          # slope
         (0, 4, 3),             # side    -X
         (1, 2, 5)]             # side    +X
    n = flat_faces(v, f)
    return add_mesh("Wedge", v, f, lambda i, p: n[i], lambda i, p: planar_uv(n[i], p))


def build_plane():
    """1 x 1 in XZ, facing +Y. Single-sided, like Unity's."""
    v = [(-.5, 0, -.5), (-.5, 0, .5), (.5, 0, .5), (.5, 0, -.5)]
    f = [(0, 1, 2, 3)]
    return add_mesh("Plane", v, f,
                    lambda i, p: (0, 1, 0),
                    lambda i, p: (p[0] + 0.5, p[2] + 0.5))


def build_quad():
    """1 x 1 in XY, facing -Z — toward a default camera. Single-sided, like Unity's."""
    v = [(-.5, -.5, 0), (.5, -.5, 0), (.5, .5, 0), (-.5, .5, 0)]
    f = [(0, 3, 2, 1)]
    return add_mesh("Quad", v, f,
                    lambda i, p: (0, 0, -1),
                    lambda i, p: (p[0] + 0.5, p[1] + 0.5))


def build_sphere():
    """Diameter 1. A UV sphere: poles are single vertices, so the seam runs down +X."""
    verts = [(0, .5, 0)]
    for r in range(1, SPHERE_RINGS):
        phi = math.pi * r / SPHERE_RINGS
        verts += ring(SEGMENTS, 0.5 * math.sin(phi), 0.5 * math.cos(phi))
    verts.append((0, -.5, 0))

    south = len(verts) - 1
    faces = []
    for s in range(SEGMENTS):
        t = (s + 1) % SEGMENTS
        faces.append((0, 1 + t, 1 + s))
    for r in range(SPHERE_RINGS - 2):
        base, nxt = 1 + r * SEGMENTS, 1 + (r + 1) * SEGMENTS
        for s in range(SEGMENTS):
            t = (s + 1) % SEGMENTS
            faces.append((base + s, base + t, nxt + t, nxt + s))
    last = 1 + (SPHERE_RINGS - 2) * SEGMENTS
    for s in range(SEGMENTS):
        t = (s + 1) % SEGMENTS
        faces.append((last + s, last + t, south))

    return add_mesh("Sphere", verts, faces,
                    lambda i, p: p,
                    lambda i, p: radial_uv(p, -0.5, 1.0))


def build_cylinder():
    """Diameter 1, 1 tall. Smooth around, hard rims."""
    top, bottom = ring(SEGMENTS, 0.5, 0.5), ring(SEGMENTS, 0.5, -0.5)
    verts = top + bottom + top + bottom + [(0, .5, 0), (0, -.5, 0)]
    n = SEGMENTS
    faces = []
    for s in range(n):                                          # side
        t = (s + 1) % n
        faces.append((s, t, n + t, n + s))
    for s in range(n):                                          # +Y cap
        t = (s + 1) % n
        faces.append((2 * n + t, 2 * n + s, 4 * n))
    for s in range(n):                                          # -Y cap
        t = (s + 1) % n
        faces.append((3 * n + s, 3 * n + t, 4 * n + 1))

    side_count = n

    def normal(i, p):
        if i < side_count:
            return (p[0], 0, p[2])
        return (0, 1, 0) if i < 2 * side_count else (0, -1, 0)

    def uv(i, p):
        return radial_uv(p, -0.5, 1.0) if i < side_count else (p[0] + 0.5, p[2] + 0.5)

    return add_mesh("Cylinder", verts, faces, normal, uv)


def build_capsule():
    """Diameter 1, 2 tall: a 1-tall cylinder body between two radius-0.5 hemispherical caps. The one
    shape whose native size is not its `Size` — at 1 x 1 x 1 a capsule is a sphere. `Normalise`
    halves its Y so `Size 1, 2, 1` is the true capsule and anything shorter squashes the caps."""
    verts = [(0, 1, 0)]
    for r in range(1, DOME_RINGS + 1):                          # north cap, pole down to equator
        phi = 0.5 * math.pi * r / DOME_RINGS
        verts += ring(SEGMENTS, 0.5 * math.sin(phi), 0.5 + 0.5 * math.cos(phi))
    for r in range(DOME_RINGS, 0, -1):                          # south cap, equator down to pole
        phi = 0.5 * math.pi * r / DOME_RINGS
        verts += ring(SEGMENTS, 0.5 * math.sin(phi), -0.5 - 0.5 * math.cos(phi))
    verts.append((0, -1, 0))

    south = len(verts) - 1
    rings = 2 * DOME_RINGS                                      # ring 0 is the top of the body
    faces = []
    for s in range(SEGMENTS):
        t = (s + 1) % SEGMENTS
        faces.append((0, 1 + t, 1 + s))
    for r in range(rings - 1):
        base, nxt = 1 + r * SEGMENTS, 1 + (r + 1) * SEGMENTS
        for s in range(SEGMENTS):
            t = (s + 1) % SEGMENTS
            faces.append((base + s, base + t, nxt + t, nxt + s))
    last = 1 + (rings - 1) * SEGMENTS
    for s in range(SEGMENTS):
        t = (s + 1) % SEGMENTS
        faces.append((last + s, last + t, south))

    def normal(i, p):
        # Above/below the body the surface is a sphere centred on that cap's origin; across the
        # body it is a cylinder. The two agree exactly at the equators, so there is no seam.
        centre = 0.5 if p[1] > 0.5 else (-0.5 if p[1] < -0.5 else p[1])
        return (p[0], p[1] - centre, p[2])

    return add_mesh("Capsule", verts, faces, normal, lambda i, p: radial_uv(p, -1.0, 2.0))


def build_cone():
    """Base diameter 1 at Y -0.5, apex at Y +0.5. The apex ring is `SEGMENTS` coincident vertices so
    each side triangle carries its own tip normal — a single shared apex shades as a dark pinprick."""
    base = ring(SEGMENTS, 0.5, -0.5)
    apex = [(0, .5, 0)] * SEGMENTS
    verts = base + apex + base + [(0, -.5, 0)]
    n = SEGMENTS
    faces = []
    for s in range(n):                                          # side
        t = (s + 1) % n
        faces.append((t, s, n + s))
    for s in range(n):                                          # base cap
        t = (s + 1) % n
        faces.append((2 * n + s, 2 * n + t, 3 * n))

    # The slant: for a cone of radius 0.5 and height 1 the surface normal leans out by the ratio of
    # radius to height, so its Y component is 0.5 and its radial component 1 before normalising.
    slant_y = 0.5

    def normal(i, p):
        if i >= n:
            return (0, -1, 0)
        # At the apex all three components of `p` are zero on X/Z, so take the segment's own angle.
        if p[1] > 0:
            angle = 2 * math.pi * (i + 0.5) / n
            return (math.cos(angle), slant_y, math.sin(angle))
        return (p[0], slant_y * 0.5, p[2])

    def uv(i, p):
        return radial_uv(p, -0.5, 1.0) if i < n else (p[0] + 0.5, p[2] + 0.5)

    return add_mesh("Cone", verts, faces, normal, uv)


def build_hemisphere():
    """1 x 1 x 1: a dome 1 across and 1 tall, capped flat at Y -0.5. It is half an *ellipsoid* rather
    than half a sphere, which is what keeps `Size` a true bounding box — `Size 1, 0.5, 1` is the true
    half-sphere, and `Size 1, 1, 1` a dome twice as tall."""
    verts = [(0, .5, 0)]
    for r in range(1, DOME_RINGS + 1):
        phi = 0.5 * math.pi * r / DOME_RINGS
        verts += ring(SEGMENTS, 0.5 * math.sin(phi), -0.5 + math.cos(phi))
    rim = verts[-SEGMENTS:]
    verts += rim + [(0, -.5, 0)]

    n = SEGMENTS
    faces = []
    for s in range(n):
        t = (s + 1) % n
        faces.append((0, 1 + t, 1 + s))
    for r in range(DOME_RINGS - 1):
        base, nxt = 1 + r * n, 1 + (r + 1) * n
        for s in range(n):
            t = (s + 1) % n
            faces.append((base + s, base + t, nxt + t, nxt + s))

    dome_faces = len(faces)
    cap = 1 + DOME_RINGS * n
    for s in range(n):
        t = (s + 1) % n
        faces.append((cap + s, cap + t, cap + n))

    def normal(i, p):
        if i >= dome_faces:
            return (0, -1, 0)
        # Gradient of (x / 0.5)^2 + ((y + 0.5) / 1)^2 + (z / 0.5)^2 = 1.
        return (4 * p[0], p[1] + 0.5, 4 * p[2])

    def uv(i, p):
        return radial_uv(p, -0.5, 1.0) if i < dome_faces else (p[0] + 0.5, p[2] + 0.5)

    return add_mesh("Hemisphere", verts, faces, normal, uv)


BUILDERS = [build_cube, build_sphere, build_capsule, build_cylinder, build_plane,
            build_quad, build_wedge, build_cone, build_hemisphere]


# -------------------------------------------------------------------------------------- driver --


def rebuild():
    col = bpy.data.collections.get(COLLECTION)
    if col is None:
        col = bpy.data.collections.new(COLLECTION)
        bpy.context.scene.collection.children.link(col)
    for obj in list(col.objects):
        data = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if isinstance(data, bpy.types.Mesh) and data.users == 0:
            bpy.data.meshes.remove(data)

    built = []
    for i, builder in enumerate(BUILDERS):
        obj = builder()
        obj.location = (i * 1.5, 0, 0)
        built.append(obj)
    return built


def export(directory):
    os.makedirs(directory, exist_ok=True)
    written = []
    for obj in bpy.data.collections[COLLECTION].objects:
        bpy.ops.object.select_all(action="DESELECT")
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        # Exported at the origin: the layout offset above is only so they can be seen side by side.
        location = tuple(obj.location)
        obj.location = (0, 0, 0)
        path = os.path.join(directory, f"{obj.name}.fbx")
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=True,
            object_types={"MESH"},
            apply_unit_scale=True,
            global_scale=1.0,
            apply_scale_options="FBX_SCALE_NONE",
            axis_forward="-Z",
            axis_up="Y",
            use_mesh_modifiers=False,
            mesh_smooth_type="OFF",
            use_triangles=True,
            use_custom_props=False,
            bake_anim=False,
            path_mode="STRIP",
            # Without this the Blender-to-Unity axis conversion (and the metre-to-centimetre unit
            # conversion) lands on the FBX *node* transform rather than the mesh data. Assembler
            # loads the Mesh sub-asset directly and never instantiates the imported prefab, so an
            # unbaked transform means vertices arriving Z-up and a hundredth of their true size.
            bake_space_transform=True,
        )
        obj.location = location
        written.append(path)
    return written


if __name__ == "__main__":
    rebuild()
    target = sys.argv[-1] if "--" in sys.argv else None
    if target:
        export(target)
