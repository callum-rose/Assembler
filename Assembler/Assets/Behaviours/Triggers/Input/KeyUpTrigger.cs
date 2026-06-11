
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires on the frame the named key is released.</summary>
	/// <remarks>
	/// Properties:
	///   Key: Legacy input key name (lowercase), e.g. "space", "w", "mouse0", "escape".
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
