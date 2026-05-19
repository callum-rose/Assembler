using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class UIInputFieldData : TriggerData
	{
		public ScreenRect Rect { get; }

		public UIInputFieldData(string id,
			IReadOnlyList<Action> listeners,
			ScreenRect rect) : base(id, listeners) => Rect = rect;
	}
}
