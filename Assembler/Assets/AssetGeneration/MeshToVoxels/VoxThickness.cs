using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
	/// <summary>
	/// Per-voxel bounded morphological-erosion depth — how many erosion passes a voxel survives,
	/// capped at a caller-supplied limit. Used to spot sub-Nyquist features: a structure thinner
	/// than the downres/resample ratio (an antenna, a fin) that a coverage-majority vote would
	/// otherwise erase. Shared by the integer-block <see cref="VoxDownres"/> and the area-weighted
	/// <see cref="VoxResample"/>, which differ only in how they consume the depth (integer factor
	/// vs fractional ratio).
	/// </summary>
	internal static class VoxThickness
	{
		/// <summary>
		/// Erosion depth per occupied voxel, capped at <paramref name="cap"/>: surface voxels (an empty
		/// face-neighbour, or the grid edge) erode at pass 1 → depth 1; a voxel that survives all
		/// <paramref name="cap"/> passes is at least that deep → depth = cap. Out-of-bounds counts as
		/// empty, so the model's outer shell erodes inward as expected. A non-occupied voxel has depth 0.
		/// </summary>
		public static int[] Map(VoxModel model, int cap)
		{
			cap = Mathf.Max(1, cap);
			int n = model.Occupied.Length;
			var thickness = new int[n];
			var current = (bool[])model.Occupied.Clone();
			var next = new bool[n];

			for (int pass = 1; pass <= cap; pass++)
			{
				bool anyRemoved = false;
				for (int z = 0; z < model.Z; z++)
				{
					for (int y = 0; y < model.Y; y++)
					{
						for (int x = 0; x < model.X; x++)
						{
							int i = model.Index(x, y, z);
							if (!current[i])
							{
								// Explicitly clear in the scratch buffer — leaving it stale would let a
								// voxel removed on an earlier pass reappear after the buffer swap (and then
								// wrongly pick up the "survived all passes → cap" depth at the end).
								next[i] = false;
								continue;
							}

							if (IsInterior(current, model, x, y, z))
							{
								next[i] = true;
							}
							else
							{
								next[i] = false;
								thickness[i] = pass;
								anyRemoved = true;
							}
						}
					}
				}

				(current, next) = (next, current);
				if (!anyRemoved)
				{
					break;
				}
			}

			// Whatever still stands after the capped passes is at least `cap` deep.
			for (int i = 0; i < n; i++)
			{
				if (current[i])
				{
					thickness[i] = cap;
				}
			}
			return thickness;
		}

		// A voxel is interior (survives erosion) only if all six face-neighbours are still present;
		// a neighbour off the grid counts as empty, so edge voxels are never interior.
		private static bool IsInterior(bool[] current, VoxModel model, int x, int y, int z)
		{
			foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
			{
				int nx = x + dx, ny = y + dy, nz = z + dz;
				if (!model.InBounds(nx, ny, nz) || !current[model.Index(nx, ny, nz)])
				{
					return false;
				}
			}
			return true;
		}
	}
}
