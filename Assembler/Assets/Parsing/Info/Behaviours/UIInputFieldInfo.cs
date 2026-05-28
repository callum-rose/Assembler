using System.Collections.Generic;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UIInputFieldInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ScreenRect Rect) : BehaviourInfo(Id, Listeners)
	{
		public static UIInputFieldInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIInputFieldInfo(Id, substitutedListeners, Rect);
	}
}
