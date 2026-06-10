using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record LightInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<LightKind> Type,
		ValueSource<Color> Colour,
		ValueSource<float> Intensity,
		ValueSource<float> Range,
		ValueSource<float> SpotAngle)
		: BehaviourInfo(Id, Listeners)
	{
		public static LightInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateEnumSource(ctx, props.GetValueOrDefault("Type"), LightKind.Directional),
				ValueSourceFactory.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Intensity")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("Range")),
				ValueSourceFactory.CreateValueSource<float>(ctx, props.GetValueOrDefault("SpotAngle")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new LightInfo(Id,
				substitutedListeners,
				Type.SubstituteParameters(ctx),
				Colour.SubstituteParameters(ctx),
				Intensity.SubstituteParameters(ctx),
				Range.SubstituteParameters(ctx),
				SpotAngle.SubstituteParameters(ctx));
	}
}
