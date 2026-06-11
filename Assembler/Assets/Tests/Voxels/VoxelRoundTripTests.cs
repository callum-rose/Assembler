using System;
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

		// ---- VoxReader robustness -------------------------------------------

		[Test]
		public void VoxReader_NegativeContentSize_Throws()
		{
			var bytes = BuildCorruptVox(contentSize: -1, childrenSize: 0);
			Assert.Throws<InvalidDataException>(() => VoxReader.Read(bytes));
		}

		[Test]
		public void VoxReader_NegativeChildrenSize_Throws()
		{
			var bytes = BuildCorruptVox(contentSize: 0, childrenSize: -1);
			Assert.Throws<InvalidDataException>(() => VoxReader.Read(bytes));
		}

		[Test]
		public void VoxReader_ChunkExtendsBeyondFile_Throws()
		{
			// childrenSize pushes the end position well past the byte array.
			var bytes = BuildCorruptVox(contentSize: 0, childrenSize: 9999);
			Assert.Throws<InvalidDataException>(() => VoxReader.Read(bytes));
		}

		[Test]
		public void VoxReader_XyziCountExceedsContent_Throws()
		{
			var bytes = BuildCorruptXyziVox(count: 10000, actualVoxels: 1);
			Assert.Throws<InvalidDataException>(() => VoxReader.Read(bytes));
		}

		// ---- VoxWriter robustness -------------------------------------------

		[Test]
		public void VoxWriter_ExtentOver255_Throws()
		{
			const string text = "0 0 0 ff0000\n256 0 0 00ff00"; // extent 256 in X
			var model = GoxelTextParser.Parse(text);
			Assert.Throws<InvalidDataException>(() => VoxWriter.Write(model));
		}

		[Test]
		public void VoxWriter_ExtentExactly255_Succeeds()
		{
			const string text = "0 0 0 ff0000\n255 0 0 00ff00"; // extent exactly 255
			var model = GoxelTextParser.Parse(text);
			Assert.DoesNotThrow(() => VoxWriter.Write(model));
		}

		// ---- GoxelTextParser skipped-line count -----------------------------

		[Test]
		public void GoxelTextParser_ReportsSkippedLines()
		{
			const string text = "0 0 0 ff0000\nbadline\n# comment\n1 0 0 notahex";
			var model = GoxelTextParser.Parse(text, out var skipped);
			Assert.AreEqual(1, model.Voxels.Count);
			Assert.AreEqual(2, skipped, "badline + notahex should be counted");
		}

		[Test]
		public void GoxelTextParser_NoSkipped_ReturnsZero()
		{
			const string text = "0 0 0 ff0000\n1 0 0 00ff00";
			GoxelTextParser.Parse(text, out var skipped);
			Assert.AreEqual(0, skipped);
		}

		// ---- Helpers for corrupt .vox construction --------------------------

		private static byte[] BuildCorruptVox(int contentSize, int childrenSize)
		{
			using var ms = new System.IO.MemoryStream();
			using var w = new System.IO.BinaryWriter(ms);
			// Header
			w.Write(new[] { (byte)'V', (byte)'O', (byte)'X', (byte)' ' });
			w.Write(150); // version
			// MAIN chunk with the given (possibly corrupt) sizes
			w.Write(new[] { (byte)'M', (byte)'A', (byte)'I', (byte)'N' });
			w.Write(contentSize);
			w.Write(childrenSize);
			return ms.ToArray();
		}

		private static byte[] BuildCorruptXyziVox(int count, int actualVoxels)
		{
			using var ms = new System.IO.MemoryStream();
			using var w = new System.IO.BinaryWriter(ms);
			w.Write(new[] { (byte)'V', (byte)'O', (byte)'X', (byte)' ' });
			w.Write(150);
			// MAIN with SIZE + XYZI children
			var xyziContent = 4 + actualVoxels * 4; // header count + actual data
			var childrenSize = 12 + 12 + xyziContent; // SIZE header+content + XYZI header+content
			w.Write(new[] { (byte)'M', (byte)'A', (byte)'I', (byte)'N' });
			w.Write(0); w.Write(childrenSize);
			// SIZE chunk
			w.Write(new[] { (byte)'S', (byte)'I', (byte)'Z', (byte)'E' });
			w.Write(12); w.Write(0);
			w.Write(1); w.Write(1); w.Write(1);
			// XYZI chunk: content claims `count` voxels but only `actualVoxels` are present
			w.Write(new[] { (byte)'X', (byte)'Y', (byte)'Z', (byte)'I' });
			w.Write(xyziContent); w.Write(0);
			w.Write(count); // advertised count > actual data
			for (var i = 0; i < actualVoxels; i++)
			{
				w.Write((byte)0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)1);
			}
			return ms.ToArray();
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
