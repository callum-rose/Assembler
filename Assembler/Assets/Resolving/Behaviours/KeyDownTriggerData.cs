using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public sealed class KeyDownTriggerData : TriggerData
	{
		public IValueProvider<string> Key { get; }

		public KeyDownTriggerData(string id, IValueProvider<string> key) :
			base(id) => Key = key;
	}
}
