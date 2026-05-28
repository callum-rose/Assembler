using System;
using Assembler.Parsing;
using System.Collections.Generic;

namespace Assembler.Resolving.Behaviours
{
	public class UIInputFieldData : TriggerData
	{
		public ScreenRect Rect { get; }

		public UIInputFieldData(string id,
						ScreenRect rect) : base(id) => Rect = rect;
	}
}
