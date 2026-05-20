
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on the frame the named key is pressed down.</summary>
	/// <remarks>
	/// Properties:
	///   Key: KeyCode name to listen for (e.g. "Space", "W", "Mouse0").
	/// </remarks>
	public class KeyDownTrigger : InputTrigger<KeyDownTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKeyDown(Data.Key.Value))
			{
				NotifyListeners();
			}
		}
	}
}