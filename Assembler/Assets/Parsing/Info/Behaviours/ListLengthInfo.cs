using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ListLengthInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<IList<T>> List,
		ValueSource<int> Length) : BehaviourInfo(Id, Listeners)
	{
		public static ListLengthInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(v, props.GetValueOrDefault("List"), parameters: p),
				Transformer.CreateValueSource<int>(v, props.GetValueOrDefault("Length"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ListLengthInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(parameters, allValues),
				Length.SubstituteParameters(parameters, allValues));
	}
}
