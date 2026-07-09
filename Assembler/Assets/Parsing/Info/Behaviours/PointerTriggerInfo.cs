using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record PointerTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<PointerPhase> Phase,
		ValueSource<Vector3> PlanePoint,
		ValueSource<Vector3> PlaneNormal) : BehaviourInfo(Id, Listeners)
	{
		public static PointerTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("Phase"), PointerPhase.Press),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("PlanePoint")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("PlaneNormal")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PointerTriggerInfo(Id,
				substitutedListeners,
				Phase.SubstituteParameters(ctx),
				PlanePoint.SubstituteParameters(ctx),
				PlaneNormal.SubstituteParameters(ctx));
	}
}
