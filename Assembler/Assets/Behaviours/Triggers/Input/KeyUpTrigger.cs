
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on the frame the named key is released.</summary>
	/// <remarks>
	/// Properties:
	///   Key: KeyCode name to listen for (e.g. "Space", "W", "Mouse0").
	/// </remarks>
	public class KeyUpTrigger : InputTrigger<KeyUpTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKeyUp(Data.Key.Get()))
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}
