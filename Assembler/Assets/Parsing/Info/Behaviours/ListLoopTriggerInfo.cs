using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListLoopTriggerInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List) : BehaviourInfo(Id, Listeners)
	{
		public static ListLoopTriggerInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListLoopTriggerInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx));
	}
}
