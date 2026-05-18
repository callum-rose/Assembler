using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	public class KeyHoldTrigger : InputTrigger<KeyHoldTriggerData>
	{
		private void Update()
		{
			var keyCode = Data.Key.Value switch
			{
				"w" => KeyCode.W,
				"s" => KeyCode.S,
				"d" => KeyCode.D,
				"a" => KeyCode.A,
				"left" => KeyCode.LeftArrow,
				"right" => KeyCode.RightArrow,
				"up" => KeyCode.UpArrow,
				"down" => KeyCode.DownArrow
			};
			
			if (UnityEngine.Input.GetKey(keyCode))
			{
				InvokeListeners();
			}
		}
	}
}