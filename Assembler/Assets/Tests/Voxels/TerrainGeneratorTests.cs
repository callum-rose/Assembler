using System.Collections.Generic;
using Assembler.Voxels;
using Assembler.Voxels.Scripting;
using Assembler.Voxels.Terrain;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxels
{
	public sealed class TerrainGeneratorTests
	{
		private static readonly Color32 Green = new Color32(0x5a, 0x8f, 0x3c, 255);
		private static readonly Color32 Grey = new Color32(0x6b, 0x6b, 0x6b, 255);
		private static readonly Color32 Stone = new Color32(0x8a, 0x8f, 0x95, 255);

		[Test]
		public void FlatBase_ProducesFlatTopAtExpectedHeight()
		{
			var spec = Spec(new Vector3Int(8, 8, 16), FlatBase(5), skin: 2, Enclosure.Open);
			var model = TerrainGenerator.Generate(spec);

			for (var x = 0; x < 8; x++)
			{
				for (var y = 0; y < 8; y++)
				{
					Assert.IsTrue(Has(model, x, y, 5), "top voxel of every column at z=5");
					Assert.IsTrue(Has(model, x, y, 4), "skin thickness 2 also fills z=4");
					Assert.IsFalse(Has(model, x, y, 6), "nothing above the flat top");
					Assert.IsFalse(Has(model, x, y, 3), "skin does not reach z=3");
				}
			}
		}

		[Test]
		public void NoiseBase_StaysWithinBaseHeightPlusAmplitude()
		{
			var noise = new NoiseSettings(NoiseKind.Fbm, 5, 0.08f, 20f, 2f, 0.5f, 0f);
			var spec = Spec(new Vector3Int(16, 16, 64), new BaseOp(BaseKind.Noise, Green, 6, noise), skin: 3, Enclosure.Open);
			var model = TerrainGenerator.Generate(spec);

			for (var x = 0; x < 16; x++)
			{
				for (var y = 0; y < 16; y++)
				{
					var top = TopZ(model, x, y);
					Assert.GreaterOrEqual(top, 6, "surface never below BaseHeight");
					Assert.LessOrEqual(top, 26, "surface never above BaseHeight + Amplitude");
				}
			}
		}

		[Test]
		public void Model_StaysUnderCapAndWithinAxisLimit()
		{
			var spec = Spec(new Vector3Int(200, 200, 64), FlatBase(6), skin: 3, Enclosure.Open);
			var model = TerrainGenerator.Generate(spec);

			Assert.Less(model.Voxels.Count, 1_000_000, "skin keeps the model well under the 1M cap");
			Assert.LessOrEqual(model.Size.x, 256);
			Assert.LessOrEqual(model.Size.y, 256);
			Assert.LessOrEqual(model.Size.z, 256);
		}

		[Test]
		public void Walled_AddsPerimeterThatOpenLacks()
		{
			var size = new Vector3Int(12, 12, 16);
			var open = TerrainGenerator.Generate(Spec(size, FlatBase(2), skin: 1, Enclosure.Open));
			var walled = TerrainGenerator.Generate(WalledSpec(size, FlatBase(2), Enclosure.Walled, wallHeight: 6, wallThickness: 2));

			// A corner column above the flat top: empty when open, wall-filled when walled.
			Assert.IsFalse(Has(open, 0, 0, 5));
			Assert.IsTrue(Has(walled, 0, 0, 5), "wall rises to z=5 (WallHeight 6)");
		}

		[Test]
		public void Sealed_AddsCeilingThatWalledLacks()
		{
			var size = new Vector3Int(12, 12, 16);
			var walled = TerrainGenerator.Generate(WalledSpec(size, FlatBase(2), Enclosure.Walled, wallHeight: 6, wallThickness: 2));
			var sealedModel = TerrainGenerator.Generate(WalledSpec(size, FlatBase(2), Enclosure.Sealed, wallHeight: 6, wallThickness: 2));

			// Interior cell at the ceiling height: open above the walls, capped when sealed.
			Assert.IsFalse(Has(walled, 6, 6, 6));
			Assert.IsTrue(Has(sealedModel, 6, 6, 6), "ceiling caps the interior at z=6");
		}

		[Test]
		public void Carve_RemovesVoxelsInsideTheShape()
		{
			// A thick skin gives a near-solid column, then carve a sphere out of it.
			var carve = new ModifierOp(
				OpKind.Carve, ShapeKind.Sphere, default, CombineMode.Replace,
				Vector3Int.zero, Vector3Int.zero, new Vector3Int(4, 4, 5), 2, 1, VoxelAxis.Z);
			var spec = Spec(new Vector3Int(8, 8, 16), FlatBase(5), skin: 10, Enclosure.Open, carve);
			var model = TerrainGenerator.Generate(spec);

			Assert.IsFalse(Has(model, 4, 4, 5), "sphere centre was carved away");
			Assert.IsTrue(Has(model, 0, 0, 5), "material outside the sphere remains");
		}

		[Test]
		public void Stamp_AddsVoxelsAboveTheBase()
		{
			var stamp = new ModifierOp(
				OpKind.Stamp, ShapeKind.Box, Stone, CombineMode.Replace,
				new Vector3Int(2, 2, 8), new Vector3Int(4, 4, 10), Vector3Int.zero, 1, 1, VoxelAxis.Z);
			var spec = Spec(new Vector3Int(8, 8, 16), FlatBase(2), skin: 1, Enclosure.Open, stamp);
			var model = TerrainGenerator.Generate(spec);

			Assert.IsTrue(Has(model, 3, 3, 9), "stamped box sits above the flat base");
		}

		// ---- helpers ------------------------------------------------------

		private static BaseOp FlatBase(int height) => new BaseOp(BaseKind.Flat, Green, height, null);

		private static TerrainSpec Spec(Vector3Int size, BaseOp baseOp, int skin, Enclosure enclosure, params ModifierOp[] ops)
			=> new TerrainSpec("test", 1337, size, skin, enclosure, 8, 2, Grey, baseOp, new List<ModifierOp>(ops));

		private static TerrainSpec WalledSpec(Vector3Int size, BaseOp baseOp, Enclosure enclosure, int wallHeight, int wallThickness)
			=> new TerrainSpec("test", 1337, size, 1, enclosure, wallHeight, wallThickness, Grey, baseOp, new List<ModifierOp>());

		private static bool Has(VoxelModel model, int x, int y, int z)
			=> model.Voxels.ContainsKey(new Vector3Int(x, y, z));

		private static int TopZ(VoxelModel model, int x, int y)
		{
			// Voxel coordinates are absolute; scan the model's actual Z bounds (not the
			// extent, which starts at Min.z) so tall columns near Max.z aren't missed.
			for (var z = model.Max.z; z >= model.Min.z; z--)
			{
				if (Has(model, x, y, z))
				{
					return z;
				}
			}

			return -1;
		}
	}
}
