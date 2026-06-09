using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record DebouncedTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Interval)
		: BehaviourInfo(Id, Listeners)
	{
		public static DebouncedTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Interval")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new DebouncedTriggerInfo(Id,
				substitutedListeners,
				Interval.SubstituteParameters(ctx));
	}
}
