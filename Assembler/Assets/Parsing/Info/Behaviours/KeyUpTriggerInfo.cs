using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyUpTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyUpTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("Key")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new KeyUpTriggerInfo(Id,
				substitutedListeners,
				Key.SubstituteParameters(ctx));
	}
}