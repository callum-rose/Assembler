using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Estimates the <b>baked voxel pitch</b> of a voxel-style mesh — the world-space edge length of
	/// the regular lattice a tool like Meshy bakes into a voxel-art model. A confident estimate lets
	/// the converter voxelize a <i>lossless master</i> at that native pitch (1:1 with the baked grid)
	/// and then resample to the target resolution in voxel space, instead of resampling a quantized
	/// mesh at an unaligned ratio (which produces moiré/mud at non-power-of-2 targets).
	///
	/// The estimate is deliberately conservative: a smooth, non-grid mesh (no baked lattice) must
	/// report <b>low confidence</b> so the caller falls back to the existing direct/supersample path.
	///
	/// <para><b>Method.</b> Voxel meshes put every vertex on a regular axis-aligned lattice, so per
	/// axis the gaps between consecutive distinct vertex coordinates cluster hard at <c>p, 2p, 3p…</c>.
	/// For each axis we (1) take the dominant smallest gap as the candidate pitch, then (2) score how
	/// well <i>all</i> vertices fit a lattice of that pitch via the circular order parameter
	/// <c>R = |mean exp(i·2π·coord/p)|</c> — 1 when every coordinate lands on the lattice, ~0 for a
	/// continuous (smooth-mesh) spread. The axes' pitches must also agree (a uniform cubic lattice).
	/// Confidence is the worst axis fit scaled by that cross-axis agreement.</para>
	/// </summary>
	public static class NativePitch
	{
		/// <summary>Result of a pitch estimate: whether a lattice was found, its pitch, and the confidence.</summary>
		public readonly struct PitchEstimate
		{
			/// <summary>True when <see cref="Confidence"/> cleared the detection threshold and <see cref="Pitch"/> is usable.</summary>
			public bool Detected { get; init; }

			/// <summary>The estimated lattice pitch in world units (0 when not detected).</summary>
			public float Pitch { get; init; }

			/// <summary>How well the vertices fit a uniform lattice of <see cref="Pitch"/>, in [0, 1].</summary>
			public float Confidence { get; init; }

			public static PitchEstimate None => new() { Detected = false, Pitch = 0f, Confidence = 0f };

			public override string ToString() =>
				Detected ? $"pitch {Pitch:G4} (confidence {Confidence:P0})" : $"no lattice (confidence {Confidence:P0})";
		}

		// An axis needs at least this many coordinate samples, resolving to at least this many distinct
		// lattice planes, to evaluate a fit; below it the axis carries no usable periodicity signal.
		private const int MinCoords = 4;
		private const int MinPlanes = 3;

		// The intra-plane vs inter-plane gap distribution is bimodal: vertices on one baked plane scatter
		// by jitter (tiny gaps), planes are a pitch apart (large gaps). A ratio jump at least this big
		// between consecutive sorted gaps marks the valley between the two modes.
		private const double BimodalRatio = 3.0;

		/// <summary>
		/// Estimates the native pitch of <paramref name="model"/> from its mesh vertices.
		/// <paramref name="confidenceThreshold"/> gates <see cref="PitchEstimate.Detected"/>.
		/// </summary>
		public static PitchEstimate Detect(ObjToVoxConverter.LoadedModel model, float confidenceThreshold = 0.8f)
		{
			g3.DMesh3 mesh = model.Mesh;
			var xs = new List<double>(mesh.VertexCount);
			var ys = new List<double>(mesh.VertexCount);
			var zs = new List<double>(mesh.VertexCount);
			foreach (int vid in mesh.VertexIndices())
			{
				g3.Vector3d v = mesh.GetVertex(vid);
				xs.Add(v.x);
				ys.Add(v.y);
				zs.Add(v.z);
			}
			return Estimate(xs, ys, zs, confidenceThreshold);
		}

		/// <summary>
		/// Pure-data core (no Unity/g3 dependency): estimate the lattice pitch from per-axis vertex
		/// coordinate samples. Exposed for testing with synthetic lattices.
		/// </summary>
		public static PitchEstimate Estimate(
			IReadOnlyList<double> xs, IReadOnlyList<double> ys, IReadOnlyList<double> zs,
			float confidenceThreshold = 0.8f)
		{
			AxisFit fx = FitAxis(xs);
			AxisFit fy = FitAxis(ys);
			AxisFit fz = FitAxis(zs);

			AxisFit[] evaluable = new[] { fx, fy, fz }.Where(f => f.Evaluable).ToArray();
			if (evaluable.Length < 2)
			{
				// Too little structure to trust a uniform-lattice claim (a 1D periodicity isn't enough).
				return PitchEstimate.None;
			}

			double[] pitches = evaluable.Select(f => f.Pitch).OrderBy(p => p).ToArray();
			double medianPitch = Median(pitches);
			double worstFit = evaluable.Min(f => f.Fit);

			// Cross-axis agreement: a uniform cubic lattice has equal pitch on every axis. Penalise the
			// confidence by how far the per-axis pitches spread from their median.
			double spread = medianPitch > 0 ? (pitches[^1] - pitches[0]) / medianPitch : 1.0;
			double agreement = Math.Max(0.0, 1.0 - spread);
			double confidence = worstFit * agreement;

			return new PitchEstimate
			{
				Detected = confidence >= confidenceThreshold && medianPitch > 0,
				Pitch = (float)medianPitch,
				Confidence = (float)confidence,
			};
		}

		private readonly struct AxisFit
		{
			public bool Evaluable { get; init; }
			public double Pitch { get; init; }
			public double Fit { get; init; }
		}

		// Estimate one axis's pitch and how well its coordinates fit that lattice. Coordinates are first
		// grouped into lattice planes (collapsing per-vertex jitter); the pitch is the median spacing
		// between plane centroids, and the fit is the circular order parameter of those centroids.
		// Returns Evaluable=false when the axis resolves to too few planes to carry a signal.
		private static AxisFit FitAxis(IReadOnlyList<double> coords)
		{
			if (coords.Count < MinCoords)
			{
				return default;
			}

			double[] sorted = coords.OrderBy(c => c).ToArray();
			double extent = sorted[^1] - sorted[0];
			if (extent <= 0)
			{
				return default;
			}

			// Gaps above a tiny floor (drop exact-duplicate coordinates), used both to find the
			// intra/inter-plane split and to cluster coordinates into planes.
			double tinyFloor = extent * 1e-6;
			var gaps = new List<double>();
			for (int i = 1; i < sorted.Length; i++)
			{
				double g = sorted[i] - sorted[i - 1];
				if (g > tinyFloor)
				{
					gaps.Add(g);
				}
			}
			if (gaps.Count == 0)
			{
				return default;
			}

			// Clamp the clustering threshold up to the tiny floor so exact-duplicate / ULP-apart
			// coordinates always merge into one plane even when the gaps aren't bimodal.
			double splitThreshold = Math.Max(PlaneSplitThreshold(gaps), tinyFloor);
			double[] centroids = ClusterCentroids(sorted, splitThreshold);
			if (centroids.Length < MinPlanes)
			{
				return default;
			}

			var spacings = new double[centroids.Length - 1];
			for (int i = 0; i < spacings.Length; i++)
			{
				spacings[i] = centroids[i + 1] - centroids[i];
			}
			double pitch = Median(spacings.OrderBy(s => s).ToArray());
			if (pitch <= 0)
			{
				return default;
			}

			// Fit on plane centroids (clean), not raw coordinates — the order parameter is sensitive to
			// pitch error accumulated over many periods, so jitter-averaged centroids matter.
			return new AxisFit { Evaluable = true, Pitch = pitch, Fit = LatticeFit(centroids, pitch) };
		}

		// The gap value separating intra-plane jitter from inter-plane spacing: the geometric mean of the
		// two gaps straddling the biggest ratio jump in the sorted gap distribution. Returns 0 (no
		// clustering — every distinct coordinate is its own plane) when the gaps aren't clearly bimodal,
		// as for an exact lattice whose vertices are perfectly coplanar.
		private static double PlaneSplitThreshold(List<double> gaps)
		{
			double[] asc = gaps.OrderBy(g => g).ToArray();
			double bestRatio = 1.0;
			int split = -1;
			for (int i = 0; i < asc.Length - 1; i++)
			{
				double ratio = asc[i + 1] / asc[i];
				if (ratio > bestRatio)
				{
					bestRatio = ratio;
					split = i;
				}
			}
			return bestRatio >= BimodalRatio && split >= 0 ? Math.Sqrt(asc[split] * asc[split + 1]) : 0.0;
		}

		// Group sorted coordinates into planes — a gap larger than the threshold starts a new plane — and
		// return each plane's centroid (mean), so per-vertex jitter within a plane averages out.
		private static double[] ClusterCentroids(double[] sorted, double threshold)
		{
			var centroids = new List<double>();
			double sum = sorted[0];
			int count = 1;
			for (int i = 1; i < sorted.Length; i++)
			{
				if (sorted[i] - sorted[i - 1] > threshold)
				{
					centroids.Add(sum / count);
					sum = 0;
					count = 0;
				}
				sum += sorted[i];
				count++;
			}
			centroids.Add(sum / count);
			return centroids.ToArray();
		}

		// Circular order parameter R = |mean of exp(i·2π·coord/pitch)| over all coordinate samples.
		// R → 1 when every coordinate sits on the lattice (same phase), → 0 for a continuous spread.
		private static double LatticeFit(IReadOnlyList<double> coords, double pitch)
		{
			double sumCos = 0, sumSin = 0;
			double k = 2.0 * Math.PI / pitch;
			foreach (double c in coords)
			{
				sumCos += Math.Cos(k * c);
				sumSin += Math.Sin(k * c);
			}
			int n = coords.Count;
			return Math.Sqrt(sumCos * sumCos + sumSin * sumSin) / n;
		}

		private static double Median(double[] ascending) =>
			ascending.Length % 2 == 1
				? ascending[ascending.Length / 2]
				: 0.5 * (ascending[ascending.Length / 2 - 1] + ascending[ascending.Length / 2]);
	}
}
