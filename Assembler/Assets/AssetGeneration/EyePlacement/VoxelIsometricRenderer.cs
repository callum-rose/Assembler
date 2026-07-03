using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Renders a <see cref="VoxelModel"/> to a PNG from a <see cref="VoxelViewProjection"/>,
    /// so the placement flow can show a vision model a clean picture of the creature and
    /// then reproject the model's 2D picks through the very same projection. A simple CPU
    /// painter's splat (no Unity scene/camera) keeps it deterministic and runnable in batch
    /// mode and tests: each voxel is drawn as a shaded square, far to near, lit by its
    /// outward face direction so the form reads as 3D rather than a flat silhouette.
    /// </summary>
    public static class VoxelIsometricRenderer
    {
        private static readonly Vector3 LightDirection = new Vector3(-0.5f, -0.3f, 1f).normalized;
        private static readonly Color32 Background = new(240, 240, 240, 255);

        /// <summary>Renders to raw RGBA pixels plus PNG bytes; the editor uses the bytes for preview.</summary>
        public static byte[] RenderPng(VoxelModel model, VoxelViewProjection projection, int imageSize)
        {
            imageSize = Mathf.Clamp(imageSize, 64, 2048);
            var texture = Render(model, projection, imageSize);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return png;
        }

        /// <summary>Renders to a readable <see cref="Texture2D"/>. Caller owns disposal.</summary>
        public static Texture2D Render(VoxelModel model, VoxelViewProjection projection, int imageSize)
        {
            imageSize = Mathf.Clamp(imageSize, 64, 2048);
            var pixels = new Color32[imageSize * imageSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Background;
            }

            int half = Mathf.Max(1, Mathf.RoundToInt(projection.VoxelPixelSize(imageSize) * 0.5f + 0.5f));

            // Painter's order: sort keys by depth descending so nearer voxels overdraw farther ones.
            var voxels = new (Vector3Int cell, float depth)[model.Voxels.Count];
            int n = 0;
            foreach (var kv in model.Voxels)
            {
                var centre = (Vector3)kv.Key + new Vector3(0.5f, 0.5f, 0.5f);
                voxels[n++] = (kv.Key, Vector3.Dot(centre, projection.View.Forward));
            }
            System.Array.Sort(voxels, (a, b) => b.depth.CompareTo(a.depth));

            foreach (var (cell, _) in voxels)
            {
                var centre = (Vector3)cell + new Vector3(0.5f, 0.5f, 0.5f);
                Vector2 n01 = projection.WorldToNormalized(centre);
                int cx = Mathf.RoundToInt(n01.x * imageSize);
                // n01.y is y-down (0 = top). Texture2D row 0 is the bottom and EncodeToPNG
                // preserves that, so flip here to keep the PNG's visual top matching the
                // normalised coords the vision model reports its picks in.
                int cy = imageSize - 1 - Mathf.RoundToInt(n01.y * imageSize);

                byte index = model.Voxels[cell];
                Color32 baseColour = ColourFor(model, index);
                float shade = Shade(model, cell);
                var lit = new Color32(
                    (byte)Mathf.RoundToInt(baseColour.r * shade),
                    (byte)Mathf.RoundToInt(baseColour.g * shade),
                    (byte)Mathf.RoundToInt(baseColour.b * shade),
                    255);

                FillSquare(pixels, imageSize, cx, cy, half, lit);
            }

            var texture = new Texture2D(imageSize, imageSize, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // Lambert-ish shading from the voxel's outward direction (sum of exposed faces),
        // so flat-coloured voxels still convey which way a surface turns.
        private static float Shade(VoxelModel model, Vector3Int cell)
        {
            Vector3 outward = Vector3.zero;
            foreach (var offset in FaceOffsets)
            {
                if (!model.Voxels.ContainsKey(cell + offset))
                {
                    outward += offset;
                }
            }

            if (outward == Vector3.zero)
            {
                return 0.85f;
            }

            float diffuse = Mathf.Max(0f, Vector3.Dot(outward.normalized, LightDirection));
            return Mathf.Clamp01(0.4f + 0.6f * diffuse);
        }

        private static Color32 ColourFor(VoxelModel model, byte index)
        {
            // Voxel values are 1-based into the compact palette (see VoxReader).
            int slot = index - 1;
            return slot >= 0 && slot < model.Palette.Length
                ? model.Palette[slot]
                : new Color32(180, 180, 180, 255);
        }

        private static void FillSquare(Color32[] pixels, int size, int cx, int cy, int half, Color32 colour)
        {
            int x0 = Mathf.Max(0, cx - half), x1 = Mathf.Min(size - 1, cx + half);
            int y0 = Mathf.Max(0, cy - half), y1 = Mathf.Min(size - 1, cy + half);
            for (int y = y0; y <= y1; y++)
            {
                int row = y * size;
                for (int x = x0; x <= x1; x++)
                {
                    pixels[row + x] = colour;
                }
            }
        }

        private static readonly Vector3Int[] FaceOffsets =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };
    }
}
