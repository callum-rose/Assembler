using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record GamepadButtonTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Button,
		ValueSource<string> Mode)
		: BehaviourInfo(Id, Listeners)
	{
		public static GamepadButtonTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Button")),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Mode")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new GamepadButtonTriggerInfo(Id,
				substitutedListeners,
				Button.SubstituteParameters(ctx),
				Mode.SubstituteParameters(ctx));
	}
}
