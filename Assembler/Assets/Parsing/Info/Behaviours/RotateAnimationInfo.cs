using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RotateAnimationInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<float> Duration,
		ValueSource<string> Easing) : BehaviourInfo(Id, Listeners)
	{
		public static RotateAnimationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			IReadOnlyList<ValueInfo> v,
			IReadOnlyDictionary<string, AssemblerValue> p) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("Start"), parameters: p),
				Transformer.CreateValueSource<Vector3>(v, props.GetValueOrDefault("End"), parameters: p),
				Transformer.CreateValueSource<float>(v, props.GetValueOrDefault("Duration"), parameters: p),
				Transformer.CreateValueSource<string>(v, props.GetValueOrDefault("Easing"), parameters: p));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			new RotateAnimationInfo(Id,
				substitutedListeners,
				Start.SubstituteParameters(parameters, allValues),
				End.SubstituteParameters(parameters, allValues),
				Duration.SubstituteParameters(parameters, allValues),
				Easing.SubstituteParameters(parameters, allValues));
	}
}
