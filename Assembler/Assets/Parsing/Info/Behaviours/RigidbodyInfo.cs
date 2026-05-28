using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RigidbodyInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<bool> UseGravity)
		: BehaviourInfo(Id, Listeners)
	{
		public static RigidbodyInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<bool>(ctx, props.GetValueOrDefault("UseGravity")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new RigidbodyInfo(Id,
				substitutedListeners,
				UseGravity.SubstituteParameters(ctx));
	}
}