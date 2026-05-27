using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListFillInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IList<T>> List,
		ValueSource<int> Count,
		ValueSource<T> Value) : BehaviourInfo(Id, Listeners)
	{
		public static ListFillInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(v, props.GetValueOrDefault("List"), parameters: p),
				Transformer.CreateValueSource<int>(v, props.GetValueOrDefault("Count"), parameters: p),
				Transformer.CreateValueSource<T>(v, props.GetValueOrDefault("Value"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ListFillInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(parameters, allValues),
				Count.SubstituteParameters(parameters, allValues),
				Value.SubstituteParameters(parameters, allValues));
	}
}
