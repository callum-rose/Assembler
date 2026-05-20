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
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<IList<T>>(v, props.GetValueOrDefault("List"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ListClearInfo<T>(Id,
				substitutedListeners,
				List.SubstituteParameters(parameters, allValues));
	}
}
