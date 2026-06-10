using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// Regular-layout helpers that each return a <c>List&lt;Vector3&gt;</c> of world positions, for use as a
	/// Placements <c>At</c> source (e.g. <c>!expr { Do: 'GridPositions(19, 21, 0.5f, origin)', ReturnType:
	/// vector list }</c>). Registered globally in CompiledExpressionsRegistry so every descriptor expression
	/// can call these by bare name (GridPositions, LinePositions, RingPositions). For irregular layouts
	/// (a grid with holes) build the list imperatively with <c>PositionList</c> instead. All numeric
	/// parameters are float so int arguments coerce automatically during overload resolution.
	/// </summary>
	public static class LayoutMath
	{
		/// <summary>A row-major grid of cell-centre world positions, starting at <paramref name="origin"/>.</summary>
		/// <param name="cols">Number of columns (cast to int; clamped at 0).</param>
		/// <param name="rows">Number of rows (cast to int; clamped at 0).</param>
		/// <param name="cellSize">Spacing between adjacent cells in world units.</param>
		/// <param name="origin">World position of cell (0, 0).</param>
		/// <returns>cols × rows positions, ordered row by row (all of row 0, then row 1, …).</returns>
		public static List<Vector3> GridPositions(float cols, float rows, float cellSize, Vector3 origin)
		{
			var colCount = Mathf.Max(0, (int)cols);
			var rowCount = Mathf.Max(0, (int)rows);
			var result = new List<Vector3>(colCount * rowCount);

			for (var y = 0; y < rowCount; y++)
			{
				for (var x = 0; x < colCount; x++)
				{
					result.Add(new Vector3(origin.x + x * cellSize, origin.y + y * cellSize, origin.z));
				}
			}

			return result;
		}

		/// <summary>Evenly spaced positions along the segment from <paramref name="start"/> to <paramref name="end"/>.</summary>
		/// <param name="start">First position (always included).</param>
		/// <param name="end">Last position (included when count &gt; 1).</param>
		/// <param name="count">Number of positions (clamped at 0; a count of 1 yields just <paramref name="start"/>).</param>
		/// <returns>The <paramref name="count"/> positions, endpoints inclusive.</returns>
		public static List<Vector3> LinePositions(Vector3 start, Vector3 end, int count)
		{
			var n = Mathf.Max(0, count);
			var result = new List<Vector3>(n);

			for (var i = 0; i < n; i++)
			{
				var t = n == 1 ? 0f : i / (float)(n - 1);
				result.Add(Vector3.Lerp(start, end, t));
			}

			return result;
		}

		/// <summary>Positions evenly spaced around a circle, the first at angle 0 (along +x).</summary>
		/// <param name="center">Centre of the ring.</param>
		/// <param name="radius">Ring radius in world units.</param>
		/// <param name="count">Number of positions, spaced 360/count degrees apart (clamped at 0).</param>
		/// <returns>The <paramref name="count"/> positions, counter-clockwise from +x, on the z = center.z plane.</returns>
		public static List<Vector3> RingPositions(Vector3 center, float radius, int count)
		{
			var n = Mathf.Max(0, count);
			var result = new List<Vector3>(n);

			for (var i = 0; i < n; i++)
			{
				var angle = 2f * Mathf.PI * i / n;
				result.Add(new Vector3(center.x + radius * Mathf.Cos(angle), center.y + radius * Mathf.Sin(angle), center.z));
			}

			return result;
		}
	}
}
