using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record BranchInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Condition)
		: BehaviourInfo(Id, Listeners)
	{
		public static BranchInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Condition")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new BranchInfo(Id,
				substitutedListeners,
				Condition.SubstituteParameters(ctx));
	}
}
