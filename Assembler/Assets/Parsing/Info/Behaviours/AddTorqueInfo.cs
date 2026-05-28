using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record AddTorqueInfo(string Id, IReadOnlyList<ListenerInfo> Listeners, ValueSource<Vector3> Torque)
		: BehaviourInfo(Id, Listeners)
	{
		public static AddTorqueInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				Transformer.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Torque")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new AddTorqueInfo(Id,
				substitutedListeners,
				Torque.SubstituteParameters(ctx));
	}
}
