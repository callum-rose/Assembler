using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SetPositionData : BehaviourData
	{
		public IValueProvider<Vector3> ValueExpression { get; }

		public SetPositionData(string id, IReadOnlyList<Action> listeners, IValueProvider<Vector3> valueExpression) : base(id,
			listeners) =>
			ValueExpression = valueExpression;
	}
}