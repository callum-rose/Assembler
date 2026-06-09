using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListAddRangeInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<List<T>> List,
		ValueSource<List<T>> Other) : BehaviourInfo(Id, Listeners)
	{
		public static ListAddRangeInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("List")),
				ValueSourceFactory.CreateValueSource<List<T>>(ctx, props.GetValueOrDefault("Other")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ListAddRangeInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(ctx),
				Other.SubstituteParameters(ctx));
	}
}
