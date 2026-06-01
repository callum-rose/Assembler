using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ActivePollInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> Active)
		: BehaviourInfo(Id, Listeners)
	{
		public static ActivePollInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Active")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ActivePollInfo(Id,
				substitutedListeners,
				Active.SubstituteParameters(ctx));
	}
}
