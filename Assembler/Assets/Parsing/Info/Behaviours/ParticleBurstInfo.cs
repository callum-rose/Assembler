using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ParticleBurstInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<int> Count,
		ValueSource<Vector3> Direction,
		ValueSource<float> Spread,
		ValueSource<float> Speed,
		ValueSource<float> SpeedVariation,
		ValueSource<Vector3> InheritVelocity,
		ValueSource<float> InheritFactor,
		ValueSource<float> Lifetime,
		ValueSource<Color> StartColour,
		ValueSource<Color> EndColour,
		ValueSource<float> StartSize,
		ValueSource<float> EndSize,
		ValueSource<float> Gravity,
		ValueSource<float> Drag,
		ValueSource<ParticleShape> Shape,
		ValueSource<bool> Collision) : BehaviourInfo(Id, Listeners)
	{
		public static ParticleBurstInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<int>(ctx, props.GetValueOrDefault("Count")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Direction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Spread")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Speed")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("SpeedVariation")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("InheritVelocity")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("InheritFactor")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Lifetime")),
				ValueSourceFactory.CreateOptionalValueSource<Color>(ctx, props.GetValueOrDefault("StartColour")),
				ValueSourceFactory.CreateOptionalValueSource<Color>(ctx, props.GetValueOrDefault("EndColour")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("StartSize")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("EndSize")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Gravity")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Drag")),
				ValueSourceFactory.CreateOptionalEnumSource<ParticleShape>(ctx, props.GetValueOrDefault("Shape")),
				ValueSourceFactory.CreateOptionalValueSource<bool>(ctx, props.GetValueOrDefault("Collision")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ParticleBurstInfo(Id,
				substitutedListeners,
				Count.SubstituteParameters(ctx),
				Direction.SubstituteParameters(ctx),
				Spread.SubstituteParameters(ctx),
				Speed.SubstituteParameters(ctx),
				SpeedVariation.SubstituteParameters(ctx),
				InheritVelocity.SubstituteParameters(ctx),
				InheritFactor.SubstituteParameters(ctx),
				Lifetime.SubstituteParameters(ctx),
				StartColour.SubstituteParameters(ctx),
				EndColour.SubstituteParameters(ctx),
				StartSize.SubstituteParameters(ctx),
				EndSize.SubstituteParameters(ctx),
				Gravity.SubstituteParameters(ctx),
				Drag.SubstituteParameters(ctx),
				Shape.SubstituteParameters(ctx),
				Collision.SubstituteParameters(ctx));
	}
}
