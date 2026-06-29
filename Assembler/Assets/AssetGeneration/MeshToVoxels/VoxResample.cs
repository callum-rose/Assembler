using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Resamples a lossless native-pitch <i>master</i> (see <see cref="NativePitch"/> /
	/// <see cref="ObjToVoxConverter.ConvertAtVoxelSize"/>) down to an arbitrary target resolution — the
	/// back-end that makes any final voxel size tractable for a voxel-style mesh. Because the master is a
	/// clean discrete signal with known support (one voxel = one baked cell), resampling it is well-behaved
	/// even at a <b>non-integer ratio</b>, unlike resampling the quantized mesh directly (which moirés/muds).
	///
	/// <para>The <b>integer ratio</b> case (master dim an exact multiple of the target) defers to the
	/// proven <see cref="VoxDownres"/> block aggregation. The <b>non-integer</b> case area-weights: each
	/// output voxel covers a fractional <c>r³</c> region of the master, and each overlapped master voxel
	/// contributes by its fractional overlap volume to both the occupancy coverage and the colour vote.</para>
	///
	/// <para>Occupancy is the overlap-weighted occupied fraction against a coverage threshold, with the same
	/// feature-aware sub-Nyquist override as <see cref="VoxDownres"/> (a structure thinner than the ratio is
	/// force-kept). Colour is the shared <see cref="VoxColourVote"/> salience vote, each master voxel weighted
	/// by its overlap so a small bright detail survives a non-integer downsample. A box/area filter is used
	/// deliberately — no bi/trilinear colour blending, which would create off-palette mush (palette-snap runs
	/// downstream anyway).</para>
	/// </summary>
	public static class VoxResample
	{
		/// <summary>Config for a resample pass. Mirrors the occupancy/colour levers of <see cref="VoxDownres.Options"/>.</summary>
		public readonly struct Options
		{
			/// <summary>Occupy an output voxel when its overlap-weighted occupied fraction reaches this (0..1).</summary>
			public float CoverageThreshold { get; }

			/// <summary>Also keep structures thinner than the resample ratio (sub-Nyquist features) the coverage vote would erase.</summary>
			public bool FeatureAware { get; }

			/// <summary>Boost for perceptually distinct minority colours in the per-voxel vote. 0 = pure majority.</summary>
			public float ColourSalience { get; }

			public Options(float coverageThreshold, bool featureAware, float colourSalience)
			{
				CoverageThreshold = Mathf.Clamp01(coverageThreshold);
				FeatureAware = featureAware;
				ColourSalience = Mathf.Max(0f, colourSalience);
			}

			public static Options Default => new Options(0.5f, true, 1.0f);
		}

		/// <summary>
		/// Resamples <paramref name="master"/> so its longest axis becomes <paramref name="targetMaxDim"/>
		/// voxels, preserving aspect. Never <i>up</i>samples a voxel master (no sub-pitch detail exists):
		/// a target ≥ the master's longest dimension returns the master unchanged. An exact integer ratio
		/// defers to <see cref="VoxDownres"/>; otherwise an area-weighted resample runs.
		/// </summary>
		public static VoxModel ToTargetMaxDim(VoxModel master, int targetMaxDim, Options options)
		{
			int masterMaxDim = Mathf.Max(master.X, Mathf.Max(master.Y, master.Z));
			if (targetMaxDim < 1 || targetMaxDim >= masterMaxDim)
			{
				return master;
			}

			// Exact integer ratio → the proven block aggregation, aligned to the master grid origin.
			if (masterMaxDim % targetMaxDim == 0)
			{
				return VoxDownres.Apply(master, new VoxDownres.Options
				{
					Factor = masterMaxDim / targetMaxDim,
					CoverageThreshold = options.CoverageThreshold,
					FeatureAware = options.FeatureAware,
					ColourSalience = options.ColourSalience,
				});
			}

			return AreaWeighted(master, masterMaxDim, targetMaxDim, options);
		}

		private static VoxModel AreaWeighted(VoxModel master, int masterMaxDim, int targetMaxDim, Options options)
		{
			double ratio = (double)masterMaxDim / targetMaxDim;

			// Output dims from the longest-axis ratio (preserves aspect); the per-axis ratio is then pinned
			// so the output grid tiles the master exactly (outDim · rAxis == masterDim) — no dropped or
			// overhanging master voxels at the far edge.
			int outX = Mathf.Clamp(Mathf.RoundToInt((float)(master.X / ratio)), 1, master.X);
			int outY = Mathf.Clamp(Mathf.RoundToInt((float)(master.Y / ratio)), 1, master.Y);
			int outZ = Mathf.Clamp(Mathf.RoundToInt((float)(master.Z / ratio)), 1, master.Z);
			double rx = (double)master.X / outX;
			double ry = (double)master.Y / outY;
			double rz = (double)master.Z / outZ;

			var lo = new VoxModel(outX, outY, outZ);

			// Thickness on the master, capped at the ratio: lets the occupancy pass force-keep a feature
			// thinner than the resample ratio (sub-Nyquist) that the coverage vote would otherwise erase.
			int cap = Mathf.Max(1, Mathf.CeilToInt((float)ratio));
			int[]? thickness = options.FeatureAware ? VoxThickness.Map(master, cap) : null;

			float coverageThreshold = Mathf.Clamp01(options.CoverageThreshold);
			float salience = Mathf.Max(0f, options.ColourSalience);

			for (int oz = 0; oz < outZ; oz++)
			{
				for (int oy = 0; oy < outY; oy++)
				{
					for (int ox = 0; ox < outX; ox++)
					{
						ResolveCell(
							master, lo, ox, oy, oz, rx, ry, rz, ratio,
							coverageThreshold, salience, thickness);
					}
				}
			}

			return lo;
		}

		// Area-weight one output voxel from the master cells its [o·r, (o+1)·r) support overlaps.
		private static void ResolveCell(
			VoxModel master, VoxModel lo, int ox, int oy, int oz,
			double rx, double ry, double rz, double ratio,
			float coverageThreshold, float salience, int[]? thickness)
		{
			(int x0, int x1, double[] wx) = AxisOverlaps(ox, rx, master.X);
			(int y0, int y1, double[] wy) = AxisOverlaps(oy, ry, master.Y);
			(int z0, int z1, double[] wz) = AxisOverlaps(oz, rz, master.Z);

			double inBoundsWeight = 0;
			double occupiedWeight = 0;
			int maxThickness = 0;
			var samples = new List<(Color32 colour, float weight)>();

			for (int z = z0; z < z1; z++)
			{
				double wzv = wz[z - z0];
				for (int y = y0; y < y1; y++)
				{
					double wyv = wy[y - y0];
					for (int x = x0; x < x1; x++)
					{
						double w = wx[x - x0] * wyv * wzv;
						if (w <= 0)
						{
							continue;
						}
						inBoundsWeight += w;

						int i = master.Index(x, y, z);
						if (!master.Occupied[i])
						{
							continue;
						}
						occupiedWeight += w;
						samples.Add((master.Colors[i], (float)w));
						if (thickness != null && thickness[i] > maxThickness)
						{
							maxThickness = thickness[i];
						}
					}
				}
			}

			if (occupiedWeight <= 0 || inBoundsWeight <= 0)
			{
				return;
			}

			// Coverage vote with the feature-aware override for structures thinner than the ratio.
			float coverage = (float)(occupiedWeight / inBoundsWeight);
			bool thinFeature = thickness != null && maxThickness < ratio;
			if (coverage < coverageThreshold && !thinFeature)
			{
				return;
			}

			int li = lo.Index(ox, oy, oz);
			lo.Occupied[li] = true;
			lo.Colors[li] = VoxColourVote.Choose(samples, salience);
		}

		// The master cells [first, last) overlapping output index o's support [o·r, (o+1)·r), and the
		// overlap length each contributes. The support tiles the master exactly, so it stays in bounds.
		private static (int first, int last, double[] weights) AxisOverlaps(int o, double r, int masterDim)
		{
			double lo = o * r;
			double hi = (o + 1) * r;
			int first = Mathf.Clamp((int)System.Math.Floor(lo), 0, masterDim - 1);
			int last = Mathf.Clamp((int)System.Math.Ceiling(hi), first + 1, masterDim);

			var weights = new double[last - first];
			for (int m = first; m < last; m++)
			{
				double overlap = System.Math.Min(hi, m + 1) - System.Math.Max(lo, m);
				weights[m - first] = overlap > 0 ? overlap : 0;
			}
			return (first, last, weights);
		}
	}
}
