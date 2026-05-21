using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class LineGizmoData : BehaviourData
	{
		public IValueProvider<Vector3> Start { get; }
		public IValueProvider<Vector3> End { get; }
		public IValueProvider<Color> Colour { get; }

		public LineGizmoData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<Vector3> start,
			IValueProvider<Vector3> end,
			IValueProvider<Color> colour) : base(id, listeners) => (Start, End, Colour) = (start, end, colour);
	}
}
