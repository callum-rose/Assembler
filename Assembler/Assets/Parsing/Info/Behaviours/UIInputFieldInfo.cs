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
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				ScreenRectParser.Parse(props.GetValueOrDefault("Rect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new UIInputFieldInfo(Id, substitutedListeners, Rect);
	}
}
