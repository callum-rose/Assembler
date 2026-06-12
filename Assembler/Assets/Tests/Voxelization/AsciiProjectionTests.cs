using System.Collections.Generic;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class AsciiProjectionTests
	{
		private static readonly PaletteEntry[] Palette =
		{
			new('A', new Color32(255, 0, 0, 255)),
			new('B', new Color32(0, 0, 255, 255)),
		};

		[Test]
		public void Ascii_RendersKeyedViews_TopRowFirst()
		{
			// A at the origin, B one step right and up.
			var model = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(1, 1, 0)] = 2,
			}, Palette);

			Assert.That(VoxelProjector.Ascii(model, Palette, ProjectionFace.Front), Is.EqualTo(".B\nA."));
			Assert.That(VoxelProjector.Ascii(model, Palette, ProjectionFace.Side), Is.EqualTo("B\nA"));
			Assert.That(VoxelProjector.Ascii(model, Palette, ProjectionFace.Top), Is.EqualTo("AB"));
		}

		[Test]
		public void Ascii_FrontShowsTheNearestVoxel()
		{
			// Two voxels share an (x, y) column; the one closer to the viewer
			// (greater z) must win the front projection.
			var model = LayersCodec.ToModel(new Dictionary<Vector3Int, byte>
			{
				[new Vector3Int(0, 0, 0)] = 1,
				[new Vector3Int(0, 0, 1)] = 2,
			}, Palette);

			Assert.That(VoxelProjector.Ascii(model, Palette, ProjectionFace.Front), Is.EqualTo("B"));
		}

		[Test]
		public void Ascii_EmptyModelSaysSo() =>
			Assert.That(
				VoxelProjector.Ascii(LayersCodec.ToModel(new Dictionary<Vector3Int, byte>(), Palette), Palette, ProjectionFace.Front),
				Is.EqualTo("(empty)"));
	}
}
