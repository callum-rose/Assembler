using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetRotationInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		[property: YamlName("Rotation")] ValueSource<Vector3> ValueExpression)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetRotationInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Rotation")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SetRotationInfo(Id,
				substitutedListeners,
				ValueExpression.SubstituteParameters(ctx));
	}
}
