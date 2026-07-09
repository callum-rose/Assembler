using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ScreenToWorldInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> ScreenPosition,
		ValueSource<Vector3> PlanePoint,
		ValueSource<Vector3> PlaneNormal) : BehaviourInfo(Id, Listeners)
	{
		public static ScreenToWorldInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("ScreenPosition")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("PlanePoint")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("PlaneNormal")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ScreenToWorldInfo(Id,
				substitutedListeners,
				ScreenPosition.SubstituteParameters(ctx),
				PlanePoint.SubstituteParameters(ctx),
				PlaneNormal.SubstituteParameters(ctx));
	}
}
