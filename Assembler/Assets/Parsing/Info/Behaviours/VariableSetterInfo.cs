using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VariableSetterInfo<T>(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		[property: YamlName("VariableId")] ValueSource<T> ValueToSet,
		[property: YamlName("Value")] ValueSource<T> ValueToGet) : BehaviourInfo(Id, Listeners)
	{
		public static VariableSetterInfo<T> Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("VariableId")),
				Transformer.CreateValueSource<T>(ctx, props.GetValueOrDefault("Value")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new VariableSetterInfo<T>(Id,
				substitutedListeners,
				ValueToSet.SubstituteParameters(ctx),
				ValueToGet.SubstituteParameters(ctx));
	}
}
