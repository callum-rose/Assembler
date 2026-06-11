using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	public record SetBehaviourEnabledInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<ListenerInfo> Targets,
		ValueSource<bool> Enabled) : BehaviourInfo(Id, Listeners)
	{
		public static SetBehaviourEnabledInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ListenerParsing.ParseTargets(ctx, props.GetValueOrDefault("Targets"), id),
				ValueSourceFactory.CreateValueSource<bool>(ctx, props.GetValueOrDefault("Enabled")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new SetBehaviourEnabledInfo(Id,
				substitutedListeners,
				TemplateInstantiator.SubstituteListeners(Targets, ctx.Parameters),
				Enabled.SubstituteParameters(ctx));
	}
}
