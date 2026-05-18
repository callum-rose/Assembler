using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VariableSetterInfo<T>(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<T> ValueToSet,
		ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners)
	{
		public static VariableSetterInfo<T> Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<T>(v, props?.GetValueOrDefault("VariableId"), parameters: p),
				Transformer.Wrap<T>(v, props?.GetValueOrDefault("Value"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VariableSetterInfo<T>(Id,
				substitutedListeners,
				ValueToSet.Substitute(parameters, allValues),
				ValueToGet.Substitute(parameters, allValues));
	}
}