
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on the frame the named key is pressed down.</summary>
	/// <remarks>
	/// Properties:
	///   Key: Legacy input key name (lowercase), e.g. "space", "w", "mouse0", "escape".
	/// </remarks>
	public class KeyDownTrigger : InputTrigger<KeyDownTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKeyDown(Data.Key.Get()))
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}
