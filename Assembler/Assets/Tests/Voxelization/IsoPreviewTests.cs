using System.Collections.Generic;
using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class IsoPreviewTests
	{
		[Test]
		public void RenderIso_ReadsTopDown_NearerVoxelsSitLowerOnScreen()
		{
			// Two voxels along z: A far (z=0), B near (z=1). In a top-down
			// dimetric the nearer voxel must appear LOWER in the image; the old
			// projection put it higher, which read as a view from below.
			var palette = new[]
			{
				new PaletteEntry('A', new Color32(255, 0, 0, 255)),
				new PaletteEntry('B', new Color32(0, 0, 255, 255)),
			};
			var model = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(0, 0, 1)] = 2,
			}, palette);

			var texture = VoxelPreviewRenderer.RenderIso(model);
			var pixels = texture.GetPixels32();

			// The top face of each cube keeps the exact palette colour.
			int LowestRowOf(Color32 colour) => Enumerable.Range(0, pixels.Length)
				.Where(i => pixels[i].a > 0 && pixels[i].r == colour.r && pixels[i].g == colour.g && pixels[i].b == colour.b)
				.Select(i => i / texture.width)
				.Min();

			Assert.That(LowestRowOf(palette[1].Colour), Is.LessThan(LowestRowOf(palette[0].Colour)));
		}
	}
}
