using Assembler.Voxels.Terrain;
using NUnit.Framework;

namespace Tests.Voxels
{
	public sealed class NoiseTests
	{
		[Test]
		public void Perlin_IsDeterministic_ForSameSeed()
		{
			for (var i = 0; i < 50; i++)
			{
				var x = i * 0.37f;
				var y = i * 0.19f;
				Assert.AreEqual(Noise.Perlin(1234, x, y), Noise.Perlin(1234, x, y), 1e-6f);
			}
		}

		[Test]
		public void Perlin_DiffersAcrossSeeds()
		{
			var differing = 0;
			for (var i = 0; i < 50; i++)
			{
				var x = i * 0.41f;
				var y = i * 0.23f;
				if (Mathf_Abs(Noise.Perlin(1, x, y) - Noise.Perlin(2, x, y)) > 1e-4f)
				{
					differing++;
				}
			}

			Assert.Greater(differing, 0, "Two different seeds should produce a different field somewhere.");
		}

		[Test]
		public void Fbm_IsDeterministic()
		{
			var a = Noise.Fbm(99, 3.5f, 7.25f, 5, 0.02f, 2f, 0.5f);
			var b = Noise.Fbm(99, 3.5f, 7.25f, 5, 0.02f, 2f, 0.5f);
			Assert.AreEqual(a, b, 1e-6f);
		}

		[Test]
		public void HeightField01_StaysInUnitRange([Values(NoiseKind.Fbm, NoiseKind.Ridged, NoiseKind.Billow)] NoiseKind kind)
		{
			for (var x = 0; x < 40; x++)
			{
				for (var y = 0; y < 40; y++)
				{
					var h = Noise.HeightField01(kind, 7, x, y, 5, 0.05f, 2f, 0.5f, 6f);
					Assert.GreaterOrEqual(h, 0f);
					Assert.LessOrEqual(h, 1f);
				}
			}
		}

		private static float Mathf_Abs(float v) => v < 0f ? -v : v;
	}
}
