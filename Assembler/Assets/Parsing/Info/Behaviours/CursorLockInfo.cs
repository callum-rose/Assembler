using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CursorLockInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<bool> Locked,
		ValueSource<bool> Visible) : BehaviourInfo(Id, Listeners)
	{
		public static CursorLockInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<bool>(ctx, props.GetValueOrDefault("Locked")),
				ValueSourceFactory.CreateOptionalValueSource<bool>(ctx, props.GetValueOrDefault("Visible")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CursorLockInfo(Id,
				substitutedListeners,
				Locked.SubstituteParameters(ctx),
				Visible.SubstituteParameters(ctx));
	}
}
