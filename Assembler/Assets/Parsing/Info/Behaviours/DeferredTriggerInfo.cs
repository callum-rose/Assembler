using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DeferredTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Delay)
		: BehaviourInfo(Id, Listeners)
	{
		public static DeferredTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Delay")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new DeferredTriggerInfo(Id,
				substitutedListeners,
				Delay.SubstituteParameters(ctx));
	}
}
