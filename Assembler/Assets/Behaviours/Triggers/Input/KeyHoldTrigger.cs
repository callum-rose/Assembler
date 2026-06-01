using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame while the named key is held down.</summary>
	/// <remarks>
	/// Properties:
	///   Key: One of "w", "a", "s", "d", "up", "down", "left", "right".
	/// </remarks>
	public class KeyHoldTrigger : InputTrigger<KeyHoldTriggerData>
	{
		private void Update()
		{
			var keyCode = Data.Key.Get() switch
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
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}