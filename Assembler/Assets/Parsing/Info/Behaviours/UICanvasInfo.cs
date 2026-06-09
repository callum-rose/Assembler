using System.Collections.Generic;
using UnityEngine;


namespace Assembler.Parsing.Info.Behaviours
{
	public record UICanvasInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<float> MatchWidthOrHeight,
		ValueSource<Vector3> ReferenceResolution) : BehaviourInfo(Id, Listeners)
	{
		public static UICanvasInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateOptionalValueSource<float>(ctx, props.GetValueOrDefault("MatchWidthOrHeight")),
				ValueSourceFactory.CreateOptionalValueSource<Vector3>(ctx, props.GetValueOrDefault("ReferenceResolution")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new UICanvasInfo(Id,
				substitutedListeners,
				MatchWidthOrHeight.SubstituteParameters(ctx),
				ReferenceResolution.SubstituteParameters(ctx));
	}
}
