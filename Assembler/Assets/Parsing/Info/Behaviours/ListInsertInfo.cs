using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListInsertInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List,
		ValueSource<int> Index,
		ValueSource<T> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListInsertInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")),
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Index")),
				Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListInsertInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Index.SubstituteParameters(ctx),
				Value.SubstituteParameters(ctx));
	}
}
