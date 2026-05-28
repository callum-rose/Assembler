using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListClearInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IList<T>> List) : BehaviourInfo(Id, Listeners)
	{
		public static ListClearInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(ctx, props.GetValueOrDefault("List")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListClearInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx));
	}
}
