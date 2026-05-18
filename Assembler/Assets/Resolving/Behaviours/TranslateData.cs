using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class TranslateData : BehaviourData
	{
		public IValueProvider<Vector3> Displacement { get; }

		public TranslateData(string id, IReadOnlyList<Action> listeners, IValueProvider<Vector3> displacement) : base(id,
			listeners) =>
			Displacement = displacement;
	}
}