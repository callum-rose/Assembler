using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record LineGizmoInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Vector3> Start,
		ValueSource<Vector3> End,
		ValueSource<Color> Colour) : BehaviourInfo(Id, Listeners)
	{
		public static LineGizmoInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Start")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("End")),
				ValueSourceFactory.CreateValueSource<Color>(ctx, props.GetValueOrDefault("Colour")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new LineGizmoInfo(Id,
				substitutedListeners,
				Start.SubstituteParameters(ctx),
				End.SubstituteParameters(ctx),
				Colour.SubstituteParameters(ctx));
	}
}
