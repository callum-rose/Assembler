using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>Fires every frame while the named key is held down.</summary>
	/// <remarks>
	/// Properties:
	///   Key: Legacy input key name (lowercase), e.g. "w", "space", "escape", "up", "mouse0".
	/// </remarks>
	public class KeyHoldTrigger : InputTrigger<KeyHoldTriggerData>
	{
		private void Update()
		{
			if (UnityEngine.Input.GetKey(Data.Key.Get()))
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}
	}
}
