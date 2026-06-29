using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
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
        // VOX colour index 0 means "empty", so voxel indices live in 1..255. We additionally
        // RESERVE voxel index 1 (palette entry 0) as an unused, opaque dummy and start real
        // colours at index 2: the Voxel Toolkit importer subtracts 1 from each voxel byte and
        // then hardcodes material index 0 as empty air (FacesGenerationJob: Material==0 emits no
        // faces; it is also force-flagged Invalid/Transparent and never recomputed), so any real
        // colour written to that slot renders invisible — dropping every voxel of that colour.
        // Real colours therefore occupy voxel indices 2..255 (palette entries 1..254).
        private const int FirstColorIndex = 2;
        private const int MaxColorIndex = 255;
        private const int MaxColors = MaxColorIndex - FirstColorIndex + 1; // 254

        public static void Write(string path, VoxResult result)
        {
            // Prefer an exact palette so distinct (quantised) colours survive unaltered;
            // fall back to median-cut only when the model has more than 254 distinct colours.
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
                var (medianPalette, medianSlots) = BuildMedianCutPalette(result.Cells, MaxColors);
                palette = medianPalette;
                colorIndexOf = c => medianSlots[ColorKey(c)];
            }

            // Force every palette entry fully opaque. We never write transparency, but both
            // builders leave UNUSED slots zero-filled (alpha 0), and importers that derive a
            // material from the palette can treat any alpha<255 entry as transparent/glass
            // (e.g. Voxel Toolkit flags alpha<0.99) — so a stray index/sample into an unused
            // slot renders see-through. Opaque-filling the whole table removes that hazard.
            for (int e = 0; e < 256; e++)
            {
                palette[e * 4 + 3] = 255;
            }

            // Remap mesh-grid cells (gx, gy=up, gz) → MagicaVoxel (vx, vy, vz=up).
            //
            // The mesh is in OBJ space (right-handed: +X right, +Y up, +Z toward
            // viewer); MagicaVoxel is right-handed with +Z up and +Y pointing away.
            // A bare axis swap (x,y,z)→(x,z,y) is a reflection (mirrors the model),
            // so we rotate 180° about the up (vox-Z) axis on top of it — flipping
            // BOTH the width and depth axes — which keeps handedness (a proper
            // rotation, det +1) and lands the model facing the viewer the right way
            // round. (A plain `vx = gx` came out turned 180° about the vertical.)
            //
            // VERIFY against an asymmetric mesh (e.g. an "L"): this only ever rotates,
            // never mirrors. If it still reads as mirror-imaged, the source mesh's own
            // handedness is the culprit (e.g. the FBX Z-negation), not this mapping.
            int gridX = result.GridX;
            int gridY = result.GridY;
            int gridZ = result.GridZ;

            int sizeX = gridX;
            int sizeY = gridZ;
            int sizeZ = gridY;

            var voxels = new List<(byte x, byte y, byte z, byte i)>(result.Cells.Count);
            foreach (VoxCell cell in result.Cells)
            {
                byte vx = (byte)(gridX - 1 - cell.X);
                byte vy = (byte)cell.Z;
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
        /// Assigns each distinct voxel colour a palette slot (2..255 — slot 1 is reserved, see
        /// <see cref="FirstColorIndex"/>) and fills the RGBA table with those exact colours.
        /// Returns false (leaving outputs to be discarded) if the model has more than 254
        /// distinct colours.
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
                if (slotByColor.Count >= MaxColors)
                {
                    return false; // > 254 distinct colours — caller uses median-cut instead.
                }

                int slot = slotByColor.Count + FirstColorIndex; // 2..255
                slotByColor[key] = (byte)slot;
                int offset = (slot - 1) * 4;
                paletteOut[offset + 0] = cell.Color.r;
                paletteOut[offset + 1] = cell.Color.g;
                paletteOut[offset + 2] = cell.Color.b;
                paletteOut[offset + 3] = 255;
            }
            return true;
        }

        /// <summary>
        /// Median-cut quantisation to <paramref name="maxColors"/> swatches when the model has
        /// more than 255 distinct colours. Unlike a fixed 3-3-2 palette (whose 2-bit blue channel
        /// turns neutral greys into lavender — R/G snap to 8 levels, B to only 4), this derives the
        /// palette from the model's <i>actual</i> colours: each box's representative is the
        /// population-weighted average of its members, so a cloud of near-neutral greys collapses to
        /// a neutral grey. Returns the 256-entry RGBA table plus a colour→slot map. Slots start at
        /// <see cref="FirstColorIndex"/> (slot 1 is the reserved dummy), so they span
        /// <c>2..maxColors+1</c>.
        /// </summary>
        private static (byte[] palette, Dictionary<int, byte> indexOf) BuildMedianCutPalette(
            IReadOnlyList<VoxCell> cells, int maxColors)
        {
            var counts = new Dictionary<int, int>();
            foreach (VoxCell cell in cells)
            {
                int key = ColorKey(cell.Color);
                counts.TryGetValue(key, out int existing);
                counts[key] = existing + 1;
            }

            var initial = new List<(int r, int g, int b, int n)>(counts.Count);
            foreach (KeyValuePair<int, int> kv in counts)
            {
                initial.Add(((kv.Key >> 16) & 0xFF, (kv.Key >> 8) & 0xFF, kv.Key & 0xFF, kv.Value));
            }

            var boxes = new List<List<(int r, int g, int b, int n)>> { initial };
            while (boxes.Count < maxColors)
            {
                // Pick the box with the widest single-channel spread and split it at the
                // population median along that channel.
                int target = -1, targetRange = 0, targetChannel = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    List<(int r, int g, int b, int n)> box = boxes[i];
                    if (box.Count < 2)
                    {
                        continue;
                    }
                    int rmin = 255, rmax = 0, gmin = 255, gmax = 0, bmin = 255, bmax = 0;
                    foreach ((int r, int g, int b, int n) c in box)
                    {
                        rmin = Math.Min(rmin, c.r); rmax = Math.Max(rmax, c.r);
                        gmin = Math.Min(gmin, c.g); gmax = Math.Max(gmax, c.g);
                        bmin = Math.Min(bmin, c.b); bmax = Math.Max(bmax, c.b);
                    }
                    int range = rmax - rmin, channel = 0;
                    if (gmax - gmin > range) { range = gmax - gmin; channel = 1; }
                    if (bmax - bmin > range) { range = bmax - bmin; channel = 2; }
                    if (range > targetRange) { targetRange = range; target = i; targetChannel = channel; }
                }
                if (target < 0)
                {
                    break; // every box is a single colour — can't split further
                }

                List<(int r, int g, int b, int n)> split = boxes[target];
                int ch = targetChannel;
                split.Sort((x, y) =>
                    (ch == 0 ? x.r : ch == 1 ? x.g : x.b).CompareTo(ch == 0 ? y.r : ch == 1 ? y.g : y.b));
                int total = 0;
                foreach ((int r, int g, int b, int n) c in split)
                {
                    total += c.n;
                }
                int acc = 0, at = 1;
                for (int i = 0; i < split.Count; i++)
                {
                    acc += split[i].n;
                    if (acc * 2 >= total)
                    {
                        at = Math.Max(1, Math.Min(split.Count - 1, i + 1));
                        break;
                    }
                }
                boxes[target] = split.GetRange(0, at);
                boxes.Add(split.GetRange(at, split.Count - at));
            }

            var palette = new byte[256 * 4];
            var indexOf = new Dictionary<int, byte>();
            for (int i = 0; i < boxes.Count; i++)
            {
                List<(int r, int g, int b, int n)> box = boxes[i];
                long rs = 0, gs = 0, bs = 0, ns = 0;
                foreach ((int r, int g, int b, int n) c in box)
                {
                    rs += (long)c.r * c.n; gs += (long)c.g * c.n; bs += (long)c.b * c.n; ns += c.n;
                }
                var slot = (byte)(i + FirstColorIndex); // 2..maxColors+1
                int offset = (slot - 1) * 4;
                palette[offset + 0] = (byte)(rs / ns);
                palette[offset + 1] = (byte)(gs / ns);
                palette[offset + 2] = (byte)(bs / ns);
                palette[offset + 3] = 255;
                foreach ((int r, int g, int b, int n) c in box)
                {
                    indexOf[(c.r << 16) | (c.g << 8) | c.b] = slot;
                }
            }
            return (palette, indexOf);
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
