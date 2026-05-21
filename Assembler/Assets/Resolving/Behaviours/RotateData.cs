using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class RotateData : BehaviourData
	{
		public IValueProvider<Vector3> Displacement { get; }

		public RotateData(string id, IReadOnlyList<Action> listeners, IValueProvider<Vector3> displacement) : base(id,
			listeners) =>
			Displacement = displacement;
	}
}
