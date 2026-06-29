using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.VoxelPipeline
{
    /// <summary>A single filled voxel in the mesh's Y-up grid space.</summary>
    public readonly struct VoxCell
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly Color32 Color;

        public VoxCell(int x, int y, int z, Color32 color)
        {
            X = x;
            Y = y;
            Z = z;
            Color = color;
        }
    }

    /// <summary>Output of <see cref="ObjToVoxConverter.Convert"/>: grid dims + filled cells.</summary>
    public sealed class VoxResult
    {
        public int GridX { get; }
        public int GridY { get; }
        public int GridZ { get; }
        public IReadOnlyList<VoxCell> Cells { get; }

        public VoxResult(int gridX, int gridY, int gridZ, List<VoxCell> cells)
        {
            GridX = gridX;
            GridY = gridY;
            GridZ = gridZ;
            Cells = cells;
        }
    }

    /// <summary>Progress sink for the (slow) per-voxel pass. Return false to cancel.</summary>
    public interface IProgressReporter
    {
        bool Report(float fraction, string message);
    }

    /// <summary>
    /// Solid-fills a textured mesh (.obj or .fbx) into a coloured voxel grid.
    ///
    /// Geometry + spatial queries use geometry3Sharp (g3). OBJ is parsed by a small
    /// local reader (rather than g3's <c>StandardMeshReader</c>) so that per-wedge UVs
    /// survive — g3's single-material OBJ path collapses one UV per position and would
    /// drop the UVs typical textured meshes carry. FBX is loaded through Unity's own
    /// importer (there is no practical hand-written FBX parser), then the imported
    /// mesh + material texture are read back out and fed into the same pipeline.
    /// </summary>
    public static class ObjToVoxConverter
    {
        private const int MaxGridDim = 256;
        private const string TempImportFolder = "Assets/__VoxTemp";

        /// <summary>
        /// Imports the mesh and snapshots its textures into Unity-free buffers. Touches the
        /// <c>AssetDatabase</c> / <c>Texture2D</c> APIs, so it <b>must run on the main thread</b>;
        /// the returned <see cref="LoadedModel"/> can then be voxelized off-thread via
        /// <see cref="Convert(LoadedModel, int, IProgressReporter)"/>.
        /// </summary>
        public static LoadedModel LoadScene(string meshPath) => LoadModel(meshPath);

        /// <summary>
        /// Voxelizes a pre-<see cref="LoadScene">loaded</see> model. Pure CPU work (g3 geometry +
        /// managed texture sampling), so it is safe to run on a background thread.
        /// </summary>
        public static VoxResult Convert(LoadedModel model, int maxDimVoxels, IProgressReporter progress)
        {
            maxDimVoxels = Mathf.Clamp(maxDimVoxels, 1, MaxGridDim);

            g3.DMesh3 mesh = model.Mesh;
            bool hasUVs = model.HasUVs;
            ColorSource colors = model.Colors;

            var tree = new g3.DMeshAABBTree3(mesh);
            tree.Build();

            g3.AxisAlignedBox3d b = mesh.GetBounds();
            double maxDim = b.MaxDim;
            if (maxDim <= 0)
            {
                throw new InvalidDataException("Mesh has zero extent; nothing to voxelize.");
            }

            double voxelSize = maxDim / maxDimVoxels;
            int gridX = GridDim(b.Width, voxelSize);
            int gridY = GridDim(b.Height, voxelSize);
            int gridZ = GridDim(b.Depth, voxelSize);

            // --- Pass 1: occupancy. Solid-fill via winding number. ---
            // |winding| so the fill is robust to global orientation: an inverted/flipped
            // mesh (handedness conversion on FBX, or a generative mesh wound inside-out)
            // reads −1 inside, not +1.
            var occupied = new bool[gridX * gridY * gridZ];
            long total = (long)gridX * gridY * gridZ;
            long done = 0;
            int reportEvery = Mathf.Max(1, (int)(total / 100));
            int filled = 0;

            for (int gy = 0; gy < gridY; gy++)
            {
                double py = b.Min.y + (gy + 0.5) * voxelSize;
                for (int gz = 0; gz < gridZ; gz++)
                {
                    double pz = b.Min.z + (gz + 0.5) * voxelSize;
                    for (int gx = 0; gx < gridX; gx++)
                    {
                        double px = b.Min.x + (gx + 0.5) * voxelSize;
                        if (Math.Abs(tree.FastWindingNumber(new g3.Vector3d(px, py, pz))) > 0.5)
                        {
                            occupied[VoxelIndex(gx, gy, gz, gridX, gridZ)] = true;
                            filled++;
                        }

                        if (++done % reportEvery == 0 &&
                            !progress.Report(
                                0.6f * (float)((double)done / total),
                                $"Voxelizing… {done:N0}/{total:N0} cells, {filled:N0} filled"))
                        {
                            throw new OperationCanceledException("Voxelization cancelled.");
                        }
                    }
                }
            }

            // --- Pass 2: surface colour. Supersample every triangle at ~½-voxel spacing
            // and bin each surface sample's texel colour into the voxel it lands in, so a
            // voxel's colour is the dominant colour of the surface it actually covers — not
            // one noisy point sample. Thin baked-AO seams lose to the panel majority. ---
            Dictionary<int, Dictionary<int, (long r, long g, long b, int n)>> accum =
                AccumulateSurfaceColours(mesh, colors, hasUVs, b, voxelSize, gridX, gridY, gridZ, progress);

            // --- Pass 3: assign each occupied voxel its dominant surface colour. ---
            var cells = new List<VoxCell>();
            for (int gy = 0; gy < gridY; gy++)
            {
                for (int gz = 0; gz < gridZ; gz++)
                {
                    for (int gx = 0; gx < gridX; gx++)
                    {
                        if (!occupied[VoxelIndex(gx, gy, gz, gridX, gridZ)])
                        {
                            continue;
                        }
                        Color32 color = ResolveVoxelColour(
                            gx, gy, gz, accum, gridX, gridY, gridZ, mesh, tree, colors, hasUVs, b, voxelSize);
                        cells.Add(new VoxCell(gx, gy, gz, color));
                    }
                }
            }

            return new VoxResult(gridX, gridY, gridZ, cells);
        }

        private static int VoxelIndex(int gx, int gy, int gz, int gridX, int gridZ) =>
            (gy * gridZ + gz) * gridX + gx;

        /// <summary>
        /// Supersamples the mesh surface and bins each sample's texel colour into the voxel it
        /// falls in. Every triangle is sampled on an interior barycentric grid at roughly half-
        /// voxel spacing (centroid always included), so each occupied surface voxel accumulates
        /// many texels — letting a robust per-voxel dominant (<see cref="DominantColour"/>) wash
        /// out thin baked shading and single-sample noise. Sampling triangle <i>interiors</i>
        /// (never the shared edges/vertices) also dodges the seam-AO bias of nearest-point sampling.
        /// </summary>
        private static Dictionary<int, Dictionary<int, (long r, long g, long b, int n)>>
            AccumulateSurfaceColours(
                g3.DMesh3 mesh, ColorSource colors, bool hasUVs,
                g3.AxisAlignedBox3d bounds, double voxelSize,
                int gridX, int gridY, int gridZ, IProgressReporter progress)
        {
            var accum = new Dictionary<int, Dictionary<int, (long r, long g, long b, int n)>>();
            bool textured = colors.HasTexture && hasUVs;
            TextureSnapshot? tex = colors.Texture;

            // Model centre, for the outward-facing test below.
            double cx = bounds.Min.x + bounds.Width * 0.5;
            double cy = bounds.Min.y + bounds.Height * 0.5;
            double cz = bounds.Min.z + bounds.Depth * 0.5;

            foreach (int tid in mesh.TriangleIndices())
            {
                g3.Vector3d v0 = g3.Vector3d.Zero, v1 = g3.Vector3d.Zero, v2 = g3.Vector3d.Zero;
                mesh.GetTriVertices(tid, ref v0, ref v1, ref v2);

                // Back-face cull: skip inward/interior faces. The mesh's hidden inner surfaces
                // carry baked occlusion (a dark dusty purple) the visible model never shows;
                // accumulating them pollutes exterior voxels. Keep only faces whose normal
                // points outward — radially away from the model centre. (Measured: inner faces
                // are ~6× more purple than outer; the OBJ's winding is outward-consistent.)
                double e1x = v1.x - v0.x, e1y = v1.y - v0.y, e1z = v1.z - v0.z;
                double e2x = v2.x - v0.x, e2y = v2.y - v0.y, e2z = v2.z - v0.z;
                double nx = e1y * e2z - e1z * e2y;
                double ny = e1z * e2x - e1x * e2z;
                double nz = e1x * e2y - e1y * e2x;
                double rx = (v0.x + v1.x + v2.x) / 3.0 - cx;
                double ry = (v0.y + v1.y + v2.y) / 3.0 - cy;
                double rz = (v0.z + v1.z + v2.z) / 3.0 - cz;
                if (nx * rx + ny * ry + nz * rz <= 0)
                {
                    continue;
                }

                g3.Index3i tri = mesh.GetTriangle(tid);
                g3.Vector2f uv0 = mesh.GetVertexUV(tri.a);
                g3.Vector2f uv1 = mesh.GetVertexUV(tri.b);
                g3.Vector2f uv2 = mesh.GetVertexUV(tri.c);

                double maxEdge = Math.Max((v0 - v1).Length, Math.Max((v1 - v2).Length, (v2 - v0).Length));
                int n = Mathf.Clamp(Mathf.CeilToInt((float)(maxEdge / (0.5 * voxelSize))), 1, 64);

                // Bin each sample into the voxel half a cell INWARD of the surface (along −normal),
                // i.e. the occupied shell voxel whose centre sits just inside the hull — instead of
                // the empty voxel the surface point itself may fall in. This lands colour directly on
                // the shell voxel, so it rarely needs the 27-neighbour merge (which would otherwise
                // smear a recess colour across its neighbours).
                double nlen = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                double nudge = nlen > 1e-12 ? 0.5 * voxelSize / nlen : 0.0;

                foreach ((double ba, double bb, double bc) in BarycentricSamples(n))
                {
                    double wx = ba * v0.x + bb * v1.x + bc * v2.x - nx * nudge;
                    double wy = ba * v0.y + bb * v1.y + bc * v2.y - ny * nudge;
                    double wz = ba * v0.z + bb * v1.z + bc * v2.z - nz * nudge;

                    int gx = Mathf.Clamp((int)((wx - bounds.Min.x) / voxelSize), 0, gridX - 1);
                    int gy = Mathf.Clamp((int)((wy - bounds.Min.y) / voxelSize), 0, gridY - 1);
                    int gz = Mathf.Clamp((int)((wz - bounds.Min.z) / voxelSize), 0, gridZ - 1);

                    Color32 c;
                    if (textured)
                    {
                        float u = (float)(ba * uv0.x + bb * uv1.x + bc * uv2.x);
                        float v = (float)(ba * uv0.y + bb * uv1.y + bc * uv2.y);
                        c = SampleTexel(tex!, u, v);
                    }
                    else
                    {
                        c = colors.FlatColor;
                    }

                    AccumulateColour(accum, VoxelIndex(gx, gy, gz, gridX, gridZ), c);
                }
            }

            progress.Report(0.9f, "Resolving voxel colours…");
            return accum;
        }

        /// <summary>
        /// Interior barycentric sample points for a triangle subdivided <paramref name="n"/> ways:
        /// the centroid plus an (n×n) half-offset grid clipped to the triangle. All strictly inside,
        /// so no sample lands on a shared edge/vertex (where UV islands — and baked AO — meet).
        /// </summary>
        private static IEnumerable<(double a, double b, double c)> BarycentricSamples(int n)
        {
            yield return (1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0);
            if (n < 2)
            {
                yield break;
            }
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    double a = (i + 0.5) / n;
                    double b = (j + 0.5) / n;
                    if (a + b >= 1.0)
                    {
                        continue;
                    }
                    yield return (a, b, 1.0 - a - b);
                }
            }
        }

        // sRGB texel fetch: the snapshot samples in linear space (mirroring GetPixelBilinear in a
        // Linear-space project), so re-encode to gamma for the (sRGB) .vox palette. Bilinear is fine
        // here — the gutter is grey, and these samples are averaged per voxel anyway.
        private static Color32 SampleTexel(TextureSnapshot tex, float u, float v) =>
            tex.SampleBilinear(u, v).gamma;

        private static void AccumulateColour(
            Dictionary<int, Dictionary<int, (long r, long g, long b, int n)>> accum, int voxel, Color32 c)
        {
            if (!accum.TryGetValue(voxel, out Dictionary<int, (long r, long g, long b, int n)>? bins))
            {
                bins = new Dictionary<int, (long r, long g, long b, int n)>();
                accum[voxel] = bins;
            }
            // Coarse 5-bit/channel bin so near-identical samples pool (matches DeLight).
            int key = ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);
            bins.TryGetValue(key, out (long r, long g, long b, int n) acc);
            bins[key] = (acc.r + c.r, acc.g + c.g, acc.b + c.b, acc.n + 1);
        }

        /// <summary>
        /// The colour for one occupied voxel: the dominant of its own surface samples; failing
        /// that (the surface rounded into a neighbouring cell), the dominant of its 26-neighbour
        /// samples; failing that (deep interior / sparse surface), a single nearest-surface sample.
        /// </summary>
        private static Color32 ResolveVoxelColour(
            int gx, int gy, int gz,
            Dictionary<int, Dictionary<int, (long r, long g, long b, int n)>> accum,
            int gridX, int gridY, int gridZ,
            g3.DMesh3 mesh, g3.DMeshAABBTree3 tree, ColorSource colors, bool hasUVs,
            g3.AxisAlignedBox3d bounds, double voxelSize)
        {
            if (accum.TryGetValue(VoxelIndex(gx, gy, gz, gridX, gridZ), out var bins) && bins.Count > 0)
            {
                return DominantColour(bins);
            }

            var merged = new Dictionary<int, (long r, long g, long b, int n)>();
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = gx + dx, ny = gy + dy, nz = gz + dz;
                        if (nx < 0 || ny < 0 || nz < 0 || nx >= gridX || ny >= gridY || nz >= gridZ)
                        {
                            continue;
                        }
                        if (!accum.TryGetValue(VoxelIndex(nx, ny, nz, gridX, gridZ), out var nb))
                        {
                            continue;
                        }
                        foreach (KeyValuePair<int, (long r, long g, long b, int n)> kv in nb)
                        {
                            merged.TryGetValue(kv.Key, out (long r, long g, long b, int n) acc);
                            merged[kv.Key] = (acc.r + kv.Value.r, acc.g + kv.Value.g, acc.b + kv.Value.b, acc.n + kv.Value.n);
                        }
                    }
                }
            }
            if (merged.Count > 0)
            {
                return DominantColour(merged);
            }

            double px = bounds.Min.x + (gx + 0.5) * voxelSize;
            double py = bounds.Min.y + (gy + 0.5) * voxelSize;
            double pz = bounds.Min.z + (gz + 0.5) * voxelSize;
            return SampleColor(mesh, tree, colors, hasUVs, new g3.Vector3d(px, py, pz));
        }

        /// <summary>The most-populous coarse colour bin's average — the voxel's dominant material colour.</summary>
        private static Color32 DominantColour(Dictionary<int, (long r, long g, long b, int n)> bins)
        {
            (long r, long g, long b, int n) best = default;
            int bestCount = -1;
            foreach ((long r, long g, long b, int n) acc in bins.Values)
            {
                if (acc.n > bestCount)
                {
                    bestCount = acc.n;
                    best = acc;
                }
            }
            return new Color32(
                (byte)(best.r / best.n), (byte)(best.g / best.n), (byte)(best.b / best.n), 255);
        }

        private static Color32 SampleColor(
            g3.DMesh3 mesh, g3.DMeshAABBTree3 tree, ColorSource colors, bool hasUVs, g3.Vector3d p)
        {
            if (!colors.HasTexture || !hasUVs)
            {
                return colors.FlatColor;
            }

            int tid = tree.FindNearestTriangle(p);
            if (tid < 0)
            {
                return colors.FlatColor;
            }

            g3.Vector3d v0 = g3.Vector3d.Zero, v1 = g3.Vector3d.Zero, v2 = g3.Vector3d.Zero;
            mesh.GetTriVertices(tid, ref v0, ref v1, ref v2);
            var dist = new g3.DistPoint3Triangle3(p, new g3.Triangle3d(v0, v1, v2));
            dist.GetSquared();
            g3.Vector3d bary = dist.TriangleBaryCoords;

            g3.Index3i tri = mesh.GetTriangle(tid);
            g3.Vector2f uv0 = mesh.GetVertexUV(tri.a);
            g3.Vector2f uv1 = mesh.GetVertexUV(tri.b);
            g3.Vector2f uv2 = mesh.GetVertexUV(tri.c);

            float u = (float)(bary.x * uv0.x + bary.y * uv1.x + bary.z * uv2.x);
            // OBJ and Unity textures both put (0,0) at bottom-left, so V is used as-is.
            // If the imported result looks vertically flipped, sample (u, 1 - v) instead.
            float v = (float)(bary.x * uv0.y + bary.y * uv1.y + bary.z * uv2.y);

            // The snapshot samples in LINEAR space (mirroring the GPU sampler's sRGB→linear
            // decode), whereas the .vox palette is sRGB. Re-encode to gamma so the written
            // colours aren't darkened by the gamma curve. (Color.gamma applies linear→sRGB.)
            return colors.Texture!.SampleBilinear(u, v).gamma;
        }

        private static int GridDim(double extent, double voxelSize) =>
            Mathf.Clamp(Mathf.Max(1, Mathf.CeilToInt((float)(extent / voxelSize))), 1, MaxGridDim);

        // ---- Loader dispatch -------------------------------------------------

        private static LoadedModel LoadModel(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".obj" => LoadObjModel(path),
                ".fbx" => LoadUnityAssetModel(path),
                _ => throw new InvalidDataException(
                    $"Unsupported model format '{ext}'. Supported: .obj, .fbx.")
            };
        }

        /// <summary>
        /// Builds a g3 mesh from a per-vertex soup. Vertices are appended in order
        /// (so array index == g3 vertex id), then triangles; g3 rejects triangles
        /// that would make an edge non-manifold, so those are counted and reported
        /// because the drops degrade the winding field on imperfect meshes.
        /// </summary>
        private static g3.DMesh3 BuildDMesh(
            IReadOnlyList<g3.Vector3d> positions, IReadOnlyList<g3.Vector2f> uvs, IReadOnlyList<int> triangles)
        {
            var mesh = new g3.DMesh3();
            mesh.EnableVertexUVs(g3.Vector2f.Zero);

            for (int i = 0; i < positions.Count; i++)
            {
                var info = new g3.NewVertexInfo(positions[i]) { bHaveUV = true, uv = uvs[i] };
                mesh.AppendVertex(ref info);
            }

            int appended = 0;
            int dropped = 0;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (a == b || b == c || a == c)
                {
                    continue;
                }
                if (mesh.AppendTriangle(a, b, c) >= 0)
                {
                    appended++;
                }
                else
                {
                    dropped++;
                }
            }

            if (dropped > 0)
            {
                Debug.LogWarning(
                    $"[MeshToVoxels] Dropped {dropped:N0} non-manifold triangle(s) " +
                    $"of {appended + dropped:N0}; solid fill may be approximate.");
            }

            return mesh;
        }

        // ---- OBJ geometry ----------------------------------------------------

        private static LoadedModel LoadObjModel(string objPath)
        {
            g3.DMesh3 mesh = LoadObjMesh(objPath, out bool hasUVs);
            ColorSource colors = LoadColorSource(objPath);
            return new LoadedModel(mesh, hasUVs, colors);
        }

        private static g3.DMesh3 LoadObjMesh(string objPath, out bool hasUVs)
        {
            var positions = new List<g3.Vector3d>();
            var uvs = new List<g3.Vector2f>();
            var faces = new List<int[]>(); // each entry: flattened [pos,uv, pos,uv, ...]

            foreach (string raw in File.ReadLines(objPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string[] tok = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                switch (tok[0])
                {
                    case "v" when tok.Length >= 4:
                        positions.Add(new g3.Vector3d(ParseF(tok[1]), ParseF(tok[2]), ParseF(tok[3])));
                        break;
                    case "vt" when tok.Length >= 3:
                        uvs.Add(new g3.Vector2f(ParseF(tok[1]), ParseF(tok[2])));
                        break;
                    case "f" when tok.Length >= 4:
                        faces.Add(ParseFace(tok, positions.Count, uvs.Count));
                        break;
                }
            }

            // Split vertices per unique (position, uv) so wedge UVs are preserved.
            var outPositions = new List<g3.Vector3d>();
            var outUVs = new List<g3.Vector2f>();
            var triangles = new List<int>();
            var vertexMap = new Dictionary<(int pos, int uv), int>();

            int VertexFor(int posIdx, int uvIdx)
            {
                var key = (posIdx, uvIdx);
                if (vertexMap.TryGetValue(key, out int existing))
                {
                    return existing;
                }

                int index = outPositions.Count;
                outPositions.Add(positions[posIdx]);
                outUVs.Add(uvIdx >= 0 && uvIdx < uvs.Count ? uvs[uvIdx] : g3.Vector2f.Zero);
                vertexMap[key] = index;
                return index;
            }

            foreach (int[] face in faces)
            {
                int corners = face.Length / 2;
                // Fan-triangulate the (assumed convex) polygon.
                for (int i = 1; i < corners - 1; i++)
                {
                    triangles.Add(VertexFor(face[0], face[1]));
                    triangles.Add(VertexFor(face[i * 2], face[i * 2 + 1]));
                    triangles.Add(VertexFor(face[(i + 1) * 2], face[(i + 1) * 2 + 1]));
                }
            }

            // UVs are usable only if faces referenced texture coords at all.
            hasUVs = uvs.Count > 0 && faces.Exists(f => f[1] >= 0);
            return BuildDMesh(outPositions, outUVs, triangles);
        }

        // ---- FBX / Unity-imported geometry -----------------------------------

        private static LoadedModel LoadUnityAssetModel(string path)
        {
            string assetPath = ToProjectRelative(path, out bool isTemp);
            try
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go == null)
                {
                    throw new InvalidDataException($"Unity could not import '{path}' as a model.");
                }

                MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>();
                if (filters.Length == 0)
                {
                    throw new InvalidDataException("Imported model contains no meshes.");
                }

                var positions = new List<g3.Vector3d>();
                var uvs = new List<g3.Vector2f>();
                var triangles = new List<int>();
                bool hasUVs = false;

                Matrix4x4 rootInverse = go.transform.worldToLocalMatrix;
                foreach (MeshFilter filter in filters)
                {
                    Mesh? m = filter.sharedMesh;
                    if (m == null)
                    {
                        continue;
                    }

                    Matrix4x4 toRoot = rootInverse * filter.transform.localToWorldMatrix;
                    Vector3[] verts = m.vertices;
                    Vector2[] meshUVs = m.uv;
                    bool meshHasUVs = meshUVs.Length == verts.Length && verts.Length > 0;
                    hasUVs |= meshHasUVs;

                    int baseIndex = positions.Count;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        Vector3 p = toRoot.MultiplyPoint3x4(verts[i]);
                        // Unity is left-handed (+Z forward); convert to the OBJ-style
                        // right-handed convention (+Z toward viewer) by negating Z so
                        // both formats share one orientation path downstream.
                        positions.Add(new g3.Vector3d(p.x, p.y, -p.z));
                        Vector2 uv = meshHasUVs ? meshUVs[i] : Vector2.zero;
                        uvs.Add(new g3.Vector2f(uv.x, uv.y));
                    }

                    int[] tris = m.triangles;
                    foreach (int idx in tris)
                    {
                        triangles.Add(baseIndex + idx);
                    }
                }

                g3.DMesh3 mesh = BuildDMesh(positions, uvs, triangles);
                ColorSource colors = LoadUnityColorSource(go);
                return new LoadedModel(mesh, hasUVs, colors);
            }
            finally
            {
                if (isTemp)
                {
                    AssetDatabase.DeleteAsset(TempImportFolder);
                }
            }
        }

        // Returns a project-relative ("Assets/…") path. Files already inside the
        // project are referenced in place; anything else is copied into a temp folder
        // and imported (isTemp=true), so the caller cleans it up afterwards.
        private static string ToProjectRelative(string absolutePath, out bool isTemp)
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string normalised = absolutePath.Replace('\\', '/');
            if (normalised.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                isTemp = false;
                return "Assets" + normalised.Substring(dataPath.Length);
            }

            if (!AssetDatabase.IsValidFolder(TempImportFolder))
            {
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(TempImportFolder));
            }
            string dest = $"{TempImportFolder}/{Path.GetFileName(absolutePath)}";
            File.Copy(absolutePath, dest, true);
            AssetDatabase.ImportAsset(dest, ImportAssetOptions.ForceSynchronousImport);
            isTemp = true;
            return dest;
        }

        private static ColorSource LoadUnityColorSource(GameObject go)
        {
            var midGrey = new Color32(128, 128, 128, 255);
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

            foreach (Renderer r in renderers)
            {
                Material? mat = r.sharedMaterial;
                if (mat != null && mat.mainTexture is Texture2D tex)
                {
                    return new ColorSource { Texture = SnapshotTexture(tex) };
                }
            }

            foreach (Renderer r in renderers)
            {
                Material? mat = r.sharedMaterial;
                if (mat == null)
                {
                    continue;
                }
                if (mat.HasProperty("_BaseColor"))
                {
                    return new ColorSource { FlatColor = mat.GetColor("_BaseColor") };
                }
                if (mat.HasProperty("_Color"))
                {
                    return new ColorSource { FlatColor = mat.color };
                }
            }

            return new ColorSource { FlatColor = midGrey };
        }

        // Blits to a temporary RenderTexture so we get CPU-readable pixels regardless of the
        // source texture's import settings (non-readable / compressed), then snapshots them into
        // a Unity-free linear buffer the off-thread colour passes can sample. Main thread only.
        private static TextureSnapshot SnapshotTexture(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);

            RenderTexture? previous = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            readable.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            TextureSnapshot snapshot = TextureSnapshot.Capture(readable);
            UnityEngine.Object.DestroyImmediate(readable);
            return snapshot;
        }

        // Parse one face token list into a flattened [pos,uv,...] array (0-based; uv = -1 if none).
        private static int[] ParseFace(string[] tok, int posCount, int uvCount)
        {
            int corners = tok.Length - 1;
            var flat = new int[corners * 2];
            for (int i = 0; i < corners; i++)
            {
                string[] parts = tok[i + 1].Split('/');
                int pos = ResolveIndex(parts[0], posCount);
                int uv = parts.Length >= 2 ? ResolveIndex(parts[1], uvCount) : -1;
                flat[i * 2] = pos;
                flat[i * 2 + 1] = uv;
            }
            return flat;
        }

        // OBJ indices are 1-based; negative values count back from the current end.
        private static int ResolveIndex(string s, int count)
        {
            if (string.IsNullOrEmpty(s) || !int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
            {
                return -1;
            }
            return idx > 0 ? idx - 1 : count + idx;
        }

        private static float ParseF(string s) =>
            float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // ---- Shared types ----------------------------------------------------

        public sealed class LoadedModel
        {
            public g3.DMesh3 Mesh { get; }
            public bool HasUVs { get; }
            public ColorSource Colors { get; }

            public LoadedModel(g3.DMesh3 mesh, bool hasUVs, ColorSource colors)
            {
                Mesh = mesh;
                HasUVs = hasUVs;
                Colors = colors;
            }
        }

        // ---- Colour source (.mtl / map_Kd) -----------------------------------

        public sealed class ColorSource
        {
            public TextureSnapshot? Texture { get; init; }
            public Color32 FlatColor { get; init; }
            public bool HasTexture => Texture != null;
        }

        /// <summary>
        /// A CPU-side, Unity-free copy of a texture's pixels (decoded to linear space) plus a
        /// managed bilinear sampler — the off-thread stand-in for <see cref="Texture2D.GetPixelBilinear"/>,
        /// which is main-thread only. Captured during <see cref="LoadScene"/> so the colour passes
        /// can sample it from a background thread.
        /// </summary>
        public sealed class TextureSnapshot
        {
            private readonly Color[] _linearPixels; // row-major, (0,0) bottom-left
            private readonly int _width;
            private readonly int _height;

            public TextureSnapshot(Color[] linearPixels, int width, int height)
            {
                _linearPixels = linearPixels;
                _width = width;
                _height = height;
            }

            /// <summary>Snapshots <paramref name="tex"/> into a linear-space buffer. Main thread only.</summary>
            public static TextureSnapshot Capture(Texture2D tex)
            {
                Color[] stored = tex.GetPixels(); // stored sRGB values, no colour-space conversion
                var linear = new Color[stored.Length];
                for (int i = 0; i < stored.Length; i++)
                {
                    linear[i] = stored[i].linear; // mirror the GPU sampler's sRGB→linear decode
                }
                return new TextureSnapshot(linear, tex.width, tex.height);
            }

            /// <summary>
            /// Repeat-wrapped bilinear sample in LINEAR space — the off-thread equivalent of
            /// <see cref="Texture2D.GetPixelBilinear"/> in a linear-colour-space project. Callers
            /// re-encode the result to gamma for the (sRGB) .vox palette.
            /// </summary>
            public Color SampleBilinear(float u, float v)
            {
                if (_width <= 0 || _height <= 0)
                {
                    return Color.clear;
                }

                float fx = Mathf.Repeat(u, 1f) * _width - 0.5f;
                float fy = Mathf.Repeat(v, 1f) * _height - 0.5f;
                int x0 = Mathf.FloorToInt(fx);
                int y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0;
                float ty = fy - y0;

                Color bottom = Color.Lerp(Texel(x0, y0), Texel(x0 + 1, y0), tx);
                Color top = Color.Lerp(Texel(x0, y0 + 1), Texel(x0 + 1, y0 + 1), tx);
                return Color.Lerp(bottom, top, ty);
            }

            private Color Texel(int x, int y)
            {
                x = ((x % _width) + _width) % _width;
                y = ((y % _height) + _height) % _height;
                return _linearPixels[y * _width + x];
            }
        }

        private static ColorSource LoadColorSource(string objPath)
        {
            var midGrey = new Color32(128, 128, 128, 255);
            try
            {
                string? mtlPath = FindMtlPath(objPath);
                if (mtlPath == null || !File.Exists(mtlPath))
                {
                    return new ColorSource { FlatColor = midGrey };
                }

                string mtlDir = Path.GetDirectoryName(mtlPath) ?? ".";
                Color32 flat = midGrey;
                string? mapKd = null;

                foreach (string raw in File.ReadLines(mtlPath))
                {
                    string line = raw.Trim();
                    if (line.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                    {
                        mapKd = line.Substring("map_Kd".Length).Trim();
                    }
                    else if (line.StartsWith("Kd", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (t.Length >= 4)
                        {
                            flat = new Color(ParseF(t[1]), ParseF(t[2]), ParseF(t[3]), 1f);
                        }
                    }
                }

                if (mapKd != null)
                {
                    string? texPath = ResolveTexturePath(mapKd, mtlDir);
                    if (texPath != null && File.Exists(texPath))
                    {
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(File.ReadAllBytes(texPath)))
                        {
                            TextureSnapshot snapshot = TextureSnapshot.Capture(tex);
                            UnityEngine.Object.DestroyImmediate(tex);
                            return new ColorSource { Texture = snapshot };
                        }
                    }
                    Debug.LogWarning($"[MeshToVoxels] map_Kd '{mapKd}' not found/loadable; using flat Kd colour.");
                }

                return new ColorSource { FlatColor = flat };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MeshToVoxels] Failed to load colour source: {e.Message}; using mid-grey.");
                return new ColorSource { FlatColor = midGrey };
            }
        }

        private static string? FindMtlPath(string objPath)
        {
            string objDir = Path.GetDirectoryName(objPath) ?? ".";
            foreach (string raw in File.ReadLines(objPath))
            {
                string line = raw.Trim();
                if (line.StartsWith("mtllib", StringComparison.OrdinalIgnoreCase))
                {
                    string name = line.Substring("mtllib".Length).Trim();
                    string referenced = Path.IsPathRooted(name) ? name : Path.Combine(objDir, name);
                    // Use the OBJ's own mtllib only if it actually resolves on disk. Meshy bakes a
                    // generic "mtllib model.mtl" into every OBJ, but the material library is saved
                    // under the asset's base name — so the referenced file is usually absent and we
                    // must fall through to the sibling rather than return a dead path.
                    if (File.Exists(referenced))
                    {
                        return referenced;
                    }
                    break;
                }
            }
            // Fallback: sibling file sharing the OBJ basename.
            string sibling = Path.Combine(objDir, Path.GetFileNameWithoutExtension(objPath) + ".mtl");
            return File.Exists(sibling) ? sibling : null;
        }

        private static string? ResolveTexturePath(string mapKd, string mtlDir)
        {
            // map_Kd may carry options before the filename (e.g. "-bm 0.5 tex.png").
            // Try the whole remainder first, then fall back to the last token.
            string whole = Path.IsPathRooted(mapKd) ? mapKd : Path.Combine(mtlDir, mapKd);
            if (File.Exists(whole))
            {
                return whole;
            }

            string[] parts = mapKd.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }
            string last = parts[parts.Length - 1];
            return Path.IsPathRooted(last) ? last : Path.Combine(mtlDir, last);
        }
    }
}
