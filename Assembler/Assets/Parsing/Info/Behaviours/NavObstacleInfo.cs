using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record NavObstacleInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Blocked)
		: BehaviourInfo(Id, Listeners)
	{
		public static NavObstacleInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<bool>(ctx, props.GetValueOrDefault("Blocked")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new NavObstacleInfo(Id,
				substitutedListeners,
				Blocked.SubstituteParameters(ctx));
	}
}
