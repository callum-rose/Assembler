using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Voxels;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Voxels
{
	public class VoxelRoundTripTests
	{
		[Test]
		public void Parse_SimpleVoxels_PopulatesModel()
		{
			const string text = "# comment\n0 0 0 ff0000\n1 0 0 00ff00\n1 1 0 ff0000";

			var model = GoxelTextParser.Parse(text);

			Assert.AreEqual(3, model.Voxels.Count);
			Assert.AreEqual(2, model.Palette.Length, "ff0000 and 00ff00 should share two palette entries");
			Assert.AreEqual(new Vector3Int(0, 0, 0), model.Min);
			Assert.AreEqual(new Vector3Int(1, 1, 0), model.Max);
			Assert.AreEqual(model.Voxels[new Vector3Int(0, 0, 0)], model.Voxels[new Vector3Int(1, 1, 0)],
				"same colour should map to same palette index");
		}

		[Test]
		public void Write_ThenRead_RoundTripsVoxelsAndPalette()
		{
			const string text = "2 0 0 ff0000\n3 1 4 00ff00\n2 0 5 0000ff";

			var original = GoxelTextParser.Parse(text);
			var bytes = VoxWriter.Write(original);

			var (size, voxels, palette) = ReadVox(bytes);

			Assert.AreEqual(new Vector3Int(2, 2, 6), size);
			Assert.AreEqual(original.Voxels.Count, voxels.Count);

			foreach (var kv in original.Voxels)
			{
				var translated = kv.Key - original.Min;
				Assert.IsTrue(voxels.TryGetValue(translated, out var idx),
					$"voxel at {translated} missing in read-back");
				var originalColour = original.Palette[kv.Value - 1];
				var readColour = palette[idx - 1];
				Assert.AreEqual(originalColour.r, readColour.r);
				Assert.AreEqual(originalColour.g, readColour.g);
				Assert.AreEqual(originalColour.b, readColour.b);
			}
		}

		private static (Vector3Int size, Dictionary<Vector3Int, byte> voxels, Color32[] palette) ReadVox(byte[] bytes)
		{
			using var ms = new MemoryStream(bytes);
			using var r = new BinaryReader(ms);

			var magic = new string(r.ReadChars(4));
			Assert.AreEqual("VOX ", magic);
			r.ReadInt32(); // version

			ReadChunkHeader(r, out var mainId, out var mainContent, out var mainChildren);
			Assert.AreEqual("MAIN", mainId);
			r.ReadBytes(mainContent);

			Vector3Int size = default;
			var voxels = new Dictionary<Vector3Int, byte>();
			var palette = new Color32[256];

			var end = r.BaseStream.Position + mainChildren;
			while (r.BaseStream.Position < end)
			{
				ReadChunkHeader(r, out var id, out var content, out var children);
				var contentBytes = r.ReadBytes(content);
				r.ReadBytes(children);

				switch (id)
				{
					case "SIZE":
						size = new Vector3Int(
							System.BitConverter.ToInt32(contentBytes, 0),
							System.BitConverter.ToInt32(contentBytes, 4),
							System.BitConverter.ToInt32(contentBytes, 8));
						break;
					case "XYZI":
						var count = System.BitConverter.ToInt32(contentBytes, 0);
						for (var i = 0; i < count; i++)
						{
							var off = 4 + i * 4;
							voxels[new Vector3Int(contentBytes[off], contentBytes[off + 1], contentBytes[off + 2])] = contentBytes[off + 3];
						}
						break;
					case "RGBA":
						for (var i = 0; i < 256; i++)
						{
							var off = i * 4;
							palette[i] = new Color32(contentBytes[off], contentBytes[off + 1],
								contentBytes[off + 2],
								contentBytes[off + 3]);
						}

						break;
				}
			}

			return (size, voxels, palette);
		}

		private static void ReadChunkHeader(BinaryReader r, out string id, out int contentSize, out int childrenSize)
		{
			id = new string(r.ReadChars(4));
			contentSize = r.ReadInt32();
			childrenSize = r.ReadInt32();
		}
	}
}
