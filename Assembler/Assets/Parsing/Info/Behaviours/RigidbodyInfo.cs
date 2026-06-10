using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record RigidbodyInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<bool> UseGravity,
		ValueSource<bool> IsKinematic,
		ValueSource<float> Mass,
		ValueSource<float> LinearDamping,
		ValueSource<float> AngularDamping,
		ValueSource<Vector3> FreezePosition,
		ValueSource<Vector3> FreezeRotation,
		ValueSource<Vector3> CentreOfMass)
		: BehaviourInfo(Id, Listeners)
	{
		public static RigidbodyInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("UseGravity")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("IsKinematic")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Mass")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("LinearDamping")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("AngularDamping")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("FreezePosition")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("FreezeRotation")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("CentreOfMass")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new RigidbodyInfo(Id,
				substitutedListeners,
				UseGravity.SubstituteParameters(ctx),
				IsKinematic.SubstituteParameters(ctx),
				Mass.SubstituteParameters(ctx),
				LinearDamping.SubstituteParameters(ctx),
				AngularDamping.SubstituteParameters(ctx),
				FreezePosition.SubstituteParameters(ctx),
				FreezeRotation.SubstituteParameters(ctx),
				CentreOfMass.SubstituteParameters(ctx));
	}
}
