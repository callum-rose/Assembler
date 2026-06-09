using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ExclusiveTriggerInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<string> Group)
		: BehaviourInfo(Id, Listeners)
	{
		public static ExclusiveTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Group")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ExclusiveTriggerInfo(Id,
				substitutedListeners,
				Group.SubstituteParameters(ctx));
	}
}
