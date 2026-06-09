using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListRemoveInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List,
		ValueSource<T> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListRemoveInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")),
				ValueSourceFactory.CreateValueSource<T>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListRemoveInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Value.SubstituteParameters(ctx));
	}
}
