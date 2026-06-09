using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VariableChangedTriggerInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<T> VariableId) : BehaviourInfo(Id, Listeners)
	{
		public static VariableChangedTriggerInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("VariableId")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new VariableChangedTriggerInfo<T>(Id,
				substitutedListeners,
				VariableId.SubstituteParameters(ctx));
	}
}
