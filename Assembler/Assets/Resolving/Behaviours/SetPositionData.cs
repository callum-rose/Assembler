using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SetPositionData : BehaviourData
	{
		public IValueProvider<Vector3> ValueExpression { get; }

		public SetPositionData(string id, IValueProvider<Vector3> valueExpression) : base(id) =>
			ValueExpression = valueExpression;
	}
}