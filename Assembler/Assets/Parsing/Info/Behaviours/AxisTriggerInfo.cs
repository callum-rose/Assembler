using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AxisTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<string> XAxis,
		ValueSource<string> YAxis)
		: BehaviourInfo(Id, Listeners)
	{
		public static AxisTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("XAxis")),
				ValueSourceFactory.CreateValueSource<string>(ctx, props.GetValueOrDefault("YAxis")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AxisTriggerInfo(Id,
				substitutedListeners,
				XAxis.SubstituteParameters(ctx),
				YAxis.SubstituteParameters(ctx));
	}
}
