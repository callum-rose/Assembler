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
						IValueProvider<bool> initialValue,
			IValueProvider<string> label,
			ScreenRect rect) : base(id) => (InitialValue, Label, Rect) = (initialValue, label, rect);
	}
}
