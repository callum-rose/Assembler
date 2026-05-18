using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ConditionInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> ExpressionId,
		IReadOnlyList<ValueSource<object>> Arguments) : BehaviourInfo(Id, Listeners)
	{
		public static ConditionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("ExpressionId"), parameters: p),
				Transformer.ConvertArgumentList(v, props?.GetValueOrDefault("Arguments")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new ConditionInfo(Id,
				substitutedListeners,
				ExpressionId.Substitute(parameters, allValues),
				Arguments.Select(a => TemplateInstantiator.Substitute<object>(a, parameters, allValues)).ToArray());
	}
}