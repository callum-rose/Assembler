using System;
using System.Text;

namespace Assembler.Voxels
{
	/// <summary>
	/// Goxel text stores Z-up (matches the Goxel editor and the .vox format).
	/// Claude is asked to produce Y-up (matches Unity). The swap is involutive,
	/// so this single helper handles both directions.
	/// </summary>
	public static class GoxelCoordinateConverter
	{
		public static string SwapYAndZ(string goxelText)
		{
			var sb = new StringBuilder(goxelText.Length);
			var lines = goxelText.Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var trimmed = line.TrimStart();
				if (trimmed.Length == 0 || trimmed[0] == '#')
				{
					sb.Append(line);
				}
				else
				{
					var parts = line.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 4
					    && int.TryParse(parts[0], out _)
					    && int.TryParse(parts[1], out _)
					    && int.TryParse(parts[2], out _))
					{
						sb.Append(parts[0]).Append(' ').Append(parts[2]).Append(' ').Append(parts[1]).Append(' ').Append(parts[3]);
					}
					else
					{
						sb.Append(line);
					}
				}
				if (i < lines.Length - 1) sb.Append('\n');
			}
			return sb.ToString();
		}
	}
}
