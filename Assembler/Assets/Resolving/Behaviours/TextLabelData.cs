using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class TextLabelData : BehaviourData
	{
		public IValueProvider<string> Text { get; }
		public IValueProvider<string> Label { get; }
		public IValueProvider<int> FontSize { get; }
		public ScreenRect Rect { get; }

		public TextLabelData(string id,
						IValueProvider<string> text,
			IValueProvider<string> label,
			IValueProvider<int> fontSize,
			ScreenRect rect) : base(id) => (Text, Label, FontSize, Rect) = (text, label, fontSize, rect);
	}
}
