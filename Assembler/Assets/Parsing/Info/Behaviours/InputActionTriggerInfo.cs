using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// Parse-layer info for the <c>input action</c> trigger: an entity-hosted trigger driven by a named abstract
	/// action (declared in the descriptor's <c>Controls</c> section) instead of a raw key. The action's kind and
	/// phase, and the live Unity <c>InputAction</c>, are resolved at build time from the controls — this record
	/// only carries the action name, so the parsing assembly needs no Input System reference.
	/// </summary>
	public record InputActionTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Action)
		: BehaviourInfo(Id, Listeners)
	{
		public static InputActionTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Action")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new InputActionTriggerInfo(Id,
				substitutedListeners,
				Action.SubstituteParameters(ctx));
	}
}
