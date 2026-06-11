using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record ToggleBehaviourEnabledInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<ListenerInfo> Targets) : BehaviourInfo(Id, Listeners)
	{
		public static ToggleBehaviourEnabledInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ListenerParsing.ParseTargets(ctx, props.GetValueOrDefault("Targets"), id));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new ToggleBehaviourEnabledInfo(Id,
				substitutedListeners,
				TemplateInstantiator.SubstituteListeners(Targets, ctx.Parameters));
	}
}
