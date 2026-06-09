using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxels.Generation
{
	/// <summary>
	/// Decodes a PNG/JPG into pixels and quantises it to a small palette via
	/// <see cref="PaletteQuantizer"/>. Decoding creates a <see cref="Texture2D"/>,
	/// so callers MUST invoke this on the Unity main thread (the reference stage
	/// wraps it in <c>ctx.MainThread.RunAsync</c>).
	/// </summary>
	public static class ImagePalette
	{
		/// <summary>
		/// Best-effort palette extraction. Returns an empty array (never throws)
		/// when the bytes can't be decoded, so reference generation degrades
		/// gracefully when the provider hands back something unexpected.
		/// </summary>
		public static Color32[] Extract(byte[] imageBytes, int maxColors)
		{
			if (imageBytes == null || imageBytes.Length == 0 || maxColors <= 0)
			{
				return Array.Empty<Color32>();
			}

			Texture2D? texture = null;
			try
			{
				texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
				if (!texture.LoadImage(imageBytes))
				{
					return Array.Empty<Color32>();
				}

				var pixels = SamplePixels(texture, maxSamples: 4096);
				return PaletteQuantizer.Quantize(pixels, maxColors);
			}
			catch (Exception)
			{
				return Array.Empty<Color32>();
			}
			finally
			{
				if (texture != null)
				{
					UnityEngine.Object.DestroyImmediate(texture);
				}
			}
		}

		// Stride-sample the image so large references don't blow up k-means.
		private static IReadOnlyList<Color32> SamplePixels(Texture2D texture, int maxSamples)
		{
			var all = texture.GetPixels32();
			if (all.Length <= maxSamples)
			{
				return all;
			}

			var stride = Mathf.Max(1, all.Length / maxSamples);
			var sampled = new List<Color32>(maxSamples + 1);
			for (var i = 0; i < all.Length; i += stride)
			{
				sampled.Add(all[i]);
			}

			return sampled;
		}
	}
}
