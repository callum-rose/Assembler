using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class UISliderData : TriggerData
	{
		public IValueProvider<float> InitialValue { get; }
		public IValueProvider<float> MinValue { get; }
		public IValueProvider<float> MaxValue { get; }
		public ScreenRect Rect { get; }

		public UISliderData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<float> initialValue,
			IValueProvider<float> minValue,
			IValueProvider<float> maxValue,
			ScreenRect rect) : base(id, listeners) => (InitialValue, MinValue, MaxValue, Rect) = (initialValue, minValue, maxValue, rect);
	}
}
