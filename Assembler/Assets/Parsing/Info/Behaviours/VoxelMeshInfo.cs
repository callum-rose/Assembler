using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	public record VoxelMeshInfo(
		string Id,
		IReadOnlyList<ListenerInfo> Listeners,
		ValueSource<Mesh> Mesh,
		ValueSource<Vector3> Scale)
		: BehaviourInfo(Id, Listeners)
	{
		public static VoxelMeshInfo Create(string id,
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> props,
			TransformContext ctx) =>
			new(id,
				listeners,
				ValueSourceFactory.CreateValueSource<Mesh>(ctx, props.GetValueOrDefault("Mesh")),
				ValueSourceFactory.CreateValueSource<Vector3>(ctx, props.GetValueOrDefault("Scale")));

		public override BehaviourInfo SubstituteParameters(IReadOnlyList<ListenerInfo> substitutedListeners,
			TransformContext ctx) =>
			new VoxelMeshInfo(Id,
				substitutedListeners,
				Mesh.SubstituteParameters(ctx),
				Scale.SubstituteParameters(ctx));
	}
}
