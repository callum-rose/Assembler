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
						IValueProvider<string> label,
			ScreenRect rect) : base(id) => (Label, Rect) = (label, rect);
	}
}
