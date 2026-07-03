using System.Globalization;
using System.Text;

namespace Assembler.Voxels
{
	/// <summary>
	/// Inverse of <see cref="GoxelTextParser"/>: serialises a <see cref="VoxelModel"/>
	/// back to Goxel plain-text export format (one voxel per line, "x y z RRGGBB").
	/// Coordinates are emitted in the same convention they were stored in — the
	/// editor window keeps text in Z-up, which is what Goxel itself expects.
	/// </summary>
	public static class GoxelTextWriter
	{
		public static string Write(VoxelModel model)
		{
			var sb = new StringBuilder();
			foreach (var kv in model.Voxels)
			{
				var pos = kv.Key;
				var paletteIndex = kv.Value;
				// Palette is 1-based to match .vox convention; Voxels dict stores 1-based indices.
				var colour = model.Palette[paletteIndex - 1];
				sb.Append(pos.x.ToString(CultureInfo.InvariantCulture)).Append(' ')
					.Append(pos.y.ToString(CultureInfo.InvariantCulture)).Append(' ')
					.Append(pos.z.ToString(CultureInfo.InvariantCulture)).Append(' ')
					.Append(colour.r.ToString("x2", CultureInfo.InvariantCulture))
					.Append(colour.g.ToString("x2", CultureInfo.InvariantCulture))
					.Append(colour.b.ToString("x2", CultureInfo.InvariantCulture));
				if (colour.a != 255)
				{
					sb.Append(colour.a.ToString("x2", CultureInfo.InvariantCulture));
				}
				sb.Append('\n');
			}
			return sb.ToString();
		}
	}
}
