using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListAddInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IList<T>> List,
		ValueSource<T> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListAddInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(ctx, props.GetValueOrDefault("List")),
				Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListAddInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Value.SubstituteParameters(ctx));
	}
}
