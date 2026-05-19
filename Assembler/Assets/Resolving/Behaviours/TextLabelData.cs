using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class TextLabelData : BehaviourData
	{
		public IValueProvider<string> Text { get; }
		public IValueProvider<string> Label { get; }
		public ScreenRect Rect { get; }

		public TextLabelData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<string> text,
			IValueProvider<string> label,
			ScreenRect rect) : base(id, listeners) => (Text, Label, Rect) = (text, label, rect);
	}
}
