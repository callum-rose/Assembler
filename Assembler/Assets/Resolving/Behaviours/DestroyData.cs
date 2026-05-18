using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class DestroyData : BehaviourData
	{
		public DestroyData(string id, IReadOnlyList<Action> listeners) : base(id, listeners) { }
	}
}