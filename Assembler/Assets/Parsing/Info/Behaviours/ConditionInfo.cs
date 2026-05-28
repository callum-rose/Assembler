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
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("ExpressionId")),
				Transformer.ConvertArgumentList(ctx, props.GetValueOrDefault("Arguments")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ConditionInfo(Id,
				substitutedListeners,
				ExpressionId.SubstituteParameters(ctx),
				Arguments.Select(a => a.SubstituteParameters(ctx)).ToArray());
	}
}
