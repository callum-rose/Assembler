using System.Collections.Generic;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>Knobs for the orbit contact sheet.</summary>
    public sealed record MontageOptions
    {
        public int ViewCount { get; init; } = 8;
        public float PitchDegrees { get; init; } = 30f;
        public int CellSize { get; init; } = 320;
        public int Columns { get; init; } = 4;

        /// <summary>Ring radius (voxels) drawn for a ground-truth eye that specifies none of its own.</summary>
        public float DefaultToleranceVoxels { get; init; } = 2.5f;
    }

    /// <summary>
    /// The human-review artifact for issue #479: renders the model from a ring of yaws and draws the
    /// ground-truth eye regions (cyan) and the resolved anchors on top of each — green when the
    /// anchor reached its ground-truth eye, red when it didn't, grey for an unmatched extra. Anchors
    /// on the far side of the model for a given view are drawn hollow (masked), so orbiting reveals
    /// the errors a single view hides: both eyes on one side, eyes on the top, off-model anchors.
    /// One montage per model. CPU splat render, so it works headless.
    /// </summary>
    public static class EyeMontage
    {
        private static readonly Color32 Background = new(24, 24, 28, 255);
        private static readonly Color32 GroundTruthColour = new(70, 200, 235, 255);
        private static readonly Color32 PassColour = new(90, 220, 120, 255);
        private static readonly Color32 FailColour = new(235, 80, 80, 255);
        private static readonly Color32 ExtraColour = new(170, 170, 170, 255);

        public static byte[] Render(
            VoxelModel model,
            EyeGroundTruth truth,
            IReadOnlyList<EyeAnchor> anchors,
            ModelScore score,
            MontageOptions options)
        {
            int cell = Mathf.Clamp(options.CellSize, 64, 1024);
            int cols = Mathf.Max(1, Mathf.Min(options.Columns, options.ViewCount));
            int rows = Mathf.CeilToInt(options.ViewCount / (float)cols);
            int width = cols * cell;
            int height = rows * cell;

            var montage = new Color32[width * height];
            for (int i = 0; i < montage.Length; i++)
            {
                montage[i] = Background;
            }

            Dictionary<EyeAnchor, bool> anchorPass = BuildAnchorPassLookup(score);

            IReadOnlyList<float> yaws = ModelOrientation.CandidateYaws(options.ViewCount);
            for (int v = 0; v < yaws.Count; v++)
            {
                var view = OrthographicView.FromZUpAngles(yaws[v], options.PitchDegrees);
                var projection = new VoxelViewProjection(view, model);
                Color32[] tile = RenderTile(model, truth, anchors, anchorPass, projection, cell, options);

                int col = v % cols;
                int row = v / cols;
                Blit(tile, cell, cell, montage, width, col * cell, row * cell);
            }

            // A thin border across the whole sheet: green when the model passed, red when it failed.
            DrawBorder(montage, width, height, 3, score.Pass ? PassColour : FailColour);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(montage);
            texture.Apply();
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        private static Dictionary<EyeAnchor, bool> BuildAnchorPassLookup(ModelScore score)
        {
            var lookup = new Dictionary<EyeAnchor, bool>();
            foreach (EyeScore eye in score.Eyes)
            {
                if (eye.Anchor is { } anchor)
                {
                    lookup[anchor] = eye.Pass;
                }
            }

            return lookup;
        }

        private static Color32[] RenderTile(
            VoxelModel model,
            EyeGroundTruth truth,
            IReadOnlyList<EyeAnchor> anchors,
            IReadOnlyDictionary<EyeAnchor, bool> anchorPass,
            VoxelViewProjection projection,
            int size,
            MontageOptions options)
        {
            Texture2D render = VoxelIsometricRenderer.Render(model, projection, size);
            Color32[] pixels = render.GetPixels32();
            Object.DestroyImmediate(render);

            float voxelPixel = projection.VoxelPixelSize(size);

            // Ground-truth acceptance regions first, so anchors draw over them.
            foreach (GroundTruthEye eye in truth.Eyes)
            {
                Vector3 centre = eye.Center + Vector3.one * 0.5f;
                float radius = (eye.RadiusVoxels > 0f ? eye.RadiusVoxels : options.DefaultToleranceVoxels) * voxelPixel;
                if (ToPixel(projection, centre, size, out int gx, out int gy))
                {
                    DrawRing(pixels, size, gx, gy, Mathf.Max(3, Mathf.RoundToInt(radius)), GroundTruthColour);
                }
            }

            foreach (EyeAnchor anchor in anchors)
            {
                if (!ToPixel(projection, anchor.Position, size, out int ax, out int ay))
                {
                    continue;
                }

                Color32 colour = anchorPass.TryGetValue(anchor, out bool passed)
                    ? (passed ? PassColour : FailColour)
                    : ExtraColour;
                int dot = Mathf.Max(3, Mathf.RoundToInt(voxelPixel * 0.6f));

                // Hollow when the anchor is on the far side of the model from this view.
                if (EyeVisibility.IsMasked(model, anchor, projection))
                {
                    DrawRing(pixels, size, ax, ay, dot, colour);
                }
                else
                {
                    DrawDisc(pixels, size, ax, ay, dot, colour);
                }
            }

            return pixels;
        }

        // Normalised (y-down) → texture pixel. The renderer flips Y (row 0 = bottom), so mirror that.
        private static bool ToPixel(VoxelViewProjection projection, Vector3 world, int size, out int px, out int py)
        {
            Vector2 n01 = projection.WorldToNormalized(world);
            px = Mathf.RoundToInt(n01.x * size);
            py = size - 1 - Mathf.RoundToInt(n01.y * size);
            return px >= -size && px < 2 * size && py >= -size && py < 2 * size;
        }

        private static void DrawDisc(Color32[] pixels, int size, int cx, int cy, int radius, Color32 colour)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= r2)
                    {
                        SetPixel(pixels, size, cx + dx, cy + dy, colour);
                    }
                }
            }
        }

        private static void DrawRing(Color32[] pixels, int size, int cx, int cy, int radius, Color32 colour)
        {
            int inner = (radius - 1) * (radius - 1);
            int outer = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int d2 = dx * dx + dy * dy;
                    if (d2 <= outer && d2 >= inner)
                    {
                        SetPixel(pixels, size, cx + dx, cy + dy, colour);
                    }
                }
            }
        }

        private static void DrawBorder(Color32[] pixels, int width, int height, int thickness, Color32 colour)
        {
            for (int t = 0; t < thickness; t++)
            {
                for (int x = 0; x < width; x++)
                {
                    SetPixel(pixels, width, x, t, colour);
                    SetPixel(pixels, width, x, height - 1 - t, colour);
                }

                for (int y = 0; y < height; y++)
                {
                    SetPixel(pixels, width, t, y, colour);
                    SetPixel(pixels, width, width - 1 - t, y, colour);
                }
            }
        }

        private static void Blit(Color32[] src, int srcW, int srcH, Color32[] dst, int dstW, int atX, int atY)
        {
            for (int y = 0; y < srcH; y++)
            {
                int dstRow = (atY + y) * dstW + atX;
                int srcRow = y * srcW;
                for (int x = 0; x < srcW; x++)
                {
                    dst[dstRow + x] = src[srcRow + x];
                }
            }
        }

        private static void SetPixel(Color32[] pixels, int width, int x, int y, Color32 colour)
        {
            int height = pixels.Length / width;
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            pixels[y * width + x] = colour;
        }
    }
}
