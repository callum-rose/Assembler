using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class PrimitiveColliderTests
	{
		// Collision in Assembler is opt-in via the explicit collider behaviours, so a primitive must stay
		// purely visual — otherwise every mesh silently joins physics (e.g. a floating rigidbody grinding on
		// a "ground" mesh, which pinned the Mini Racer car's yaw). The project's own meshes carry no collider
		// to begin with, unlike GameObject.CreatePrimitive's; this guards that it stays that way.
		[Test]
		public void Primitive_LeavesNoColliderButKeepsTheMesh()
		{
			var go = new GameObject("primitive host");
			try
			{
				var primitive = go.AddComponent<Primitive>();
				primitive.Initialise(
					new PrimitiveData("p",
						new ValueProvider<ShapeKind>(ShapeKind.Cube),
						NullValueProvider<Color>.Instance,
						NullValueProvider<Vector3>.Instance),
					Array.Empty<Listener>());

				Assert.IsNull(go.GetComponentInChildren<Collider>(),
					"Primitive should add no collider — primitives are visual only.");
				Assert.IsNotNull(go.GetComponentInChildren<MeshRenderer>(),
					"Primitive should still create the visual mesh.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
