using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// The perception sensor's parsed configuration. <see cref="Tag"/>/<see cref="Radius"/> are required; the
	/// cone (<see cref="Forward"/> + <see cref="ConeAngle"/>) and line-of-sight gating are optional. The four
	/// output properties name the blackboard variables this sensor maintains (plain variable names, resolved to
	/// the entity's writable variables at build time); an empty name means "don't write that output".
	/// </summary>
	public record PerceiveInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> Tag,
		ValueSource<float> Radius,
		ValueSource<float> ConeAngle,
		ValueSource<Vector3> Forward,
		ValueSource<bool> RequireLineOfSight,
		ValueSource<string> Obstacles,
		ValueSource<float> Interval,
		string TargetIdVar,
		string TargetPositionVar,
		string HasTargetVar,
		string LastKnownPositionVar) : BehaviourInfo(Id, Listeners)
	{
		public static PerceiveInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx)
		{
			if (!props.ContainsKey("Tag"))
			{
				throw new ParsingException($"perceive '{id}': 'Tag' is required (the entity tag to look for).");
			}

			if (!props.ContainsKey("Radius"))
			{
				throw new ParsingException($"perceive '{id}': 'Radius' is required (the detection range).");
			}

			return new PerceiveInfo(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Tag")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Radius")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("ConeAngle")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("Forward")),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("RequireLineOfSight"), false),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("Obstacles"), string.Empty),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Interval"), 0f),
				OptionalName(props.GetValueOrDefault("TargetIdVar")),
				OptionalName(props.GetValueOrDefault("TargetPositionVar")),
				OptionalName(props.GetValueOrDefault("HasTargetVar")),
				OptionalName(props.GetValueOrDefault("LastKnownPositionVar")));
		}

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new PerceiveInfo(Id,
				substitutedListeners,
				Tag.SubstituteParameters(ctx),
				Radius.SubstituteParameters(ctx),
				ConeAngle.SubstituteParameters(ctx),
				Forward.SubstituteParameters(ctx),
				RequireLineOfSight.SubstituteParameters(ctx),
				Obstacles.SubstituteParameters(ctx),
				Interval.SubstituteParameters(ctx),
				TargetIdVar,
				TargetPositionVar,
				HasTargetVar,
				LastKnownPositionVar);

		private static string OptionalName(AssemblerValue? value) =>
			value is StringValue s ? s.Value : string.Empty;
	}
}
