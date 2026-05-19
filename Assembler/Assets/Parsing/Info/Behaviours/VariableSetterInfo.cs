using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VariableSetterInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<T> ValueToSet,
		ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners)
	{
		public static VariableSetterInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<T>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
				Transformer.CreateValueSource<T>(v, props?.GetValueOrDefault("Value"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VariableSetterInfo<T>(Id,
				substitutedListeners,
				ValueToSet.SubstituteParameters(parameters, allValues),
				ValueToGet.SubstituteParameters(parameters, allValues));
	}
}