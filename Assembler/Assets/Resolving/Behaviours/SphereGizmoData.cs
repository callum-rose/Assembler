using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class SphereGizmoData : BehaviourData
	{
		public IValueProvider<float> Radius { get; }
		public IValueProvider<bool> IsWire { get; }
		public IValueProvider<Color> Colour { get; }

		public SphereGizmoData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<float> radius,
			IValueProvider<bool> isWire,
			IValueProvider<Color> colour) : base(id, listeners) => (Radius, IsWire, Colour) = (radius, isWire, colour);
	}
}