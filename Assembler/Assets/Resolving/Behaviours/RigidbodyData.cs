using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class RigidbodyData : BehaviourData
	{
		public IValueProvider<bool> IsKinematic { get; init; } = NullValueProvider<bool>.Instance;
		public IValueProvider<bool> UseGravity { get; init; } = NullValueProvider<bool>.Instance;

		public RigidbodyData(string id) : base(id) { }
	}
}