using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class PrimitiveColliderTests
	{
		// GameObject.CreatePrimitive bundles a default collider onto every primitive. Collision in Assembler
		// is opt-in via the explicit collider behaviours, so a primitive must stay purely visual — otherwise
		// every mesh silently joins physics (e.g. a floating rigidbody grinding on a "ground" mesh, which
		// pinned the Mini Racer car's yaw). This guards that the auto-collider is stripped.
		[Test]
		public void Primitive_LeavesNoColliderButKeepsTheMesh()
		{
			var go = new GameObject("primitive host");
			try
			{
				var primitive = go.AddComponent<Primitive>();
				primitive.Initialise(
					new PrimitiveData("p",
						new ValueProvider<string>("cube"),
						NullValueProvider<Color>.Instance,
						NullValueProvider<Vector3>.Instance),
					Array.Empty<Listener>());

				Assert.IsNull(go.GetComponentInChildren<Collider>(),
					"Primitive should strip the collider CreatePrimitive adds — primitives are visual only.");
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
