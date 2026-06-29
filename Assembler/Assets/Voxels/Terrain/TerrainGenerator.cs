using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Voxels.Terrain
{
	/// <summary>
	/// Builds a <see cref="VoxelModel"/> from a <see cref="TerrainSpec"/> by driving the
	/// existing <see cref="VoxelBuilder"/>: a base pass fills a surface skin per column,
	/// modifier ops stamp/carve primitives in order, and the enclosure adds walls/ceiling.
	/// Pure runtime code (no editor types), so it is safe to reuse on-device later.
	/// </summary>
	public static class TerrainGenerator
	{
		public static VoxelModel Generate(TerrainSpec spec) => Generate(spec, VoxelScriptLimits.Default);

		public static VoxelModel Generate(TerrainSpec spec, VoxelScriptLimits limits, CancellationToken ct = default)
		{
			var builder = new VoxelBuilder(limits, ct);
			BuildBase(builder, spec);
			foreach (var op in spec.Ops)
			{
				ApplyOp(builder, op, limits);
			}

			BuildEnclosure(builder, spec);
			return builder.Build();
		}

		private static void BuildBase(VoxelBuilder builder, TerrainSpec spec)
		{
			var size = spec.Size;
			var skin = Mathf.Max(1, spec.SkinThickness);
			var colour = spec.Base.Colour;

			for (var x = 0; x < size.x; x++)
			{
				for (var y = 0; y < size.y; y++)
				{
					var top = ColumnHeight(spec, x, y);
					var bottom = Mathf.Max(0, top - skin + 1);
					for (var z = bottom; z <= top; z++)
					{
						builder.Set(x, y, z, colour);
					}
				}
			}
		}

		private static int ColumnHeight(TerrainSpec spec, int x, int y)
		{
			var maxZ = spec.Size.z - 1;
			var baseOp = spec.Base;
			if (baseOp.Type == BaseKind.Flat || baseOp.Noise == null)
			{
				return Mathf.Clamp(baseOp.BaseHeight, 0, maxZ);
			}

			var n = baseOp.Noise;
			var h01 = Noise.HeightField01(
				n.Kind, spec.Seed, x, y, n.Octaves, n.Frequency, n.Lacunarity, n.Gain, n.DomainWarp);
			var height = baseOp.BaseHeight + Mathf.RoundToInt(n.Amplitude * h01);
			return Mathf.Clamp(height, 0, maxZ);
		}

		private static void ApplyOp(VoxelBuilder builder, ModifierOp op, VoxelScriptLimits limits)
		{
			var cells = RasterShape(op, limits);
			if (op.Op == OpKind.Carve)
			{
				foreach (var c in cells)
				{
					builder.Clear(c.x, c.y, c.z);
				}

				return;
			}

			foreach (var c in cells)
			{
				if (op.Combine == CombineMode.Add && builder.Has(c.x, c.y, c.z))
				{
					continue;
				}

				builder.Set(c.x, c.y, c.z, op.Colour);
			}
		}

		/// <summary>
		/// Rasterises a shape into voxel coordinates by replaying it on a throwaway
		/// <see cref="VoxelBuilder"/> — reusing the same tested primitive code for both
		/// stamp (fill) and carve (clear), and for the <see cref="CombineMode.Add"/> check.
		/// </summary>
		private static IReadOnlyList<Vector3Int> RasterShape(ModifierOp op, VoxelScriptLimits limits)
		{
			var scratch = new VoxelBuilder(limits);
			var marker = new Color32(255, 255, 255, 255);
			switch (op.Shape)
			{
				case ShapeKind.Sphere:
					scratch.Sphere(op.Centre.x, op.Centre.y, op.Centre.z, op.Radius, marker);
					break;
				case ShapeKind.Cylinder:
					scratch.Cylinder(op.Centre.x, op.Centre.y, op.Centre.z, op.Radius, op.Height, op.Axis, marker);
					break;
				case ShapeKind.Cone:
					scratch.Cone(op.Centre.x, op.Centre.y, op.Centre.z, op.Radius, op.Height, op.Axis, marker);
					break;
				default:
					scratch.Box(op.Min.x, op.Min.y, op.Min.z, op.Max.x, op.Max.y, op.Max.z, marker);
					break;
			}

			return scratch.Build().Voxels.Keys.ToList();
		}

		private static void BuildEnclosure(VoxelBuilder builder, TerrainSpec spec)
		{
			if (spec.Enclosure == Enclosure.Open)
			{
				return;
			}

			var size = spec.Size;
			var thickness = Mathf.Clamp(spec.WallThickness, 1, Mathf.Max(1, Mathf.Min(size.x, size.y) / 2));
			var top = Mathf.Clamp(spec.WallHeight, 1, size.z) - 1;
			var colour = spec.WallColour;

			builder.Box(0, 0, 0, size.x - 1, thickness - 1, top, colour);                 // -Y wall
			builder.Box(0, size.y - thickness, 0, size.x - 1, size.y - 1, top, colour);    // +Y wall
			builder.Box(0, 0, 0, thickness - 1, size.y - 1, top, colour);                  // -X wall
			builder.Box(size.x - thickness, 0, 0, size.x - 1, size.y - 1, top, colour);    // +X wall

			if (spec.Enclosure == Enclosure.Sealed)
			{
				var ceilingBottom = Mathf.Clamp(top + 1, 0, size.z - 1);
				var ceilingTop = Mathf.Min(size.z - 1, ceilingBottom + thickness - 1);
				builder.Box(0, 0, ceilingBottom, size.x - 1, size.y - 1, ceilingTop, colour);
			}
		}
	}
}
