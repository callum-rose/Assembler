using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VariableAdjustInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		[property: YamlName("VariableId")] ValueSource<T> ValueToSet,
		[property: YamlName("Delta")] ValueSource<T> Delta) : BehaviourInfo(Id, Listeners)
	{
		public static VariableAdjustInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<T>(v, props.GetValueOrDefault("VariableId"), parameters: p),
				Transformer.CreateValueSource<T>(v, props.GetValueOrDefault("Delta"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new VariableAdjustInfo<T>(Id,
				substitutedListeners,
				ValueToSet.SubstituteParameters(parameters, allValues),
				Delta.SubstituteParameters(parameters, allValues));
	}
}
