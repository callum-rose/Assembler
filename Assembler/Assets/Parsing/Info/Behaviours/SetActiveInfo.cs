using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetActiveInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Active)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetActiveInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Active")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SetActiveInfo(Id,
				substitutedListeners,
				Active.SubstituteParameters(ctx));
	}
}
