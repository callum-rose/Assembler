using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record CameraInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> View,
		ValueSource<float> Size) : BehaviourInfo(Id, Listeners)
	{
		public static CameraInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<string>(ctx, props.GetValueOrDefault("View")),
				Transformer.CreateValueSource<float>(ctx, props.GetValueOrDefault("Size")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CameraInfo(Id,
				substitutedListeners,
				View.SubstituteParameters(ctx),
				Size.SubstituteParameters(ctx));
	}
}