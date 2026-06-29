using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Assembler.AssetGeneration.MeshToVoxels.Tests
{
	public sealed class NativePitchTests
	{
		[Test]
		public void Estimate_PerfectLattice_DetectsPitch()
		{
			BuildLattice(planes: 9, pitch: 2.0, origin: 0.0, jitter: 0.0, rng: null,
				out List<double> xs, out List<double> ys, out List<double> zs);

			NativePitch.PitchEstimate e = NativePitch.Estimate(xs, ys, zs);

			Assert.IsTrue(e.Detected, $"expected detection, got {e}");
			Assert.AreEqual(2.0f, e.Pitch, 0.05f);
			Assert.Greater(e.Confidence, 0.95f);
		}

		[Test]
		public void Estimate_OffsetOrigin_IsPhaseInvariant()
		{
			// Lattice not anchored at 0 — the order-parameter fit is phase-invariant, so pitch still resolves.
			BuildLattice(planes: 8, pitch: 1.5, origin: 1.37, jitter: 0.0, rng: null,
				out List<double> xs, out List<double> ys, out List<double> zs);

			NativePitch.PitchEstimate e = NativePitch.Estimate(xs, ys, zs);

			Assert.IsTrue(e.Detected);
			Assert.AreEqual(1.5f, e.Pitch, 0.05f);
		}

		[Test]
		public void Estimate_JitteredLattice_StillDetects()
		{
			var rng = new Random(12345);
			BuildLattice(planes: 9, pitch: 2.0, origin: 0.0, jitter: 0.04, rng: rng,
				out List<double> xs, out List<double> ys, out List<double> zs);

			NativePitch.PitchEstimate e = NativePitch.Estimate(xs, ys, zs);

			Assert.IsTrue(e.Detected, $"expected detection under jitter, got {e}");
			Assert.AreEqual(2.0f, e.Pitch, 0.2f);
		}

		[Test]
		public void Estimate_SmoothMesh_ReportsLowConfidence()
		{
			// Continuous (non-grid) coordinates — what a smooth/organic mesh looks like. Must NOT detect,
			// so the caller falls back to the direct/supersample path.
			var rng = new Random(999);
			var xs = new List<double>();
			var ys = new List<double>();
			var zs = new List<double>();
			for (int i = 0; i < 729; i++)
			{
				xs.Add(rng.NextDouble() * 16.0);
				ys.Add(rng.NextDouble() * 16.0);
				zs.Add(rng.NextDouble() * 16.0);
			}

			NativePitch.PitchEstimate e = NativePitch.Estimate(xs, ys, zs);

			Assert.IsFalse(e.Detected, $"smooth mesh should not detect a lattice, got {e}");
			Assert.Less(e.Confidence, 0.8f);
		}

		[Test]
		public void Estimate_TooFewAxes_NotDetected()
		{
			// A near-degenerate point set (one usable axis) can't support a uniform-lattice claim.
			var xs = new List<double> { 0, 2, 4, 6, 8 };
			var ys = new List<double> { 0, 0, 0, 0, 0 };
			var zs = new List<double> { 0, 0, 0, 0, 0 };

			NativePitch.PitchEstimate e = NativePitch.Estimate(xs, ys, zs);

			Assert.IsFalse(e.Detected);
		}

		// A full grid of corner vertices (planes³), optionally jittered. Each axis's coordinate list
		// therefore clusters hard on the lattice lines — the signal pitch detection keys on.
		private static void BuildLattice(
			int planes, double pitch, double origin, double jitter, Random? rng,
			out List<double> xs, out List<double> ys, out List<double> zs)
		{
			xs = new List<double>(planes * planes * planes);
			ys = new List<double>(planes * planes * planes);
			zs = new List<double>(planes * planes * planes);
			for (int i = 0; i < planes; i++)
			{
				for (int j = 0; j < planes; j++)
				{
					for (int k = 0; k < planes; k++)
					{
						xs.Add(origin + i * pitch + Jit(jitter, rng));
						ys.Add(origin + j * pitch + Jit(jitter, rng));
						zs.Add(origin + k * pitch + Jit(jitter, rng));
					}
				}
			}
		}

		private static double Jit(double jitter, Random? rng) =>
			jitter > 0 && rng != null ? (rng.NextDouble() * 2.0 - 1.0) * jitter : 0.0;
	}
}
