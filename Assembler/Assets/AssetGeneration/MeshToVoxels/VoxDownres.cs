using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Supersample-and-downres: collapse a high-resolution working model (voxelized at
	/// <see cref="Options.Factor"/>× the target dimension) down to the target grid, one output
	/// voxel per <c>Factor³</c> block. Unlike the post-processing <see cref="IVoxStep"/>s — which
	/// run in place at the final resolution — this <i>changes the grid dimensions</i>, so it is a
	/// standalone transform run between voxelization and the pipeline, not a pipeline step.
	///
	/// It exists to preserve detail that direct low-res voxelization aliases away. Two levers:
	///
	/// <para><b>Occupancy (coverage + feature-aware).</b> Each output voxel sees a <i>coverage
	/// fraction</i> (occupied sub-voxels ÷ block size) instead of a single centre hit/miss, so the
	/// shell is anti-aliased. A plain <see cref="Options.CoverageThreshold"/> majority vote still
	/// erases features thinner than one output voxel (an antenna, a fin), so
	/// <see cref="Options.FeatureAware"/> force-keeps any block whose local structure is thinner
	/// than the factor — i.e. a sub-Nyquist feature the coverage vote would otherwise drop.</para>
	///
	/// <para><b>Colour (salience-weighted vote).</b> Collapsing a block to one colour by raw
	/// majority lets a small bright detail (an eye, a stripe) get outvoted into mush. Instead each
	/// coarse colour cluster's vote is weighted by its perceptual (Oklab) distance from the block
	/// mean, scaled by <see cref="Options.ColourSalience"/> — so a perceptually distinct minority
	/// can win the voxel and stay visible. Salience 0 reduces to a pure majority vote.</para>
	///
	/// This is the lightweight stand-in for explicit colour/geometry segmentation: it preserves
	/// <i>features</i> without a global segmentation pass. Floater removal (downstream in the
	/// pipeline) still cleans isolated specks the feature-aware keep might let through.
	/// </summary>
	public static class VoxDownres
	{
		/// <summary>Config for one downres pass. <see cref="Default"/> is a sensible A/B starting point.</summary>
		public readonly struct Options
		{
			/// <summary>K: each output voxel aggregates a <c>K³</c> block of the high-res model.</summary>
			public int Factor { get; init; }

			/// <summary>Occupy an output voxel when its block's occupied fraction reaches this (0..1).</summary>
			public float CoverageThreshold { get; init; }

			/// <summary>Also keep blocks whose local structure is thinner than the factor (sub-Nyquist features).</summary>
			public bool FeatureAware { get; init; }

			/// <summary>Boost for perceptually distinct minority colours in the per-voxel vote. 0 = pure majority.</summary>
			public float ColourSalience { get; init; }

			public static Options Default => new()
			{
				Factor = 2,
				CoverageThreshold = 0.5f,
				FeatureAware = true,
				ColourSalience = 1.0f,
			};
		}

		/// <summary>
		/// Downres <paramref name="hi"/> by <see cref="Options.Factor"/>, returning a new model at
		/// <c>ceil(dim / factor)</c> per axis. A factor ≤ 1 is a no-op (returns <paramref name="hi"/>).
		/// </summary>
		public static VoxModel Apply(VoxModel hi, Options options)
		{
			int factor = Mathf.Max(1, options.Factor);
			if (factor == 1)
			{
				return hi;
			}

			int outX = CeilDiv(hi.X, factor);
			int outY = CeilDiv(hi.Y, factor);
			int outZ = CeilDiv(hi.Z, factor);
			var lo = new VoxModel(outX, outY, outZ);

			// Bounded erosion depth (min(thickness, factor)) per high-res voxel, so the colour/occupancy
			// pass can tell a sub-output-voxel-thin feature from a thick surface. Only paid for when on.
			int[]? thickness = options.FeatureAware ? VoxThickness.Map(hi, factor) : null;

			float coverageThreshold = Mathf.Clamp01(options.CoverageThreshold);
			float salience = Mathf.Max(0f, options.ColourSalience);

			for (int oz = 0; oz < outZ; oz++)
			{
				for (int oy = 0; oy < outY; oy++)
				{
					for (int ox = 0; ox < outX; ox++)
					{
						ResolveBlock(
							hi, lo, ox, oy, oz, factor, coverageThreshold, salience, thickness);
					}
				}
			}

			return lo;
		}

		// Aggregate one factor³ high-res block into output voxel (ox,oy,oz).
		private static void ResolveBlock(
			VoxModel hi, VoxModel lo, int ox, int oy, int oz, int factor,
			float coverageThreshold, float salience, int[]? thickness)
		{
			int x0 = ox * factor, y0 = oy * factor, z0 = oz * factor;
			int inBounds = 0;
			int occupied = 0;
			int maxThickness = 0;
			var colours = new List<Color32>();

			for (int dz = 0; dz < factor; dz++)
			{
				int z = z0 + dz;
				if (z >= hi.Z)
				{
					break;
				}
				for (int dy = 0; dy < factor; dy++)
				{
					int y = y0 + dy;
					if (y >= hi.Y)
					{
						break;
					}
					for (int dx = 0; dx < factor; dx++)
					{
						int x = x0 + dx;
						if (x >= hi.X)
						{
							break;
						}

						inBounds++;
						int i = hi.Index(x, y, z);
						if (!hi.Occupied[i])
						{
							continue;
						}
						occupied++;
						colours.Add(hi.Colors[i]);
						if (thickness != null && thickness[i] > maxThickness)
						{
							maxThickness = thickness[i];
						}
					}
				}
			}

			if (occupied == 0)
			{
				return;
			}

			// Coverage vote, with a feature-aware override for structures thinner than one output
			// voxel — those are sub-Nyquist and would otherwise be erased by the coverage majority.
			float coverage = (float)occupied / inBounds;
			bool thinFeature = thickness != null && maxThickness < factor;
			if (coverage < coverageThreshold && !thinFeature)
			{
				return;
			}

			int li = lo.Index(ox, oy, oz);
			lo.Occupied[li] = true;
			lo.Colors[li] = VoxColourVote.Choose(colours, salience);
		}

		private static int CeilDiv(int a, int b) => (a + b - 1) / b;
	}
}
