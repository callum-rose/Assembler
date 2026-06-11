using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Polls a boolean value every frame and sets the entity GameObject's active state to match it.</summary>
	/// <remarks>
	/// Properties:
	///   Active: Boolean (usually a variable or expression) re-read each frame; true keeps the entity active, false deactivates it.
	/// Note: this behaviour drives its state from <c>Update</c>, which Unity stops calling once the GameObject is
	/// inactive. If Active evaluates to false the entity deactivates and can no longer poll itself back on — use the
	/// Execute-driven "set active" or "toggle active" behaviours (which fire through listeners and run even while
	/// inactive) when you need to reactivate an entity.
	/// </remarks>
	public class ActivePoll : GameBehaviour<ActivePollData>
	{
		private void Update() => gameObject.SetActive(Data.Active.Get());
	}
}
