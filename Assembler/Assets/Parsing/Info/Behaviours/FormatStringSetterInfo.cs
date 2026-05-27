using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info.Behaviours
{
	public record FormatStringSetterInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		[property: YamlName("VariableId")] ValueSource<string> ValueToSet,
		ValueSource<string> Format,
		IReadOnlyList<ValueSource<object>> Arguments) : BehaviourInfo(Id, Listeners)
	{
		public static FormatStringSetterInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props.GetValueOrDefault("VariableId"), parameters: p),
				Transformer.CreateValueSource<string>(v, props.GetValueOrDefault("Format"), parameters: p),
				Transformer.ConvertArgumentList(v, props.GetValueOrDefault("Arguments")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new FormatStringSetterInfo(Id,
				substitutedListeners,
				ValueToSet.SubstituteParameters(parameters, allValues),
				Format.SubstituteParameters(parameters, allValues),
				Arguments.Select(a => TemplateInstantiator.SubstituteParameters<object>(a, parameters, allValues)).ToArray());
	}
}
