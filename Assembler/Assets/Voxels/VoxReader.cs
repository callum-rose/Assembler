using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assembler.Voxels
{
	/// <summary>
	/// Inverse of <see cref="VoxWriter"/>: parses a MagicaVoxel .vox byte stream
	/// (RIFF: MAIN -> SIZE, XYZI, optional RGBA) into a <see cref="VoxelModel"/>.
	/// Built to round-trip files produced by <see cref="VoxWriter"/>; handles the
	/// single SIZE+XYZI pair Goxel/MagicaVoxel commonly emit and skips unknown
	/// chunks such as nTRN/nGRP/nSHP/MATL/LAYR.
	/// </summary>
	public static class VoxReader
	{
		public static VoxelModel Read(byte[] bytes)
		{
			if (bytes == null) throw new ArgumentNullException(nameof(bytes));

			using var ms = new MemoryStream(bytes);
			using var r = new BinaryReader(ms);

			if (ms.Length < 8) throw new InvalidDataException(".vox file too short.");
			var magic = new string(r.ReadChars(4));
			if (magic != "VOX ") throw new InvalidDataException(".vox file missing 'VOX ' magic.");
			r.ReadInt32(); // version

			var voxels = new List<(byte x, byte y, byte z, byte index)>();
			Color32[]? palette = null;

			while (ms.Position < ms.Length)
			{
				if (ms.Length - ms.Position < 12) break;
				var id = new string(r.ReadChars(4));
				var contentSize = r.ReadInt32();
				var childrenSize = r.ReadInt32();
				var contentEnd = ms.Position + contentSize;

				switch (id)
				{
					case "MAIN":
						// MAIN has no content of its own; children follow inline.
						// Don't skip childrenSize — walk into the children as siblings.
						ms.Position = contentEnd;
						continue;
					case "SIZE":
						// Bounding-box size is recovered from voxel positions; we don't
						// need the declared SIZE chunk values.
						break;
					case "XYZI":
						var count = r.ReadInt32();
						for (var i = 0; i < count; i++)
						{
							var vx = r.ReadByte();
							var vy = r.ReadByte();
							var vz = r.ReadByte();
							var vi = r.ReadByte();
							voxels.Add((vx, vy, vz, vi));
						}
						break;
					case "RGBA":
						palette = new Color32[256];
						for (var i = 0; i < 256; i++)
						{
							var cr = r.ReadByte();
							var cg = r.ReadByte();
							var cb = r.ReadByte();
							var ca = r.ReadByte();
							palette[i] = new Color32(cr, cg, cb, ca);
						}
						break;
				}

				// Skip past this chunk's content and any children we ignored.
				ms.Position = contentEnd + childrenSize;
			}

			if (voxels.Count == 0)
			{
				return new VoxelModel(
					new Dictionary<Vector3Int, byte>(),
					Array.Empty<Color32>(),
					Vector3Int.zero,
					Vector3Int.zero);
			}

			// Remap palette: only keep colours actually referenced, so we round-trip
			// the way VoxWriter (and GoxelTextParser) build palettes — sequential,
			// 1-based, no holes.
			var usedIndices = new SortedSet<byte>();
			foreach (var v in voxels) usedIndices.Add(v.index);

			var remap = new Dictionary<byte, byte>();
			var compactPalette = new List<Color32>();
			foreach (var oldIndex in usedIndices)
			{
				// MagicaVoxel: voxel index i refers to palette[i-1]. Default palette
				// is used if no RGBA chunk was present.
				Color32 colour = palette != null && oldIndex >= 1
					? palette[oldIndex - 1]
					: DefaultPaletteColor(oldIndex);
				compactPalette.Add(colour);
				remap[oldIndex] = (byte)compactPalette.Count;
			}

			var dict = new Dictionary<Vector3Int, byte>(voxels.Count);
			Vector3Int min = new(int.MaxValue, int.MaxValue, int.MaxValue);
			Vector3Int max = new(int.MinValue, int.MinValue, int.MinValue);
			foreach (var v in voxels)
			{
				var pos = new Vector3Int(v.x, v.y, v.z);
				dict[pos] = remap[v.index];
				min = Vector3Int.Min(min, pos);
				max = Vector3Int.Max(max, pos);
			}

			return new VoxelModel(dict, compactPalette.ToArray(), min, max);
		}

		// Fallback colour when a .vox has no RGBA chunk: opaque grey. We only hit
		// this if someone hands us a file we didn't write; good enough for review.
		private static Color32 DefaultPaletteColor(byte index)
		{
			var v = (byte)Mathf.Clamp(index, 32, 224);
			return new Color32(v, v, v, 255);
		}
	}
}
