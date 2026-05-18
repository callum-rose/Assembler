using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class KeyUpTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyUpTriggerData(string id, IValueProvider<string> key, IReadOnlyList<Action> listeners) : base(id,
			listeners) => Key = key;
	}
}