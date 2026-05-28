using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MouseButtonTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<int> Button,
		ValueSource<string> Phase)
		: BehaviourInfo(Id, Listeners)
	{
		public static MouseButtonTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Button")),
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Phase")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MouseButtonTriggerInfo(Id,
				substitutedListeners,
				Button.SubstituteParameters(ctx),
				Phase.SubstituteParameters(ctx));
	}
}
