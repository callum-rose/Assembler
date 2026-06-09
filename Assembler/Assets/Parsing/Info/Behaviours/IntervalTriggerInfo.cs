using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record IntervalTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> Interval,
		ValueSource<int> Count,
		ValueSource<bool> AutoStart)
		: BehaviourInfo(Id, Listeners)
	{
		public static IntervalTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Interval")),
				ValueSourceFactory.CreateValueSource<int>(ctx, props.GetValueOrDefault("Count")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("AutoStart")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new IntervalTriggerInfo(Id,
				substitutedListeners,
				Interval.SubstituteParameters(ctx),
				Count.SubstituteParameters(ctx),
				AutoStart.SubstituteParameters(ctx));
	}
}
