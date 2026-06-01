using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetTimeScaleInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<float> Scale)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetTimeScaleInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Scale"), fallback: 1f));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SetTimeScaleInfo(Id,
				substitutedListeners,
				Scale.SubstituteParameters(ctx));
	}
}
