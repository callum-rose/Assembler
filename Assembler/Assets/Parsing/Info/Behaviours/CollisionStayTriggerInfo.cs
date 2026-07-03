using System.Collections.Generic;

namespace Assembler.Parsing.Info.Behaviours
{
	[TriggerOutputs("contact_point", "contact_normal", "other_velocity", "other_position")]
	public record CollisionStayTriggerInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		IReadOnlyList<string> TagsToDetect) : BehaviourInfo(Id, Listeners)
	{
		public static CollisionStayTriggerInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.ConvertStringList(props.GetValueOrDefault("TagsToDetect")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new CollisionStayTriggerInfo(Id, substitutedListeners, TagsToDetect);
	}
}
