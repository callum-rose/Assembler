using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class CubeGizmoData : BehaviourData
	{
		public IValueProvider<Vector3> Size { get; }
		public IValueProvider<bool> IsWire { get; }
		public IValueProvider<Color> Colour { get; }

		public CubeGizmoData(string id,
						IValueProvider<Vector3> size,
			IValueProvider<bool> isWire,
			IValueProvider<Color> colour) : base(id) => (Size, IsWire, Colour) = (size, isWire, colour);
	}
}