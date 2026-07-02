using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Kills Meshy's purple UV-gutter bleed at the source: rasterise which texels are actually
    /// covered by a UV triangle (texel-centre-in-triangle over the mesh's own UVs — only
    /// GetTriangle/GetVertexUV, no extra mesh data), then flood the island colours outward into the
    /// uncovered gutters with iterative 8-neighbour toroidal dilation. After enough passes every
    /// texel a bilinear sample can touch near an island edge holds a real island colour instead of
    /// the gutter fill, so nearest-surface samples can no longer land purple. Returns a rebuilt
    /// <see cref="ObjToVoxConverter.LoadedModel"/> with the dilated snapshot; the mesh is untouched.
    /// </summary>
    public static class UvIslandDilation
    {
        public const int DefaultPasses = 8;

        /// <summary>No-op (same model back) when the model has no texture or no UVs.</summary>
        public static ObjToVoxConverter.LoadedModel Apply(ObjToVoxConverter.LoadedModel model, int passes)
        {
            ObjToVoxConverter.TextureSnapshot? texture = model.Colors.Texture;
            if (texture is null || !model.HasUVs)
            {
                return model;
            }

            int width = texture.Width;
            int height = texture.Height;
            if (width <= 0 || height <= 0)
            {
                return model;
            }

            Color[] pixels = texture.CopyLinearPixels();
            bool[] covered = RasteriseCoverage(model.Mesh, width, height);
            Dilate(pixels, covered, width, height, Mathf.Max(1, passes));

            var dilated = new ObjToVoxConverter.TextureSnapshot(pixels, width, height);
            return new ObjToVoxConverter.LoadedModel(
                model.Mesh, model.HasUVs,
                new ObjToVoxConverter.ColorSource { Texture = dilated, FlatColor = model.Colors.FlatColor });
        }

        /// <summary>
        /// Texels whose centres lie inside some UV triangle (toroidal wrap for UVs outside [0,1)).
        /// Exposed for tests.
        /// </summary>
        public static bool[] RasteriseCoverage(g3.DMesh3 mesh, int width, int height)
        {
            var covered = new bool[width * height];
            foreach (int tid in mesh.TriangleIndices())
            {
                g3.Index3i tri = mesh.GetTriangle(tid);
                g3.Vector2f uv0 = mesh.GetVertexUV(tri.a);
                g3.Vector2f uv1 = mesh.GetVertexUV(tri.b);
                g3.Vector2f uv2 = mesh.GetVertexUV(tri.c);

                // Texel-space triangle corners: texel (x, y) has its centre at UV ((x+½)/W, (y+½)/H),
                // so UV u maps to texel-space u·W − ½.
                float ax = uv0.x * width - 0.5f, ay = uv0.y * height - 0.5f;
                float bx = uv1.x * width - 0.5f, by = uv1.y * height - 0.5f;
                float cx = uv2.x * width - 0.5f, cy = uv2.y * height - 0.5f;

                float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Mathf.Abs(area) < 1e-8f)
                {
                    continue; // degenerate UV triangle — nothing to rasterise
                }

                int minX = Mathf.FloorToInt(Mathf.Min(ax, bx, cx));
                int maxX = Mathf.CeilToInt(Mathf.Max(ax, bx, cx));
                int minY = Mathf.FloorToInt(Mathf.Min(ay, by, cy));
                int maxY = Mathf.CeilToInt(Mathf.Max(ay, by, cy));

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!InsideTriangle(x, y, ax, ay, bx, by, cx, cy, area))
                        {
                            continue;
                        }
                        covered[Wrap(x, width) + width * Wrap(y, height)] = true;
                    }
                }
            }
            return covered;
        }

        // Edge-function test, inclusive of edges (small epsilon) so texels straddling an island
        // border count as covered rather than staying gutter.
        private static bool InsideTriangle(
            int px, int py, float ax, float ay, float bx, float by, float cx, float cy, float area)
        {
            const float epsilon = 1e-4f;
            float sign = area > 0 ? 1f : -1f;
            float e0 = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) * sign;
            float e1 = ((cx - bx) * (py - by) - (cy - by) * (px - bx)) * sign;
            float e2 = ((ax - cx) * (py - cy) - (ay - cy) * (px - cx)) * sign;
            return e0 >= -epsilon && e1 >= -epsilon && e2 >= -epsilon;
        }

        // Iterative 8-neighbour toroidal flood: each pass, every uncovered texel adjacent to
        // covered texels takes their average colour and joins the covered set. Double-buffered so a
        // pass reads only the previous pass's frontier.
        private static void Dilate(Color[] pixels, bool[] covered, int width, int height, int passes)
        {
            var next = new bool[covered.Length];
            for (int pass = 0; pass < passes; pass++)
            {
                System.Array.Copy(covered, next, covered.Length);
                bool any = false;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = x + width * y;
                        if (covered[i])
                        {
                            continue;
                        }

                        var sum = Color.clear;
                        int count = 0;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dy == 0)
                                {
                                    continue;
                                }
                                int n = Wrap(x + dx, width) + width * Wrap(y + dy, height);
                                if (covered[n])
                                {
                                    sum += pixels[n];
                                    count++;
                                }
                            }
                        }

                        if (count > 0)
                        {
                            pixels[i] = sum / count;
                            next[i] = true;
                            any = true;
                        }
                    }
                }

                (covered, next) = (next, covered);
                if (!any)
                {
                    break;
                }
            }
        }

        private static int Wrap(int v, int max) => ((v % max) + max) % max;
    }
}
