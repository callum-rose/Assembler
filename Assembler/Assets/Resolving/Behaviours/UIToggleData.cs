using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class UIToggleData : TriggerData
	{
		public IValueProvider<bool> InitialValue { get; }
		public IValueProvider<string> Label { get; }
		public ScreenRect Rect { get; }

		public UIToggleData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<bool> initialValue,
			IValueProvider<string> label,
			ScreenRect rect) : base(id, listeners) => (InitialValue, Label, Rect) = (initialValue, label, rect);
	}
}
