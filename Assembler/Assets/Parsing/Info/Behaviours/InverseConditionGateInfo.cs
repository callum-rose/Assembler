using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record InverseConditionGateInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static InverseConditionGateInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Condition")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new InverseConditionGateInfo(Id,
				substitutedListeners,
				Condition.SubstituteParameters(ctx));
	}
}
