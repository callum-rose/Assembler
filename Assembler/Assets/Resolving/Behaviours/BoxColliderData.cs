using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public sealed class BoxColliderData : BehaviourData
	{
		public IValueProvider<Vector3> Size { get; }
		public IValueProvider<bool> IsTrigger { get; }

		public BoxColliderData(string id,
						IValueProvider<Vector3> size,
			IValueProvider<bool> isTrigger) :
			base(id) =>
			(Size, IsTrigger) = (size, isTrigger);
	}
}