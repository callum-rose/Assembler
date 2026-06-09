using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record UIContainerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<LayoutDirection> Direction,
		ValueSource<float> Spacing,
		ValueSource<float> Padding,
		ValueSource<TextAnchor> ChildAlignment,
		ValueSource<bool> FitContent) : BehaviourInfo(Id, Listeners)
	{
		public static UIContainerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalEnumSource<LayoutDirection>(ctx, props.GetValueOrDefault("Direction")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Spacing")),
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("Padding")),
				ValueSourceFactory.CreateOptionalEnumSource<TextAnchor>(ctx, props.GetValueOrDefault("ChildAlignment")),
				ValueSourceFactory.CreateOptionalValueSource<bool>(ctx, props.GetValueOrDefault("FitContent")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UIContainerInfo(Id,
				substitutedListeners,
				Direction.SubstituteParameters(ctx),
				Spacing.SubstituteParameters(ctx),
				Padding.SubstituteParameters(ctx),
				ChildAlignment.SubstituteParameters(ctx),
				FitContent.SubstituteParameters(ctx));
	}
}
