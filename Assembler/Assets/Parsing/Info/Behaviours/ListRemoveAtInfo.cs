using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListRemoveAtInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IList<T>> List,
		ValueSource<int> Index) : BehaviourInfo(Id, Listeners)
	{
		public static ListRemoveAtInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(ctx, props.GetValueOrDefault("List")),
				Transformer.CreateValueSource<int>(ctx, props.GetValueOrDefault("Index")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListRemoveAtInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Index.SubstituteParameters(ctx));
	}
}
