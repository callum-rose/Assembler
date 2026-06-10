using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class SphereColliderData : BehaviourData
	{
		public IValueProvider<float> Radius { get; }
		public IValueProvider<bool> IsTrigger { get; init; } = NullValueProvider<bool>.Instance;
		public PhysicsMaterialProviders Material { get; init; } = PhysicsMaterialProviders.None;

		public SphereColliderData(string id, IValueProvider<float> radius) : base(id) => Radius = radius;
	}
}
