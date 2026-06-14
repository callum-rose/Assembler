using Assembler.Resolving;

namespace Assembler.Behaviours.AI
{
	/// <summary>
	/// Finite state machine for entity AI. Holds the current state in an entity string-variable and
	/// transitions between declared states when a transition's condition becomes true. Transitions are
	/// evaluated every frame in declared order, first match wins (one transition per frame), so behaviour
	/// is deterministic. On a transition it fires the old state's <c>OnExit</c> hooks then the new state's
	/// <c>OnEnter</c> hooks; the initial state's <c>OnEnter</c> fires once on start.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   StateVariable: Id of the string entity variable holding the current state. Auto-declared (seeded to Initial) if not already present, so it shows up in the debug console and save snapshots.
	///   Initial: The starting state. Must be one of States.
	///   States: Map of state name to optional { OnEnter, OnExit } hooks. Each hook list uses the same shape as a behaviour's top-level Listeners (EntityId + BehaviourId, EntityTag, BehaviourTag, or !gameover).
	///   Transitions: Ordered list of { from, to, when }. The first transition whose `from` equals the current state and whose `when` condition is true is taken.
	/// </remarks>
	public class StateMachine : GameBehaviour<StateMachineData>
	{
		private void Start() => EnterInitialState();

		// Unity's once-per-lifetime Start does not re-run on a reused component, so re-enter the initial state on
		// a pooled respawn. The state variable is re-seeded to Initial in this spawn's fresh scope (the builder's
		// Create phase), so CurrentState reads Initial here, and listeners are re-resolved by the time OnReuse runs.
		public override void OnReuse() => EnterInitialState();

		private void EnterInitialState()
		{
			// Fire the initial state's OnEnter once, after all behaviours have been initialised so that
			// listener targets are registered.
			if (Data.States.TryGetValue(Data.CurrentState.Get(), out var initial))
			{
				foreach (var listener in initial.OnEnter)
				{
					listener.Notify(TriggerContext.Empty);
				}
			}
		}

		private void Update()
		{
			var current = Data.CurrentState.Get();

			foreach (var transition in Data.Transitions)
			{
				if (transition.From != current || !transition.When.Get())
				{
					continue;
				}

				if (Data.States.TryGetValue(current, out var from))
				{
					foreach (var listener in from.OnExit)
					{
						listener.Notify(TriggerContext.Empty);
					}
				}

				Data.CurrentState.Set(transition.To);

				if (Data.States.TryGetValue(transition.To, out var to))
				{
					foreach (var listener in to.OnEnter)
					{
						listener.Notify(TriggerContext.Empty);
					}
				}

				return; // first match wins; at most one transition per frame
			}
		}
	}
}
