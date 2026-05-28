using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class VelocityData : BehaviourData
	{
		public IValueProvider<Vector3> Velocity { get; }

		public VelocityData(string id, IValueProvider<Vector3> velocity) :
			base(id) => Velocity = velocity;
	}
}