using System;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ProgressBarData : BehaviourData
	{
		public IValueProvider<float> Value { get; }
		public ScreenRect Rect { get; }

		public ProgressBarData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<float> value,
			ScreenRect rect) : base(id, listeners) => (Value, Rect) = (value, rect);
	}
}
