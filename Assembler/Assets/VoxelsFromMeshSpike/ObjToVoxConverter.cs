using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace VoxelsFromMeshSpike
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
    /// Solid-fills a textured OBJ into a coloured voxel grid.
    ///
    /// Geometry + spatial queries use geometry3Sharp (g3); the OBJ is parsed by a
    /// small local reader (rather than g3's <c>StandardMeshReader</c>) so that
    /// per-wedge UVs survive — g3's single-material OBJ path collapses one UV per
    /// position and would drop the UVs typical textured meshes carry.
    /// </summary>
    public static class ObjToVoxConverter
    {
        private const int MaxGridDim = 256;

        public static VoxResult Convert(string objPath, int maxDimVoxels, IProgressReporter progress)
        {
            maxDimVoxels = Mathf.Clamp(maxDimVoxels, 1, MaxGridDim);

            g3.DMesh3 mesh = LoadObjMesh(objPath, out bool hasUVs);
            ColorSource colors = LoadColorSource(objPath);

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

            var cells = new List<VoxCell>();
            long total = (long)gridX * gridY * gridZ;
            long done = 0;
            int reportEvery = Mathf.Max(1, (int)(total / 100));

            for (int gy = 0; gy < gridY; gy++)
            {
                double py = b.Min.y + (gy + 0.5) * voxelSize;
                for (int gz = 0; gz < gridZ; gz++)
                {
                    double pz = b.Min.z + (gz + 0.5) * voxelSize;
                    for (int gx = 0; gx < gridX; gx++)
                    {
                        double px = b.Min.x + (gx + 0.5) * voxelSize;
                        var p = new g3.Vector3d(px, py, pz);

                        if (tree.FastWindingNumber(p) > 0.5)
                        {
                            Color32 color = SampleColor(mesh, tree, colors, hasUVs, p);
                            cells.Add(new VoxCell(gx, gy, gz, color));
                        }

                        if (++done % reportEvery == 0)
                        {
                            float frac = (float)((double)done / total);
                            if (!progress.Report(frac, $"Voxelizing… {done:N0}/{total:N0} cells, {cells.Count:N0} filled"))
                            {
                                throw new OperationCanceledException("Voxelization cancelled.");
                            }
                        }
                    }
                }
            }

            return new VoxResult(gridX, gridY, gridZ, cells);
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

            return colors.Texture!.GetPixelBilinear(u, v);
        }

        private static int GridDim(double extent, double voxelSize) =>
            Mathf.Clamp(Mathf.Max(1, Mathf.CeilToInt((float)(extent / voxelSize))), 1, MaxGridDim);

        // ---- OBJ geometry ----------------------------------------------------

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

            var mesh = new g3.DMesh3();
            mesh.EnableVertexUVs(g3.Vector2f.Zero);

            // Split vertices per unique (position, uv) so wedge UVs are preserved.
            var vertexMap = new Dictionary<(int pos, int uv), int>();

            int VertexFor(int posIdx, int uvIdx)
            {
                var key = (posIdx, uvIdx);
                if (vertexMap.TryGetValue(key, out int existing))
                {
                    return existing;
                }

                g3.Vector2f uv = uvIdx >= 0 && uvIdx < uvs.Count ? uvs[uvIdx] : g3.Vector2f.Zero;
                var info = new g3.NewVertexInfo(positions[posIdx]) { bHaveUV = true, uv = uv };
                int vid = mesh.AppendVertex(ref info);
                vertexMap[key] = vid;
                return vid;
            }

            int appended = 0;
            int dropped = 0;
            foreach (int[] face in faces)
            {
                int corners = face.Length / 2;
                // Fan-triangulate the (assumed convex) polygon.
                for (int i = 1; i < corners - 1; i++)
                {
                    int a = VertexFor(face[0], face[1]);
                    int bx = VertexFor(face[i * 2], face[i * 2 + 1]);
                    int c = VertexFor(face[(i + 1) * 2], face[(i + 1) * 2 + 1]);
                    if (a == bx || bx == c || a == c)
                    {
                        continue;
                    }
                    // g3 rejects triangles that would make an edge non-manifold; such
                    // drops degrade the winding field on imperfect generative meshes.
                    if (mesh.AppendTriangle(a, bx, c) >= 0)
                    {
                        appended++;
                    }
                    else
                    {
                        dropped++;
                    }
                }
            }

            if (dropped > 0)
            {
                Debug.LogWarning(
                    $"[VoxelsFromMeshSpike] Dropped {dropped:N0} non-manifold triangle(s) " +
                    $"of {appended + dropped:N0}; solid fill may be approximate.");
            }

            // UVs are usable only if faces referenced texture coords at all.
            hasUVs = uvs.Count > 0 && faces.Exists(f => f[1] >= 0);
            return mesh;
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

        // ---- Colour source (.mtl / map_Kd) -----------------------------------

        private sealed class ColorSource
        {
            public Texture2D? Texture { get; init; }
            public Color32 FlatColor { get; init; }
            public bool HasTexture => Texture != null;
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
                            return new ColorSource { Texture = tex };
                        }
                    }
                    Debug.LogWarning($"[VoxelsFromMeshSpike] map_Kd '{mapKd}' not found/loadable; using flat Kd colour.");
                }

                return new ColorSource { FlatColor = flat };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VoxelsFromMeshSpike] Failed to load colour source: {e.Message}; using mid-grey.");
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
                    return Path.IsPathRooted(name) ? name : Path.Combine(objDir, name);
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
