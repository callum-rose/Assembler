using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One part's slice of the reference hull: the box-restricted intersection of
	/// the visual hull with the part's planned world box (decision 2), expressed in
	/// the part's own local cells. A cell is <em>allowed</em> when the world voxel
	/// it maps to lies inside the hull within the target frame. This is both the
	/// authoring guidance ("author ONLY inside #") and the feasibility signal
	/// (<see cref="InHullFraction"/>); the hard enforcement clip lives in
	/// <see cref="ForwardHullBound"/>.
	///
	/// Pure: no I/O, no clock, no RNG — exact-assertion testable.
	/// </summary>
	public sealed class PartHullMask
	{
		private readonly bool[,,] _allowed;
		private readonly Vector3Int _offset;
		private readonly Vector3Int _size;

		private PartHullMask(bool[,,] allowed, Vector3Int offset, Vector3Int size, int solidCount)
		{
			_allowed = allowed;
			_offset = offset;
			_size = size;
			SolidCount = solidCount;
		}

		/// <summary>Allowed (in-hull) cells across the whole box.</summary>
		public int SolidCount { get; }

		/// <summary>Total cells in the part's declared box.</summary>
		public int CellCount => _size.x * _size.y * _size.z;

		/// <summary>Fraction of the part's box that lies inside the hull; the feasibility floor compares against this.</summary>
		public float InHullFraction => CellCount == 0 ? 0f : (float)SolidCount / CellCount;

		/// <summary>True when no cell of the box is inside the hull — the part's box sits entirely outside the envelope.</summary>
		public bool IsEmpty => SolidCount == 0;

		/// <summary>The part's declared box size (local cells).</summary>
		public Vector3Int Size => _size;

		/// <summary>Whether a part-local cell is inside the envelope. Cells outside the declared box are never allowed.</summary>
		public bool Allows(Vector3Int localCell)
		{
			var i = localCell - _offset;
			return i.x >= 0 && i.x < _size.x && i.y >= 0 && i.y < _size.y && i.z >= 0 && i.z < _size.z
				   && _allowed[i.x, i.y, i.z];
		}

		/// <summary>
		/// Builds the mask for one part: for every cell of the part's declared box,
		/// the cell is allowed iff its world voxel (the part's accumulated world
		/// pivot plus the local cell) is inside <paramref name="hull"/> within the
		/// target frame.
		/// </summary>
		public static PartHullMask For(
			VoxelRigModel model,
			VoxelPart part,
			Vector3Int offset,
			Vector3Int size,
			SilhouetteHull hull,
			Vector3Int frameMin,
			Vector3Int frameSize)
		{
			var pivot = PlanGeometryChecks.WorldPivot(model, part);
			var allowed = new bool[Mathf.Max(0, size.x), Mathf.Max(0, size.y), Mathf.Max(0, size.z)];
			var solid = 0;
			for (var x = 0; x < size.x; x++)
			{
				for (var y = 0; y < size.y; y++)
				{
					for (var z = 0; z < size.z; z++)
					{
						var world = pivot + offset + new Vector3Int(x, y, z);
						if (hull.Contains(world, frameMin, frameSize))
						{
							allowed[x, y, z] = true;
							solid++;
						}
					}
				}
			}

			return new PartHullMask(allowed, offset, size, solid);
		}

		/// <summary>
		/// The allowed region as 3D ASCII layers in the same form the author writes
		/// (one layer per y bottom-to-top; each layer is size.z rows, row 0 = the
		/// front z=0, of size.x characters): '#' allowed, '.' forbidden.
		/// </summary>
		public IReadOnlyList<string> ToAsciiLayers()
		{
			var layers = new List<string>(Mathf.Max(0, _size.y));
			for (var y = 0; y < _size.y; y++)
			{
				var layer = new StringBuilder();
				for (var z = 0; z < _size.z; z++)
				{
					for (var x = 0; x < _size.x; x++)
					{
						layer.Append(_allowed[x, y, z] ? '#' : '.');
					}

					if (z < _size.z - 1)
					{
						layer.Append('\n');
					}
				}

				layers.Add(layer.ToString());
			}

			return layers;
		}
	}
}
