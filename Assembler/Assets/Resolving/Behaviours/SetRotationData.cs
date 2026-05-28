using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SetRotationData : BehaviourData
	{
		public IValueProvider<Vector3> ValueExpression { get; }

		public SetRotationData(string id, IValueProvider<Vector3> valueExpression) : base(id) =>
			ValueExpression = valueExpression;
	}
}
