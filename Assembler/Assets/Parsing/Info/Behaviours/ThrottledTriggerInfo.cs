using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ThrottledTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Rate)
		: BehaviourInfo(Id, Listeners)
	{
		public static ThrottledTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Rate")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ThrottledTriggerInfo(Id,
				substitutedListeners,
				Rate.SubstituteParameters(ctx));
	}
}
