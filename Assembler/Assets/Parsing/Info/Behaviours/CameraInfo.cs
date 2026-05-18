using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraInfo(
		string Id,
		IReadOnlyList<BehaviourDescriptor> Listeners,
		ValueSource<string> View,
		ValueSource<float> Size) : BehaviourInfo(Id, Listeners)
	{
		public static CameraInfo Create(string id,
			IReadOnlyList<BehaviourDescriptor> listeners,
			Dictionary<string, object>? props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, object>? p) =>
			new(id,
				listeners,
				Transformer.Wrap<string>(v, props?.GetValueOrDefault("View"), parameters: p),
				Transformer.Wrap<float>(v, props?.GetValueOrDefault("Size"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<BehaviourDescriptor> substitutedListeners,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new CameraInfo(Id,
				substitutedListeners,
				View.Substitute(parameters, allValues),
				Size.Substitute(parameters, allValues));
	}
}