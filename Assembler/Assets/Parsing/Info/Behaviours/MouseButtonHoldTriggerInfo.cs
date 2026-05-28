using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MouseButtonHoldTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<int> Button)
		: BehaviourInfo(Id, Listeners)
	{
		public static MouseButtonHoldTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Button")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MouseButtonHoldTriggerInfo(Id,
				substitutedListeners,
				Button.SubstituteParameters(ctx));
	}
}
