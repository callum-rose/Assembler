using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetPositionInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		[property: YamlName("Position")] ValueSource<Vector3> ValueExpression)
		: BehaviourInfo(Id, Listeners)
	{
		public static SetPositionInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Position")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SetPositionInfo(Id,
				substitutedListeners,
				ValueExpression.SubstituteParameters(ctx));
	}
}
