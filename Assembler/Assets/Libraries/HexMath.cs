using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// First-class hex-grid helpers, the hexagonal companion to GridMath. Hex cells are
	/// carried as axial coordinates Vector3(q, r, 0); world conversions take a hex "size"
	/// (centre-to-corner radius). Registered globally in CompiledExpressionsRegistry so
	/// every descriptor expression can call these by bare name (HexToWorldPointy,
	/// HexDistance, HexNeighbour, ...). Direction indices run 0..5; for the standard axial
	/// layout they are +q, +q-r, -r, -q, -q+r, +r. All numeric parameters are float so int
	/// arguments coerce automatically during overload resolution.
	/// </summary>
	public static class HexMath
	{
		// Axial neighbour offsets, indexed 0..5.
		private readonly static Vector3[] Directions =
		{
			new(1f, 0f, 0f),
			new(1f, -1f, 0f),
			new(0f, -1f, 0f),
			new(-1f, 0f, 0f),
			new(-1f, 1f, 0f),
			new(0f, 1f, 0f),
		};

		/// <summary>World position of a pointy-top hex cell (flat sides left/right).</summary>
		/// <param name="hex">Hex cell as axial Vector3(q, r, 0).</param>
		/// <param name="size">Centre-to-corner radius of a hex in world units.</param>
		/// <returns>The hex centre's world position (z = 0).</returns>
		public static Vector3 HexToWorldPointy(Vector3 hex, float size)
		{
			float x = size * (Mathf.Sqrt(3f) * hex.x + Mathf.Sqrt(3f) / 2f * hex.y);
			float y = size * (3f / 2f * hex.y);
			return new Vector3(x, y, 0f);
		}

		/// <summary>World position of a flat-top hex cell (flat sides top/bottom).</summary>
		/// <param name="hex">Hex cell as axial Vector3(q, r, 0).</param>
		/// <param name="size">Centre-to-corner radius of a hex in world units.</param>
		/// <returns>The hex centre's world position (z = 0).</returns>
		public static Vector3 HexToWorldFlat(Vector3 hex, float size)
		{
			float x = size * (3f / 2f * hex.x);
			float y = size * (Mathf.Sqrt(3f) / 2f * hex.x + Mathf.Sqrt(3f) * hex.y);
			return new Vector3(x, y, 0f);
		}

		/// <summary>Number of steps between two hex cells (hex/cube distance).</summary>
		/// <param name="a">First hex as axial Vector3(q, r, 0).</param>
		/// <param name="b">Second hex as axial Vector3(q, r, 0).</param>
		/// <returns>The minimum number of single-step moves between the cells.</returns>
		public static float HexDistance(Vector3 a, Vector3 b)
		{
			float dq = a.x - b.x;
			float dr = a.y - b.y;
			return (Mathf.Abs(dq) + Mathf.Abs(dq + dr) + Mathf.Abs(dr)) / 2f;
		}

		/// <summary>The neighbouring hex in one of the six directions (index reduced mod 6).</summary>
		/// <param name="hex">Hex cell as axial Vector3(q, r, 0).</param>
		/// <param name="directionIndex">Direction 0..5 (any int; reduced mod 6).</param>
		/// <returns>The neighbouring hex cell.</returns>
		public static Vector3 HexNeighbour(Vector3 hex, int directionIndex)
		{
			Vector3 d = Directions[((directionIndex % 6) + 6) % 6];
			return new Vector3(hex.x + d.x, hex.y + d.y, 0f);
		}

		/// <summary>All six neighbours of a hex cell, in direction order 0..5.</summary>
		/// <param name="hex">Hex cell as axial Vector3(q, r, 0).</param>
		/// <returns>A list of the six neighbouring hex cells.</returns>
		public static List<Vector3> HexNeighbours(Vector3 hex)
		{
			var result = new List<Vector3>(6);
			foreach (var d in Directions)
			{
				result.Add(new Vector3(hex.x + d.x, hex.y + d.y, 0f));
			}
			return result;
		}

		/// <summary>Round a fractional axial hex to the nearest integer hex cell (cube rounding).</summary>
		/// <param name="hex">Fractional hex as axial Vector3(q, r, 0).</param>
		/// <returns>The nearest valid hex cell as axial Vector3(q, r, 0).</returns>
		public static Vector3 HexRound(Vector3 hex)
		{
			// Axial -> cube, round each cube axis, then repair the largest-drift axis so
			// the cube constraint x + y + z == 0 holds.
			float x = hex.x;
			float z = hex.y;
			float y = -x - z;

			float rx = Mathf.Round(x);
			float ry = Mathf.Round(y);
			float rz = Mathf.Round(z);

			float dx = Mathf.Abs(rx - x);
			float dy = Mathf.Abs(ry - y);
			float dz = Mathf.Abs(rz - z);

			if (dx > dy && dx > dz)
			{
				rx = -ry - rz;
			}
			else if (dy > dz)
			{
				ry = -rx - rz;
			}
			else
			{
				rz = -rx - ry;
			}

			return new Vector3(rx, rz, 0f);
		}
	}
}
