using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record KeyDownTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Key)
		: BehaviourInfo(Id, Listeners)
	{
		public static KeyDownTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Key")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new KeyDownTriggerInfo(Id,
				substitutedListeners,
				Key.SubstituteParameters(ctx));
	}
}
