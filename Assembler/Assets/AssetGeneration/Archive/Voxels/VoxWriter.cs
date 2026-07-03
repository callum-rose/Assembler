using System.IO;
using UnityEngine;

namespace Assembler.Voxels
{
	/// <summary>
	/// Writes a <see cref="VoxelModel"/> out as a MagicaVoxel .vox file
	/// (RIFF-style: MAIN -> SIZE, XYZI, RGBA). Voxel positions are translated so
	/// the model's bounding-box minimum sits at (0,0,0).
	/// </summary>
	public static class VoxWriter
	{
		private const int VoxVersion = 150;

		public static byte[] Write(VoxelModel model)
		{
			using var ms = new MemoryStream();
			using var w = new BinaryWriter(ms);

			w.Write(new[] { (byte)'V', (byte)'O', (byte)'X', (byte)' ' });
			w.Write(VoxVersion);

			var sizeChunk = BuildSizeChunk(model);
			var xyziChunk = BuildXyziChunk(model);
			var rgbaChunk = BuildRgbaChunk(model);

			var childrenSize = sizeChunk.Length + xyziChunk.Length + rgbaChunk.Length;
			WriteChunkHeader(w, "MAIN", contentSize: 0, childrenSize: childrenSize);
			w.Write(sizeChunk);
			w.Write(xyziChunk);
			w.Write(rgbaChunk);

			return ms.ToArray();
		}

		private static byte[] BuildSizeChunk(VoxelModel model)
		{
			using var ms = new MemoryStream();
			using var w = new BinaryWriter(ms);
			var size = model.Size;
			WriteChunkHeader(w, "SIZE", contentSize: 12, childrenSize: 0);
			w.Write(size.x);
			w.Write(size.y);
			w.Write(size.z);
			return ms.ToArray();
		}

		private static byte[] BuildXyziChunk(VoxelModel model)
		{
			using var ms = new MemoryStream();
			using var w = new BinaryWriter(ms);
			var count = model.Voxels.Count;

			var ext = model.Max - model.Min;
			if (ext.x > 255 || ext.y > 255 || ext.z > 255)
			{
				throw new InvalidDataException(
					$"VoxWriter: model extent {ext.x + 1}x{ext.y + 1}x{ext.z + 1} exceeds the 256-cell .vox coordinate limit.");
			}

			WriteChunkHeader(w, "XYZI", contentSize: 4 + count * 4, childrenSize: 0);
			w.Write(count);

			var min = model.Min;
			foreach (var kv in model.Voxels)
			{
				var p = kv.Key - min;
				w.Write((byte)p.x);
				w.Write((byte)p.y);
				w.Write((byte)p.z);
				w.Write(kv.Value);
			}

			return ms.ToArray();
		}

		private static byte[] BuildRgbaChunk(VoxelModel model)
		{
			using var ms = new MemoryStream();
			using var w = new BinaryWriter(ms);
			WriteChunkHeader(w, "RGBA", contentSize: 256 * 4, childrenSize: 0);

			// MagicaVoxel: voxel index i maps to palette[i-1]. Entry 255 is unused.
			for (var i = 0; i < 256; i++)
			{
				Color32 c = i < model.Palette.Length ? model.Palette[i] : new Color32(0, 0, 0, 255);
				w.Write(c.r);
				w.Write(c.g);
				w.Write(c.b);
				w.Write(c.a);
			}

			return ms.ToArray();
		}

		private static void WriteChunkHeader(BinaryWriter w, string id, int contentSize, int childrenSize)
		{
			w.Write((byte)id[0]);
			w.Write((byte)id[1]);
			w.Write((byte)id[2]);
			w.Write((byte)id[3]);
			w.Write(contentSize);
			w.Write(childrenSize);
		}
	}
}
