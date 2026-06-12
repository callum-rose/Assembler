using System;
using System.Collections.Generic;
using System.Text;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	public enum ProjectionFace
	{
		/// <summary>Viewer at +z looking along -z: u = x, v = y.</summary>
		Front,

		/// <summary>Viewer at +x looking along -x: u = z, v = y.</summary>
		Side,

		/// <summary>Viewer above looking along -y: u = x, v = z.</summary>
		Top,

		/// <summary>Viewer at -z looking along +z (behind the model): u = mirrored x, v = y.</summary>
		Back,
	}

	/// <summary>
	/// Orthographic projections of a Y-up voxel volume, used for silhouette
	/// validation and preview rendering. Grids are indexed [u, v] with v = 0 at
	/// the bottom of the projection; callers comparing against image-style
	/// top-first rows flip v.
	/// </summary>
	public static class VoxelProjector
	{
		public static ProjectionFace ParseFace(string face) => face.ToLowerInvariant() switch
		{
			"side" => ProjectionFace.Side,
			"top" => ProjectionFace.Top,
			"back" => ProjectionFace.Back,
			_ => ProjectionFace.Front,
		};

		public static bool[,] Occupancy(VoxelModel model, ProjectionFace face)
		{
			var (width, height) = Dimensions(model, face);
			var grid = new bool[width, height];
			foreach (var p in model.Voxels.Keys)
			{
				var (u, v, _) = MapToPlane(p - model.Min, model.Size, face);
				grid[u, v] = true;
			}

			return grid;
		}

		/// <summary>Nearest-voxel colour per cell; null where the projection is empty.</summary>
		public static Color32?[,] Colours(VoxelModel model, ProjectionFace face)
		{
			var (width, height) = Dimensions(model, face);
			var colours = new Color32?[width, height];
			var nearest = new int[width, height];
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					nearest[u, v] = int.MinValue;
				}
			}

			foreach (var kv in model.Voxels)
			{
				var (u, v, depth) = MapToPlane(kv.Key - model.Min, model.Size, face);
				if (depth > nearest[u, v])
				{
					nearest[u, v] = depth;
					colours[u, v] = kv.Value >= 1 && kv.Value <= model.Palette.Length
						? model.Palette[kv.Value - 1]
						: (Color32?)null;
				}
			}

			return colours;
		}

		/// <summary>
		/// Colour-keyed ASCII projection (nearest voxel wins), image-style top row
		/// first, '.' where empty. This is the feedback medium for authoring
		/// models: they write ASCII layers, so they read ASCII views.
		/// </summary>
		public static string Ascii(VoxelModel model, IReadOnlyList<PaletteEntry> palette, ProjectionFace face)
		{
			if (model.Voxels.Count == 0)
			{
				return "(empty)";
			}

			var (width, height) = Dimensions(model, face);
			var keys = new char[width, height];
			var nearest = new int[width, height];
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					keys[u, v] = PaletteEntry.EmptyCell;
					nearest[u, v] = int.MinValue;
				}
			}

			foreach (var kv in model.Voxels)
			{
				var (u, v, depth) = MapToPlane(kv.Key - model.Min, model.Size, face);
				if (depth > nearest[u, v])
				{
					nearest[u, v] = depth;
					keys[u, v] = kv.Value >= 1 && kv.Value <= palette.Count ? palette[kv.Value - 1].Key : '?';
				}
			}

			var sb = new StringBuilder();
			for (var v = height - 1; v >= 0; v--)
			{
				for (var u = 0; u < width; u++)
				{
					sb.Append(keys[u, v]);
				}

				if (v > 0)
				{
					sb.Append('\n');
				}
			}

			return sb.ToString();
		}

		private static (int width, int height) Dimensions(VoxelModel model, ProjectionFace face)
		{
			var size = model.Size;
			return face switch
			{
				ProjectionFace.Front or ProjectionFace.Back => (Math.Max(1, size.x), Math.Max(1, size.y)),
				ProjectionFace.Side => (Math.Max(1, size.z), Math.Max(1, size.y)),
				_ => (Math.Max(1, size.x), Math.Max(1, size.z)),
			};
		}

		private static (int u, int v, int depth) MapToPlane(Vector3Int p, Vector3Int size, ProjectionFace face) => face switch
		{
			ProjectionFace.Front => (p.x, p.y, p.z),
			ProjectionFace.Back => (size.x - 1 - p.x, p.y, -p.z),
			ProjectionFace.Side => (p.z, p.y, p.x),
			_ => (p.x, p.z, p.y),
		};
	}

	/// <summary>
	/// Compares a model's orthographic occupancy against a brief silhouette by
	/// nearest-neighbour resampling the projection into the silhouette grid and
	/// computing intersection-over-union.
	/// </summary>
	public static class SilhouetteMatcher
	{
		public static float Iou(bool[,] projection, SilhouetteSpec spec)
		{
			if (spec.IsEmpty || spec.Size.x <= 0 || spec.Size.y <= 0)
			{
				return 1f;
			}

			var width = spec.Size.x;
			var height = spec.Size.y;
			var projWidth = projection.GetLength(0);
			var projHeight = projection.GetLength(1);

			var intersection = 0;
			var union = 0;
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					// Rows are image-style: row 0 is the top of the silhouette.
					var row = height - 1 - v < spec.Rows.Count ? spec.Rows[height - 1 - v] : string.Empty;
					var expected = u < row.Length && SilhouetteSpec.IsSolid(row[u]);

					var pu = Mathf.Clamp(Mathf.FloorToInt((u + 0.5f) * projWidth / width), 0, projWidth - 1);
					var pv = Mathf.Clamp(Mathf.FloorToInt((v + 0.5f) * projHeight / height), 0, projHeight - 1);
					var actual = projection[pu, pv];

					if (expected && actual)
					{
						intersection++;
					}

					if (expected || actual)
					{
						union++;
					}
				}
			}

			return union == 0 ? 1f : (float)intersection / union;
		}
	}
}
