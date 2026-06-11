using System.Collections.Generic;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Triggers.Conditionals
{
	/// <summary>
	/// Fires its listeners once every referenced trigger has fired at least once, then re-arms. An AND-gate
	/// across triggers: useful for "do X only after A and B and C have all happened" without chaining gates.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   TriggerIds: Ids of the triggers (on this entity) to AND together; fires when all have fired, then resets.
	/// </remarks>
	public class WhenAll : GameBehaviour<WhenAllData>
	{
		private readonly HashSet<string> _fired = new();

		/// <summary>Wires this gate to observe <paramref name="trigger"/> firing under <paramref name="triggerId"/>.
		/// Called by the builder once per referenced trigger.</summary>
		public void Observe(GameBehaviour trigger, string triggerId) =>
			trigger.SubscribeFired(ctx => MarkFired(triggerId, ctx));

		private void MarkFired(string triggerId, TriggerContext ctx)
		{
			_fired.Add(triggerId);

			if (_fired.Count < Data.TriggerIds.Count)
			{
				return;
			}

			// Re-arm before notifying so a listener that loops back in starts a fresh cycle.
			_fired.Clear();
			NotifyListeners(ctx);
		}
	}
}
