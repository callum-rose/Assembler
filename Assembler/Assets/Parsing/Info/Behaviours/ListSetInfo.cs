using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListSetInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List,
		ValueSource<List<T>> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListSetInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")),
				Transformer.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListSetInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Value.SubstituteParameters(ctx));
	}
}
