using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Tests
{
	public sealed class VoxResampleTests
	{
		private static readonly Color32 Red = new Color32(255, 0, 0, 255);
		private static readonly Color32 Blue = new Color32(0, 0, 255, 255);

		[Test]
		public void ToTargetMaxDim_TargetAtOrAboveMaster_ReturnsMasterUnchanged()
		{
			VoxModel master = SolidCube(4, Red);

			// Never upsample a voxel master — no sub-pitch detail exists to invent.
			VoxModel result = VoxResample.ToTargetMaxDim(master, 8, VoxResample.Options.Default);

			Assert.AreSame(master, result);
		}

		[Test]
		public void ToTargetMaxDim_IntegerRatio_DownresToTargetDims()
		{
			VoxModel master = SolidCube(8, Red);

			// 8 → 4 is an exact ÷2, so this defers to the proven block downres.
			VoxModel result = VoxResample.ToTargetMaxDim(master, 4, VoxResample.Options.Default);

			Assert.AreEqual(4, result.X);
			Assert.AreEqual(4, result.Y);
			Assert.AreEqual(4, result.Z);
			Assert.AreEqual(4 * 4 * 4, CountOccupied(result), "a solid cube stays solid through downres");
		}

		[Test]
		public void ToTargetMaxDim_NonIntegerRatio_PreservesStripesWithoutBlending()
		{
			// Solid 12-wide cube banded red/blue every 3 voxels along X. Downsample 12 → 8 (ratio 1.5).
			VoxModel master = StripedCube(12, 4, 4, bandWidth: 3);

			VoxModel result = VoxResample.ToTargetMaxDim(master, 8, VoxResample.Options.Default);

			Assert.AreEqual(8, result.X);
			Assert.AreEqual(CountOccupied(result), result.X * result.Y * result.Z, "solid cube stays solid");

			var distinct = new HashSet<int>();
			for (int i = 0; i < result.Occupied.Length; i++)
			{
				if (!result.Occupied[i])
				{
					continue;
				}
				Color32 c = result.Colors[i];
				distinct.Add((c.r << 16) | (c.g << 8) | c.b);

				// Box/area filter, not trilinear: every voxel must stay a pure stripe colour — never a
				// blended purple. (Palette-snap mush is exactly what we're avoiding.)
				bool pureRed = c.r >= 200 && c.g <= 55 && c.b <= 55;
				bool pureBlue = c.b >= 200 && c.g <= 55 && c.r <= 55;
				Assert.IsTrue(pureRed || pureBlue, $"voxel colour {c} is neither pure red nor pure blue (blended)");
			}
			Assert.GreaterOrEqual(distinct.Count, 2, "the stripes must survive as ≥2 distinct colours");
		}

		[Test]
		public void ToTargetMaxDim_NonIntegerRatio_FeatureAwareKeepsThinSheet()
		{
			// A 1-voxel-thick sheet inside a 15³ grid, downsampled 15 → 6 (ratio 2.5). Its occupancy
			// coverage (≤ 1/2.5 = 0.4) is below the 0.5 threshold, so only the feature-aware override saves it.
			VoxModel master = ThinSheet(15, sheetY: 7, Red);

			VoxModel kept = VoxResample.ToTargetMaxDim(master, 6, OptionsWith(featureAware: true));
			VoxModel dropped = VoxResample.ToTargetMaxDim(master, 6, OptionsWith(featureAware: false));

			Assert.Greater(CountOccupied(kept), 0, "feature-aware should preserve the sub-Nyquist sheet");
			Assert.AreEqual(0, CountOccupied(dropped), "without feature-aware the thin sheet falls below coverage");
		}

		private static VoxResample.Options OptionsWith(bool featureAware) =>
			new VoxResample.Options(0.5f, featureAware, 1.0f);

		private static VoxModel SolidCube(int dim, Color32 colour)
		{
			var m = new VoxModel(dim, dim, dim);
			for (int i = 0; i < m.Occupied.Length; i++)
			{
				m.Occupied[i] = true;
				m.Colors[i] = colour;
			}
			return m;
		}

		// Solid box, coloured in alternating red/blue bands of bandWidth voxels along X.
		private static VoxModel StripedCube(int x, int y, int z, int bandWidth)
		{
			var m = new VoxModel(x, y, z);
			for (int gz = 0; gz < z; gz++)
			{
				for (int gy = 0; gy < y; gy++)
				{
					for (int gx = 0; gx < x; gx++)
					{
						int i = m.Index(gx, gy, gz);
						m.Occupied[i] = true;
						m.Colors[i] = (gx / bandWidth) % 2 == 0 ? Red : Blue;
					}
				}
			}
			return m;
		}

		// A single occupied horizontal plane (thickness 1) at y = sheetY inside an otherwise empty dim³ grid.
		private static VoxModel ThinSheet(int dim, int sheetY, Color32 colour)
		{
			var m = new VoxModel(dim, dim, dim);
			for (int gz = 0; gz < dim; gz++)
			{
				for (int gx = 0; gx < dim; gx++)
				{
					int i = m.Index(gx, sheetY, gz);
					m.Occupied[i] = true;
					m.Colors[i] = colour;
				}
			}
			return m;
		}

		private static int CountOccupied(VoxModel m)
		{
			int n = 0;
			foreach (bool o in m.Occupied)
			{
				if (o)
				{
					n++;
				}
			}
			return n;
		}
	}
}
