using System.Globalization;
using System.Text;

namespace Assembler.Voxels.Pipeline
{
	/// <summary>
	/// Result of the deterministic geometry validation pass (Phase 4): how
	/// connected the model is, how many fragments were auto-pruned, and how
	/// symmetric it is about its bounding-box X-plane. Surfaced to the window log
	/// and injected into the vision-critique prompt.
	/// </summary>
	public sealed record GeometryReport(
		int TotalVoxels,
		int ComponentCount,
		int LargestComponentSize,
		int PrunedVoxels,
		float SymmetryScore)
	{
		/// <summary>True when nothing floats and the model reads as symmetric.</summary>
		public bool IsClean => ComponentCount <= 1 && SymmetryScore >= 0.9f;

		/// <summary>Human/LLM-readable one-liner for the log and critique prompt.</summary>
		public string Summarise()
		{
			var sb = new StringBuilder();
			sb.Append("Geometry: ").Append(TotalVoxels).Append(" voxels, ")
				.Append(ComponentCount).Append(ComponentCount == 1 ? " component" : " components");
			if (ComponentCount > 1)
			{
				sb.Append(" (largest ").Append(LargestComponentSize).Append(')');
			}

			if (PrunedVoxels > 0)
			{
				sb.Append(", auto-pruned ").Append(PrunedVoxels).Append(" orphan voxel(s)");
			}

			sb.Append(", symmetry ").Append((SymmetryScore * 100f).ToString("0", CultureInfo.InvariantCulture)).Append('%');
			return sb.ToString();
		}
	}
}
