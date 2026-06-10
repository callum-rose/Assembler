using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Libraries
{
	/// <summary>
	/// A constructible builder for an irregular list of world positions — the imperative counterpart to
	/// <c>LayoutMath</c>'s regular-layout helpers. Use it from a Placements <c>At</c> expression when the
	/// positions don't follow a closed-form pattern (e.g. a Pacman maze where pills skip wall cells):
	/// <c>new PositionList(); … b.Add(p); return b.ToList();</c>. The expression compiler supports
	/// <c>new PositionList(...)</c> and instance calls but not collection initializers, which is why a
	/// <c>.Add</c>-in-a-loop builder is needed. Registered globally in CompiledExpressionsRegistry so every
	/// descriptor expression can construct it by bare name.
	/// </summary>
	public sealed class PositionList
	{
		private readonly List<Vector3> _positions = new();

		/// <summary>Appends a world position to the list.</summary>
		/// <param name="position">The position to add.</param>
		public void Add(Vector3 position) => _positions.Add(position);

		/// <summary>Returns the accumulated positions as a <c>List&lt;Vector3&gt;</c> (a fresh copy).</summary>
		/// <returns>The positions added so far, in insertion order.</returns>
		public List<Vector3> ToList() => new(_positions);
	}
}
