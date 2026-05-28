using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record MouseButtonUpTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<int> Button)
		: BehaviourInfo(Id, Listeners)
	{
		public static MouseButtonUpTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Button")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new MouseButtonUpTriggerInfo(Id,
				substitutedListeners,
				Button.SubstituteParameters(ctx));
	}
}
