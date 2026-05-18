using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class KeyHoldTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyHoldTriggerData(string id, IValueProvider<string> key, IReadOnlyList<Action> listeners) :
			base(id, listeners) => Key = key;
	}
}