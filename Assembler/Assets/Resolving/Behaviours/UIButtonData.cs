using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class UIButtonData : TriggerData
	{
		public IValueProvider<string> Label { get; }
		public ScreenRect Rect { get; }

		public UIButtonData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> label,
			ScreenRect rect) : base(id, listeners) => (Label, Rect) = (label, rect);
	}
}
