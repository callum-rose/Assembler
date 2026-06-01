using System.Collections.Generic;
using Assembler.Resolving;

namespace Assembler.Behaviours.AI
{
	/// <summary>Resolved transition: when in <see cref="From"/> and <see cref="When"/> is true, switch to
	/// <see cref="To"/>.</summary>
	public sealed record StateTransition(string From, string To, IValueProvider<bool> When);

	/// <summary>A resolved state with its entry/exit listener hooks.</summary>
	public sealed class StateMachineState
	{
		public string Name { get; }
		public IReadOnlyList<Listener> OnEnter { get; }
		public IReadOnlyList<Listener> OnExit { get; }

		public StateMachineState(string name, IReadOnlyList<Listener> onEnter, IReadOnlyList<Listener> onExit) =>
			(Name, OnEnter, OnExit) = (name, onEnter, onExit);
	}

	/// <summary>
	/// Lives in the Behaviours assembly (not Resolving) because it holds resolved <see cref="Listener"/>
	/// hooks, which Resolving cannot reference.
	/// </summary>
	public sealed class StateMachineData : BehaviourData
	{
		/// <summary>Settable provider pointing at the entity variable that holds the current state name.</summary>
		public IValueProvider<string> CurrentState { get; }

		public string Initial { get; }

		/// <summary>Transitions in declared order (first match wins).</summary>
		public IReadOnlyList<StateTransition> Transitions { get; }

		/// <summary>States by name, for OnEnter/OnExit hook lookup.</summary>
		public IReadOnlyDictionary<string, StateMachineState> States { get; }

		public StateMachineData(string id,
			IValueProvider<string> currentState,
			string initial,
			IReadOnlyList<StateTransition> transitions,
			IReadOnlyDictionary<string, StateMachineState> states) : base(id)
		{
			CurrentState = currentState;
			Initial = initial;
			Transitions = transitions;
			States = states;
		}
	}
}
