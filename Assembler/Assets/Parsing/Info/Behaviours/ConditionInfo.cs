using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ConditionInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> ExpressionId,
		IReadOnlyList<IValueSourceArg> Arguments) : BehaviourInfo(Id, Listeners)
	{
		public static ConditionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props.GetValueOrDefault("ExpressionId"), parameters: p),
				Transformer.ConvertArgumentList(v, props.GetValueOrDefault("Arguments")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionInfo(Id,
				substitutedListeners,
				ExpressionId.SubstituteParameters(parameters, allValues),
				Arguments.Select(a => a.SubstituteParameters(parameters, allValues)).ToArray());
	}
}
