using System;
using System.Collections.Generic;

namespace Assembler.Resolving
{
	public abstract class BehaviourData
	{
		public string Id { get; }
		public IReadOnlyList<Action> Listeners { get; }

		protected BehaviourData(string id, IReadOnlyList<Action> listeners)
		{
			Id = id;
			Listeners = listeners;
		}
	}

}