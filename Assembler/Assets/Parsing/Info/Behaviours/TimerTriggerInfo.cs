using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record TimerTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Delay, ValueSource<bool> AutoStart)
		: BehaviourInfo(Id, Listeners)
	{
		public static TimerTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Delay")),
				// AutoStart defaults to true: an omitted AutoStart self-arms the countdown on entity
				// start (the common "self-destruct after N seconds" case). Set AutoStart: false to make
				// it wait for an upstream Execute instead.
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("AutoStart"), true));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new TimerTriggerInfo(Id,
				substitutedListeners,
				Delay.SubstituteParameters(ctx),
				AutoStart.SubstituteParameters(ctx));
	}
}
