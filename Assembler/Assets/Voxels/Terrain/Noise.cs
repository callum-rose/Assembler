using UnityEngine;

namespace Assembler.Voxels.Terrain
{
	/// <summary>
	/// Deterministic, integer-seeded gradient noise plus the fbm / ridged / billow
	/// combiners and an optional domain warp. Pure C# over <see cref="Mathf"/> only
	/// (no <c>UnityEngine.Mathf.PerlinNoise</c>, which is seedless and 2D-only), so
	/// the same seed reproduces the same field everywhere — and a future on-device
	/// terrain path can reuse it unchanged under AOT.
	/// </summary>
	public static class Noise
	{
		/// <summary>
		/// 2D gradient (Perlin-style) noise in roughly <c>[-1, 1]</c>, deterministic
		/// from <paramref name="seed"/> and the lattice it hashes.
		/// </summary>
		public static float Perlin(int seed, float x, float y)
		{
			var x0 = Mathf.FloorToInt(x);
			var y0 = Mathf.FloorToInt(y);
			var fx = x - x0;
			var fy = y - y0;
			var u = Fade(fx);
			var v = Fade(fy);

			var n00 = GradDot(x0, y0, seed, fx, fy);
			var n10 = GradDot(x0 + 1, y0, seed, fx - 1f, fy);
			var n01 = GradDot(x0, y0 + 1, seed, fx, fy - 1f);
			var n11 = GradDot(x0 + 1, y0 + 1, seed, fx - 1f, fy - 1f);

			var nx0 = Mathf.Lerp(n00, n10, u);
			var nx1 = Mathf.Lerp(n01, n11, u);
			// gradient noise peaks near +-0.707; scale toward [-1, 1] and clamp.
			return Mathf.Clamp(Mathf.Lerp(nx0, nx1, v) * 1.41421356f, -1f, 1f);
		}

		/// <summary>Fractal Brownian motion: octave-summed Perlin, amplitude-normalised to <c>[-1, 1]</c>.</summary>
		public static float Fbm(int seed, float x, float y, int octaves, float frequency, float lacunarity, float gain)
		{
			octaves = Mathf.Max(1, octaves);
			float sum = 0f, amplitude = 1f, freq = frequency, norm = 0f;
			for (var o = 0; o < octaves; o++)
			{
				sum += amplitude * Perlin(seed + o * 1013, x * freq, y * freq);
				norm += amplitude;
				amplitude *= gain;
				freq *= lacunarity;
			}

			return norm > 0f ? sum / norm : 0f;
		}

		/// <summary>Ridged multifractal: sharp ridges from <c>(1 - |perlin|)^2</c>, normalised to <c>[0, 1]</c>.</summary>
		public static float Ridged(int seed, float x, float y, int octaves, float frequency, float lacunarity, float gain)
		{
			octaves = Mathf.Max(1, octaves);
			float sum = 0f, amplitude = 1f, freq = frequency, norm = 0f;
			for (var o = 0; o < octaves; o++)
			{
				var n = 1f - Mathf.Abs(Perlin(seed + o * 1013, x * freq, y * freq));
				n *= n;
				sum += amplitude * n;
				norm += amplitude;
				amplitude *= gain;
				freq *= lacunarity;
			}

			return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
		}

		/// <summary>Billow noise: rounded mounds from <c>|perlin|</c>, normalised to <c>[0, 1]</c>.</summary>
		public static float Billow(int seed, float x, float y, int octaves, float frequency, float lacunarity, float gain)
		{
			octaves = Mathf.Max(1, octaves);
			float sum = 0f, amplitude = 1f, freq = frequency, norm = 0f;
			for (var o = 0; o < octaves; o++)
			{
				sum += amplitude * Mathf.Abs(Perlin(seed + o * 1013, x * freq, y * freq));
				norm += amplitude;
				amplitude *= gain;
				freq *= lacunarity;
			}

			return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
		}

		/// <summary>
		/// Samples the chosen combiner as a height factor in <c>[0, 1]</c>, applying an
		/// optional domain warp first (offsetting the sample point by up to
		/// <paramref name="warp"/> voxels for a more organic, less grid-aligned field).
		/// </summary>
		public static float HeightField01(
			NoiseKind kind, int seed, float x, float y,
			int octaves, float frequency, float lacunarity, float gain, float warp)
		{
			if (warp > 0f)
			{
				x += Perlin(seed + 7919, x * frequency, y * frequency) * warp;
				y += Perlin(seed + 104729, x * frequency, y * frequency) * warp;
			}

			return kind switch
			{
				NoiseKind.Ridged => Ridged(seed, x, y, octaves, frequency, lacunarity, gain),
				NoiseKind.Billow => Billow(seed, x, y, octaves, frequency, lacunarity, gain),
				_ => Mathf.Clamp01(Fbm(seed, x, y, octaves, frequency, lacunarity, gain) * 0.5f + 0.5f),
			};
		}

		private static float GradDot(int ix, int iy, int seed, float dx, float dy)
		{
			var angle = Hash(ix, iy, seed) / 4294967296f * (Mathf.PI * 2f);
			return Mathf.Cos(angle) * dx + Mathf.Sin(angle) * dy;
		}

		private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

		private static uint Hash(int x, int y, int seed)
		{
			unchecked
			{
				var h = 2166136261u;
				h = (h ^ (uint)x) * 16777619u;
				h = (h ^ (uint)y) * 16777619u;
				h = (h ^ (uint)seed) * 16777619u;
				h ^= h >> 13;
				h *= 0x5bd1e995u;
				h ^= h >> 15;
				return h;
			}
		}
	}
}
