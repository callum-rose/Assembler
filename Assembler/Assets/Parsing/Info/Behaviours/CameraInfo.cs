using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> View,
		ValueSource<float> Size) : BehaviourInfo(Id, Listeners)
	{
		public static CameraInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			Dictionary<string, AssemblerValue>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue>? p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(v, props?.GetValueOrDefault("View"), parameters: p),
				Transformer.CreateValueSource<float>(v, props?.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CameraInfo(Id,
				substitutedListeners,
				View.SubstituteParameters(parameters, allValues),
				Size.SubstituteParameters(parameters, allValues));
	}
}