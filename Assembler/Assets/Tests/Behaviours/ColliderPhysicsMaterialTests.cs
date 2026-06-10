using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Physics;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class ColliderPhysicsMaterialTests
	{
		[Test]
		public void BoxCollider_WithMaterialSet_CreatesAndAssignsPhysicsMaterial()
		{
			var go = new GameObject("box host");
			try
			{
				var behaviour = go.AddComponent<AutoAddBoxColliderBehaviour>();
				behaviour.Initialise(
					new BoxColliderData("c",
						NullValueProvider<Vector3>.Instance,
						NullValueProvider<bool>.Instance)
					{
						Material = new PhysicsMaterialProviders
						{
							Bounciness = new ValueProvider<float>(0.8f),
							DynamicFriction = new ValueProvider<float>(0.3f),
							StaticFriction = new ValueProvider<float>(0.4f)
						}
					},
					Array.Empty<Listener>());

				var collider = go.GetComponent<BoxCollider>();
				Assert.IsNotNull(collider.sharedMaterial, "A PhysicsMaterial should be assigned when properties are set.");
				Assert.AreEqual(0.8f, collider.sharedMaterial.bounciness, 1e-4f);
				Assert.AreEqual(0.3f, collider.sharedMaterial.dynamicFriction, 1e-4f);
				Assert.AreEqual(0.4f, collider.sharedMaterial.staticFriction, 1e-4f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void BoxCollider_WithNoMaterialProperties_LeavesDefaultMaterial()
		{
			var go = new GameObject("box host");
			try
			{
				var behaviour = go.AddComponent<AutoAddBoxColliderBehaviour>();
				behaviour.Initialise(
					new BoxColliderData("c",
						NullValueProvider<Vector3>.Instance,
						NullValueProvider<bool>.Instance),
					Array.Empty<Listener>());

				Assert.IsNull(go.GetComponent<BoxCollider>().sharedMaterial,
					"No PhysicsMaterial should be allocated when no material property is set.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void SphereCollider_WithOnlyBounciness_AssignsMaterialAndKeepsFrictionDefaults()
		{
			var go = new GameObject("sphere host");
			try
			{
				var behaviour = go.AddComponent<AutoAddSphereColliderBehaviour>();
				behaviour.Initialise(
					new SphereColliderData("c", NullValueProvider<float>.Instance)
					{
						Material = new PhysicsMaterialProviders { Bounciness = new ValueProvider<float>(1f) }
					},
					Array.Empty<Listener>());

				var collider = go.GetComponent<SphereCollider>();
				Assert.IsNotNull(collider.sharedMaterial);
				Assert.AreEqual(1f, collider.sharedMaterial.bounciness, 1e-4f);
				// Unset friction stays at Unity's PhysicsMaterial default (0.6).
				Assert.AreEqual(0.6f, collider.sharedMaterial.dynamicFriction, 1e-4f);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
