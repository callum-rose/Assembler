using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Minimal MagicaVoxel <c>.vox</c> (version 150) binary writer.
    ///
    /// Takes filled cells produced in the mesh's Y-up grid space, remaps them to
    /// MagicaVoxel's Z-up space and writes a single-model VOX file
    /// (MAIN → SIZE, XYZI, RGBA). When the model uses ≤255 distinct colours (e.g.
    /// after quantisation) those colours are written verbatim into the palette;
    /// otherwise it falls back to a fixed 3-3-2 palette.
    ///
    /// Spec reference: https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox.txt
    /// </summary>
    public static class VoxWriter
    {
        // VOX colour index 0 means "empty", so voxel indices live in 1..255.
        private const int MaxColorIndex = 255;

        public static void Write(string path, VoxResult result)
        {
            // Prefer an exact palette so distinct (quantised) colours survive unaltered;
            // fall back to 3-3-2 only when the model has more than 255 distinct colours.
            byte[] palette;
            Func<Color32, byte> colorIndexOf;
            var exactPalette = new byte[256 * 4];
            if (TryBuildExactPalette(result.Cells, out Dictionary<int, byte> exactSlots, exactPalette))
            {
                palette = exactPalette;
                colorIndexOf = c => exactSlots[ColorKey(c)];
            }
            else
            {
                palette = BuildPalette332();
                colorIndexOf = ColorIndex332;
            }

            // Remap mesh-grid cells (gx, gy=up, gz) → MagicaVoxel (vx, vy, vz=up).
            //
            // The mesh is in OBJ space (right-handed: +X right, +Y up, +Z toward
            // viewer); MagicaVoxel is right-handed with +Z up and +Y pointing away.
            // A bare axis swap (x,y,z)→(x,z,y) is a reflection (mirrors the model),
            // so we also flip the depth axis to keep handedness and avoid a mirror.
            //
            // VERIFY against an asymmetric mesh (e.g. an "L"): if the result is
            // mirrored, drop the `(gridZ - 1 - gz)` flip back to a plain `gz`
            // (or flip a different axis instead).
            int gridX = result.GridX;
            int gridY = result.GridY;
            int gridZ = result.GridZ;

            int sizeX = gridX;
            int sizeY = gridZ;
            int sizeZ = gridY;

            var voxels = new List<(byte x, byte y, byte z, byte i)>(result.Cells.Count);
            foreach (VoxCell cell in result.Cells)
            {
                byte vx = (byte)cell.X;
                byte vy = (byte)(gridZ - 1 - cell.Z);
                byte vz = (byte)cell.Y;
                voxels.Add((vx, vy, vz, colorIndexOf(cell.Color)));
            }

            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            writer.Write(new[] { (byte)'V', (byte)'O', (byte)'X', (byte)' ' });
            writer.Write(150);

            byte[] sizeChunk = BuildChunk("SIZE", w =>
            {
                w.Write(sizeX);
                w.Write(sizeY);
                w.Write(sizeZ);
            });

            byte[] xyziChunk = BuildChunk("XYZI", w =>
            {
                w.Write(voxels.Count);
                foreach ((byte x, byte y, byte z, byte i) in voxels)
                {
                    w.Write(x);
                    w.Write(y);
                    w.Write(z);
                    w.Write(i);
                }
            });

            byte[] rgbaChunk = BuildChunk("RGBA", w => w.Write(palette));

            // MAIN has no content of its own; its children are the chunks above.
            int childBytes = sizeChunk.Length + xyziChunk.Length + rgbaChunk.Length;
            WriteChunkHeader(writer, "MAIN", 0, childBytes);
            writer.Write(sizeChunk);
            writer.Write(xyziChunk);
            writer.Write(rgbaChunk);
        }

        private static int ColorKey(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        /// <summary>
        /// Assigns each distinct voxel colour a palette slot (1..255) and fills the
        /// RGBA table with those exact colours. Returns false (leaving outputs to be
        /// discarded) if the model has more than 255 distinct colours.
        /// </summary>
        private static bool TryBuildExactPalette(
            IReadOnlyList<VoxCell> cells, out Dictionary<int, byte> slotByColor, byte[] paletteOut)
        {
            slotByColor = new Dictionary<int, byte>();
            foreach (VoxCell cell in cells)
            {
                int key = ColorKey(cell.Color);
                if (slotByColor.ContainsKey(key))
                {
                    continue;
                }
                if (slotByColor.Count >= MaxColorIndex)
                {
                    return false; // > 255 distinct colours — caller uses 3-3-2 instead.
                }

                int slot = slotByColor.Count + 1; // 1..255
                slotByColor[key] = (byte)slot;
                int offset = (slot - 1) * 4;
                paletteOut[offset + 0] = cell.Color.r;
                paletteOut[offset + 1] = cell.Color.g;
                paletteOut[offset + 2] = cell.Color.b;
                paletteOut[offset + 3] = 255;
            }
            return true;
        }

        // 3-3-2 RGB quantisation: 3 bits red, 3 bits green, 2 bits blue → code 0..255.
        private static byte ColorIndex332(Color32 c)
        {
            int code = (c.r & 0xE0) | ((c.g & 0xE0) >> 3) | ((c.b & 0xC0) >> 6);
            // Reserve index 0 for "empty"; collapse pure-black-ish code 0 onto 1.
            return (byte)Mathf.Clamp(code == 0 ? 1 : code, 1, MaxColorIndex);
        }

        /// <summary>
        /// 256-entry RGBA table. Voxel colour index <c>i</c> (1..255) reads array
        /// position <c>i-1</c> (per the VOX spec's off-by-one), so we store the
        /// representative colour for code <c>i</c> at <c>(i-1)</c>.
        /// </summary>
        private static byte[] BuildPalette332()
        {
            var palette = new byte[256 * 4];
            for (int i = 1; i <= MaxColorIndex; i++)
            {
                int r3 = (i >> 5) & 0x7;
                int g3 = (i >> 2) & 0x7;
                int b2 = i & 0x3;

                int offset = (i - 1) * 4;
                palette[offset + 0] = (byte)(r3 * 255 / 7);
                palette[offset + 1] = (byte)(g3 * 255 / 7);
                palette[offset + 2] = (byte)(b2 * 255 / 3);
                palette[offset + 3] = 255;
            }
            return palette;
        }

        private static byte[] BuildChunk(string id, System.Action<BinaryWriter> writeContent)
        {
            using var contentStream = new MemoryStream();
            using (var contentWriter = new BinaryWriter(contentStream))
            {
                writeContent(contentWriter);
            }
            byte[] content = contentStream.ToArray();

            using var chunkStream = new MemoryStream();
            using (var chunkWriter = new BinaryWriter(chunkStream))
            {
                WriteChunkHeader(chunkWriter, id, content.Length, 0);
                chunkWriter.Write(content);
            }
            return chunkStream.ToArray();
        }

        private static void WriteChunkHeader(BinaryWriter writer, string id, int contentBytes, int childBytes)
        {
            writer.Write(new[] { (byte)id[0], (byte)id[1], (byte)id[2], (byte)id[3] });
            writer.Write(contentBytes);
            writer.Write(childBytes);
        }
    }
}
