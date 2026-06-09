using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>One ordered transition: when in <see cref="From"/> and <see cref="When"/> evaluates true,
	/// switch to <see cref="To"/>.</summary>
	public sealed record TransitionInfo(string From, string To, ValueSource<bool> When)
	{
		public TransitionInfo SubstituteParameters(TransformContext ctx) =>
			new(From, To, When.SubstituteParameters(ctx));
	}

	/// <summary>A named state, optionally with listener hooks fired on entry/exit.</summary>
	public sealed record StateInfo(
		string Name,
		IReadOnlyList<ListenerInfo> OnEnter,
		IReadOnlyList<ListenerInfo> OnExit);

	public record StateMachineInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		string StateVariable,
		string Initial,
		IReadOnlyList<StateInfo> States,
		IReadOnlyList<TransitionInfo> Transitions) : BehaviourInfo(Id, Listeners)
	{
		public static StateMachineInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			var stateVariable = RequireString(props.GetValueOrDefault("StateVariable"), id, "StateVariable");
			var initial = RequireString(props.GetValueOrDefault("Initial"), id, "Initial");
			var states = ParseStates(ctx, props.GetValueOrDefault("States"), id);

			if (states.Count == 0)
			{
				throw new ParsingException($"State machine '{id}': States must declare at least one state.");
			}

			var stateNames = states.Select(s => s.Name).ToHashSet();

			if (!stateNames.Contains(initial))
			{
				throw new ParsingException(
					$"State machine '{id}': Initial '{initial}' is not a declared state. Declared: {string.Join(", ", stateNames)}.");
			}

			var transitions = ParseTransitions(ctx, props.GetValueOrDefault("Transitions"), id);

			foreach (var t in transitions)
			{
				if (!stateNames.Contains(t.From))
				{
					throw new ParsingException(
						$"State machine '{id}': transition 'from' references unknown state '{t.From}'. Declared: {string.Join(", ", stateNames)}.");
				}

				if (!stateNames.Contains(t.To))
				{
					throw new ParsingException(
						$"State machine '{id}': transition 'to' references unknown state '{t.To}'. Declared: {string.Join(", ", stateNames)}.");
				}
			}

			return new StateMachineInfo(id, listeners, stateVariable, initial, states, transitions);
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new StateMachineInfo(Id,
				substitutedListeners,
				StateVariable,
				Initial,
				States.Select(s => new StateInfo(s.Name,
					TemplateInstantiator.SubstituteListeners(s.OnEnter, ctx.Parameters),
					TemplateInstantiator.SubstituteListeners(s.OnExit, ctx.Parameters))).ToArray(),
				Transitions.Select(t => t.SubstituteParameters(ctx)).ToArray());

		private static IReadOnlyList<StateInfo> ParseStates(TransformContext ctx, AssemblerValue? raw, string id)
		{
			if (raw is not DictValue dict)
			{
				throw new ParsingException(
					$"State machine '{id}': States must be a map of state name to optional OnEnter/OnExit hooks.");
			}

			return dict.Value.Select(kvp =>
			{
				var body = kvp.Value as DictValue;
				var onEnter = ListenerParsing.ParseNestedListeners(ctx, body?.Value.GetValueOrDefault("OnEnter"));
				var onExit = ListenerParsing.ParseNestedListeners(ctx, body?.Value.GetValueOrDefault("OnExit"));
				return new StateInfo(kvp.Key, onEnter, onExit);
			}).ToArray();
		}

		private static IReadOnlyList<TransitionInfo> ParseTransitions(TransformContext ctx, AssemblerValue? raw, string id)
		{
			if (raw is null)
			{
				return System.Array.Empty<TransitionInfo>();
			}

			if (raw is not ListValue list)
			{
				throw new ParsingException(
					$"State machine '{id}': Transitions must be a list of {{ from, to, when }} entries.");
			}

			return list.Value.Select(item =>
			{
				if (item is not DictValue d)
				{
					throw new ParsingException(
						$"State machine '{id}': each transition must be a map with 'from', 'to', and 'when'.");
				}

				var from = RequireString(d.Value.GetValueOrDefault("from"), id, "transition 'from'");
				var to = RequireString(d.Value.GetValueOrDefault("to"), id, "transition 'to'");

				var rawWhen = d.Value.GetValueOrDefault("when");
				if (rawWhen is null or NoValue)
				{
					throw new ParsingException(
						$"State machine '{id}': transition '{from}' -> '{to}' is missing 'when'. Use `when: true` for an unconditional transition.");
				}

				return new TransitionInfo(from, to, ValueSourceFactory.CreateValueSource<bool>(ctx, rawWhen));
			}).ToArray();
		}

		private static string RequireString(AssemblerValue? value, string id, string field) =>
			value is StringValue s
				? s.Value
				: throw new ParsingException($"State machine '{id}': {field} is required and must be a string.");
	}
}
