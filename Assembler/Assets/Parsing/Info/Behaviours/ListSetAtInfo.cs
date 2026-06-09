using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListSetAtInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List,
		ValueSource<int> Index,
		ValueSource<T> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListSetAtInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")),
				ValueSourceFactory.CreateValueSource<int>(ctx, props.GetValueOrDefault("Index")),
				ValueSourceFactory.CreateValueSource<T>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListSetAtInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Index.SubstituteParameters(ctx),
				Value.SubstituteParameters(ctx));
	}
}
