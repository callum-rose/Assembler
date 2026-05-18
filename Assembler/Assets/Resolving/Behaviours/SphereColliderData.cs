using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SphereColliderData : BehaviourData
	{
		public IValueProvider<float> Radius { get; }
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;

		public SphereColliderData(string id, IReadOnlyList<Action> listeners, IValueProvider<float> radius) : base(id,
			listeners) => Radius = radius;
	}
}