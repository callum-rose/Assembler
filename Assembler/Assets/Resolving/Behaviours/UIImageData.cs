using System;
using Assembler.Parsing;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class UIImageData : BehaviourData
	{
		public IValueProvider<Color> Colour { get; }
		public ScreenRect Rect { get; }

		public UIImageData(string id,
			IReadOnlyList<Action> listeners,
			IValueProvider<Color> colour,
			ScreenRect rect) : base(id, listeners) => (Colour, Rect) = (colour, rect);
	}
}
