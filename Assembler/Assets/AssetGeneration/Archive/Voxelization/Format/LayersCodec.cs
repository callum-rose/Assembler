using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Converts between the layered-ASCII part encoding and a part-local
	/// <see cref="VoxelModel"/> grid (Y-up). Decoded grids index into the
	/// model-wide palette so every part of a model shares one palette and
	/// composition is a straight dictionary merge.
	/// </summary>
	public static class LayersCodec
	{
		public static VoxelModel Decode(LayersPartData data, IReadOnlyList<PaletteEntry> palette)
		{
			if (data.Layers.Count != data.Size.y)
			{
				throw new FormatException(
					$"Expected {data.Size.y} layers (size.y) but got {data.Layers.Count}.");
			}

			var keyToIndex = BuildKeyMap(palette);
			var voxels = new Dictionary<Vector3Int, byte>();

			for (var y = 0; y < data.Size.y; y++)
			{
				var rows = SplitRows(data.Layers[y]);
				if (rows.Count != data.Size.z)
				{
					throw new FormatException(
						$"Layer {y} has {rows.Count} rows but size.z is {data.Size.z}.");
				}

				for (var z = 0; z < data.Size.z; z++)
				{
					var row = rows[z];
					if (row.Length != data.Size.x)
					{
						throw new FormatException(
							$"Layer {y} row {z} is {row.Length} chars wide but size.x is {data.Size.x}.");
					}

					for (var x = 0; x < data.Size.x; x++)
					{
						var key = row[x];
						if (key is PaletteEntry.EmptyCell or PaletteEntry.EmptyKey)
						{
							continue;
						}

						if (!keyToIndex.TryGetValue(key, out var index))
						{
							throw new FormatException(
								$"Layer {y} row {z} col {x}: '{key}' is not a declared palette key.");
						}

						voxels[data.Offset + new Vector3Int(x, y, z)] = index;
					}
				}
			}

			return ToModel(voxels, palette);
		}

		/// <summary>Inverse of <see cref="Decode"/> for round-trip tests and debugging.</summary>
		public static LayersPartData Encode(VoxelModel grid, IReadOnlyList<PaletteEntry> palette, Vector3Int offset, Vector3Int size)
		{
			var layers = new List<string>(size.y);
			for (var y = 0; y < size.y; y++)
			{
				var sb = new StringBuilder();
				for (var z = 0; z < size.z; z++)
				{
					for (var x = 0; x < size.x; x++)
					{
						var cell = PaletteEntry.EmptyCell;
						if (grid.Voxels.TryGetValue(offset + new Vector3Int(x, y, z), out var index) &&
							index >= 1 && index <= palette.Count)
						{
							cell = palette[index - 1].Key;
						}

						sb.Append(cell);
					}

					if (z < size.z - 1)
					{
						sb.Append('\n');
					}
				}

				layers.Add(sb.ToString());
			}

			return new LayersPartData(size, offset, layers);
		}

		/// <summary>Wraps a voxel dictionary (already model-palette indexed) as a VoxelModel.</summary>
		public static VoxelModel ToModel(Dictionary<Vector3Int, byte> voxels, IReadOnlyList<PaletteEntry> palette)
		{
			var colours = palette.Select(e => e.Colour).ToArray();
			if (voxels.Count == 0)
			{
				return new VoxelModel(voxels, colours, Vector3Int.zero, Vector3Int.zero);
			}

			var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			foreach (var p in voxels.Keys)
			{
				min = Vector3Int.Min(min, p);
				max = Vector3Int.Max(max, p);
			}

			return new VoxelModel(voxels, colours, min, max);
		}

		private static Dictionary<char, byte> BuildKeyMap(IReadOnlyList<PaletteEntry> palette)
		{
			var map = new Dictionary<char, byte>();
			for (var i = 0; i < palette.Count; i++)
			{
				map[palette[i].Key] = (byte)(i + 1);
			}

			return map;
		}

		private static List<string> SplitRows(string layer) =>
			layer.Replace("\r", string.Empty)
				.Split('\n')
				.Select(r => r.TrimEnd())
				.Where(r => r.Length > 0)
				.ToList();
	}
}
