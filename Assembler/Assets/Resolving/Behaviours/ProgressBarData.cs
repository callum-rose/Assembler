using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class ProgressBarData : BehaviourData
	{
		public IValueProvider<float> Value { get; }
		public ScreenRect Rect { get; }

		public ProgressBarData(string id,
						IValueProvider<float> value,
			ScreenRect rect) : base(id) => (Value, Rect) = (value, rect);
	}
}
