using System;
using System.Linq;
using Assembler.Voxelization;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxelization
{
	public sealed class LayersCodecTests
	{
		private static readonly PaletteEntry[] Palette =
		{
			new('A', new Color32(255, 0, 0, 255)),
			new('B', new Color32(0, 0, 255, 255)),
		};

		[Test]
		public void Decode_PlacesVoxelsAtOffsetWithPaletteIndices()
		{
			var data = new LayersPartData(
				new Vector3Int(2, 2, 2),
				new Vector3Int(-1, 5, 0),
				new[] { "AB\n..", "..\n.A" });

			var grid = LayersCodec.Decode(data, Palette);

			Assert.That(grid.Voxels.Count, Is.EqualTo(3));
			// Layer 0 (y=5 after offset), row 0 (z=0): 'A' at x=-1, 'B' at x=0.
			Assert.That(grid.Voxels[new Vector3Int(-1, 5, 0)], Is.EqualTo(1));
			Assert.That(grid.Voxels[new Vector3Int(0, 5, 0)], Is.EqualTo(2));
			// Layer 1 (y=6), row 1 (z=1): 'A' at x=0.
			Assert.That(grid.Voxels[new Vector3Int(0, 6, 1)], Is.EqualTo(1));
		}

		[Test]
		public void Decode_RoundTripsThroughEncode()
		{
			var data = new LayersPartData(
				new Vector3Int(3, 2, 2),
				new Vector3Int(0, 0, 0),
				new[] { "A.B\n.A.", "BBB\n..." });

			var grid = LayersCodec.Decode(data, Palette);
			var encoded = LayersCodec.Encode(grid, Palette, data.Offset, data.Size);
			var redecoded = LayersCodec.Decode(encoded, Palette);

			Assert.That(redecoded.Voxels, Is.EquivalentTo(grid.Voxels));
		}

		[Test]
		public void Decode_RejectsWrongLayerCount()
		{
			var data = new LayersPartData(new Vector3Int(1, 2, 1), Vector3Int.zero, new[] { "A" });
			Assert.That(() => LayersCodec.Decode(data, Palette),
				Throws.TypeOf<FormatException>().With.Message.Contains("layers"));
		}

		[Test]
		public void Decode_RejectsWrongRowWidth()
		{
			var data = new LayersPartData(new Vector3Int(2, 1, 1), Vector3Int.zero, new[] { "ABA" });
			Assert.That(() => LayersCodec.Decode(data, Palette),
				Throws.TypeOf<FormatException>().With.Message.Contains("size.x"));
		}

		[Test]
		public void Decode_RejectsUnknownPaletteKey()
		{
			var data = new LayersPartData(new Vector3Int(1, 1, 1), Vector3Int.zero, new[] { "Z" });
			Assert.That(() => LayersCodec.Decode(data, Palette),
				Throws.TypeOf<FormatException>().With.Message.Contains("palette"));
		}

		[Test]
		public void Decode_TreatsUnderscoreAndDotAsEmpty()
		{
			var data = new LayersPartData(new Vector3Int(2, 1, 1), Vector3Int.zero, new[] { "._" });
			var grid = LayersCodec.Decode(data, Palette);
			Assert.That(grid.Voxels, Is.Empty);
		}
	}

	public sealed class VoxelGridConvertTests
	{
		[Test]
		public void SwapYZ_IsInvolutive()
		{
			var palette = new[] { new PaletteEntry('A', new Color32(1, 2, 3, 255)) };
			var data = new LayersPartData(new Vector3Int(2, 3, 1), new Vector3Int(0, 0, 0), new[] { "A.", ".A", "AA" });
			var grid = LayersCodec.Decode(data, palette);

			var swapped = VoxelGridConvert.SwapYZ(grid);
			var back = VoxelGridConvert.SwapYZ(swapped);

			Assert.That(back.Voxels, Is.EquivalentTo(grid.Voxels));
			// A voxel at y=2 (Y-up height) lands at z=2 in Z-up storage.
			Assert.That(grid.Voxels.ContainsKey(new Vector3Int(0, 2, 0)), Is.True);
			Assert.That(swapped.Voxels.ContainsKey(new Vector3Int(0, 0, 2)), Is.True);
		}
	}
}
