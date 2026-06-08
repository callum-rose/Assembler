using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyHoldTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyHoldTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Key")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new KeyHoldTriggerInfo(Id,
				substitutedListeners,
				Key.SubstituteParameters(ctx));
	}
}
