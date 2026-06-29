using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Picks one representative colour for a group of voxel samples, preserving small perceptually
	/// distinct details that a raw majority vote would mush away. Samples are pooled into coarse
	/// (5-bit/channel) perceptual clusters; each cluster's vote is its total weight multiplied by
	/// <c>1 + salience · OklabDistance(clusterMean, groupMean)</c>, so a perceptually distinct
	/// minority (an eye, a stripe) can outvote a larger bland majority and survive aggregation.
	/// With <c>salience == 0</c> this reduces to a pure (weight-)majority vote.
	///
	/// Shared by the integer-block <see cref="VoxDownres"/> (unit-weight samples) and the
	/// area-weighted <see cref="VoxResample"/> (fractional-overlap weights) so both collapse colour
	/// identically — only the per-sample weighting differs. Unit weights reproduce the original
	/// count-based vote exactly (a weight of 1 sums to the sample count).
	/// </summary>
	internal static class VoxColourVote
	{
		/// <summary>Unit-weight vote — every colour contributes equally (the integer-block downres path).</summary>
		public static Color32 Choose(IReadOnlyList<Color32> colours, float salience)
		{
			var bins = new Dictionary<int, Accumulator>();
			var group = default(Accumulator);
			foreach (Color32 c in colours)
			{
				Bin(bins, ref group, c, 1.0);
			}
			return Resolve(bins, group, salience);
		}

		/// <summary>Weighted vote — each colour contributes proportionally to <c>weight</c> (the resample path).</summary>
		public static Color32 Choose(IReadOnlyList<(Color32 colour, float weight)> samples, float salience)
		{
			var bins = new Dictionary<int, Accumulator>();
			var group = default(Accumulator);
			foreach ((Color32 colour, float weight) in samples)
			{
				if (weight <= 0f)
				{
					continue;
				}
				Bin(bins, ref group, colour, weight);
			}
			return Resolve(bins, group, salience);
		}

		// Pool a sample into its coarse colour cluster and into the running group mean. The 5-bit/channel
		// key matches the converter's surface-colour binning so near-identical samples pool into one vote.
		private static void Bin(
			Dictionary<int, Accumulator> bins, ref Accumulator group, Color32 c, double weight)
		{
			int key = ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);
			bins.TryGetValue(key, out Accumulator acc);
			bins[key] = acc.Add(c, weight);
			group = group.Add(c, weight);
		}

		private static Color32 Resolve(
			Dictionary<int, Accumulator> bins, Accumulator group, float salience)
		{
			if (group.Weight <= 0)
			{
				return new Color32(0, 0, 0, 255);
			}

			OklabColor groupMeanLab = OklabColor.FromColor32(group.Mean());

			Accumulator best = default;
			float bestWeight = -1f;
			foreach (Accumulator acc in bins.Values)
			{
				float weight = salience <= 0f
					? (float)acc.Weight
					: (float)acc.Weight * (1f + salience * OklabColor.FromColor32(acc.Mean()).DistanceTo(groupMeanLab));
				if (weight > bestWeight)
				{
					bestWeight = weight;
					best = acc;
				}
			}

			return best.Mean();
		}

		// Weighted running sum of a colour cluster. Double accumulators so fractional overlap weights
		// sum cleanly; the truncating cast in Mean reproduces the original integer-average behaviour
		// for unit weights.
		private readonly struct Accumulator
		{
			public readonly double R;
			public readonly double G;
			public readonly double B;
			public readonly double Weight;

			private Accumulator(double r, double g, double b, double weight)
			{
				R = r;
				G = g;
				B = b;
				Weight = weight;
			}

			public Accumulator Add(Color32 c, double weight) =>
				new(R + c.r * weight, G + c.g * weight, B + c.b * weight, Weight + weight);

			public Color32 Mean() =>
				new((byte)(R / Weight), (byte)(G / Weight), (byte)(B / Weight), 255);
		}
	}
}
