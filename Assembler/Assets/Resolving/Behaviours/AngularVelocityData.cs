using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class AngularVelocityData : BehaviourData
	{
		public IValueProvider<Vector3> AngularVelocity { get; }

		public AngularVelocityData(string id, IReadOnlyList<Action> listeners, IValueProvider<Vector3> angularVelocity) :
			base(id, listeners) => AngularVelocity = angularVelocity;
	}
}
