using System;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Covers the <c>part colliders</c> behaviour: one collider per visual part, on the part's own
	/// GameObject, shape-matched to the primitive that built it.
	/// </summary>
	/// <remarks>
	/// EditMode on purpose (Tests.Behaviours is Editor-only). <c>Model</c> strips the collider
	/// <c>GameObject.CreatePrimitive</c> bundles onto each part, and in play mode that <c>Destroy</c> defers
	/// to end of frame — so a play-mode "exactly one collider per part" assertion could see two. EditMode
	/// takes the <c>DestroyImmediate</c> branch and is clean.
	/// </remarks>
	public class PartCollidersTests : BehaviourTestFixture
	{
		// ------------------------------------------------------------------
		// Shape matching — the reason this is its own behaviour.
		// ------------------------------------------------------------------

		[Test]
		public void EachPartGetsAColliderMatchingTheShapeThatBuiltIt()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube),
				Part(PrimitiveType.Sphere),
				Part(PrimitiveType.Capsule),
				Part(PrimitiveType.Cylinder),
				Part(PrimitiveType.Quad),
				Part(PrimitiveType.Plane));

			AddPartColliders(host);

			var parts = Children(host);
			Assert.AreEqual(6, parts.Length);

			Assert.IsInstanceOf<BoxCollider>(Collider(parts[0]), "a cube is a box");
			Assert.IsInstanceOf<SphereCollider>(Collider(parts[1]), "a sphere is a sphere");
			Assert.IsInstanceOf<CapsuleCollider>(Collider(parts[2]), "a capsule is a capsule");
			Assert.IsInstanceOf<CapsuleCollider>(Collider(parts[3]),
				"a cylinder is approximated by a capsule — rounded ends, usually what a leg or barrel wants");
			Assert.IsInstanceOf<BoxCollider>(Collider(parts[4]), "a quad is a box");
			Assert.IsInstanceOf<BoxCollider>(Collider(parts[5]), "a plane is a box");
		}

		[Test]
		public void ARendererWithNoPrimitiveShapeFallsBackToABox()
		{
			// A voxel mesh / sprite child has no PrimitiveShape marker, so there is no shape to match.
			var host = Track(new GameObject("host"));
			var child = new GameObject("mesh");
			child.transform.SetParent(host.transform, false);
			child.AddComponent<MeshFilter>().sharedMesh = UnitCubeMesh();
			child.AddComponent<MeshRenderer>();

			AddPartColliders(host);

			Assert.IsInstanceOf<BoxCollider>(Collider(child.transform),
				"an unmarked renderer gets a fitted box rather than a guess.");
		}

		// ------------------------------------------------------------------
		// Fitting — each collider is sized in its own part's local space.
		// ------------------------------------------------------------------

		[Test]
		public void CollidersGoOnThePartsNotTheEntityRoot()
		{
			var host = BuildModel(Part(PrimitiveType.Cube), Part(PrimitiveType.Cube));

			AddPartColliders(host);

			Assert.IsEmpty(host.GetComponents<Collider>(),
				"the entity root carries no collider of its own — the parts do.");

			foreach (var part in Children(host))
			{
				Assert.AreEqual(1, part.GetComponents<Collider>().Length,
					$"'{part.name}' should carry exactly one fitted collider.");
			}
		}

		[Test]
		public void EachBoxIsFittedToItsOwnPartsMesh()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube, size: new Vector3(2f, 1f, 2f)),
				Part(PrimitiveType.Cube, size: new Vector3(1f, 4f, 1f), position: new Vector3(3f, 0f, 0f)));

			AddPartColliders(host);

			foreach (var part in Children(host))
			{
				var collider = (BoxCollider)Collider(part);
				var local = part.GetComponent<MeshRenderer>().localBounds;

				AssertVector(local.size, collider.size, $"'{part.name}' is sized from its own mesh");
				AssertVector(local.center, collider.center, $"'{part.name}' is centred on its own mesh");
			}

			// The payoff of sizing in part-local space: the part transform's scale supplies the authored
			// world size, so the collider tracks it without any matrix work here.
			var parts = Children(host);
			AssertVector(new Vector3(2f, 1f, 2f), BoxWorldSize(parts[0]), "the first part's world size");
			AssertVector(new Vector3(1f, 4f, 1f), BoxWorldSize(parts[1]), "the second part's world size");
		}

		[Test]
		public void ACapsulePartIsAlignedToItsLongestAxis()
		{
			// Unity's capsule mesh is 2 tall and 1 across, so the fit should pick Y with height 2, radius 0.5.
			var host = BuildModel(Part(PrimitiveType.Capsule));

			AddPartColliders(host);

			var collider = (CapsuleCollider)Collider(Children(host)[0]);
			Assert.AreEqual(1, collider.direction, "the capsule is aligned to Y, its longest axis.");
			Assert.AreEqual(2f, collider.height, 1e-4f);
			Assert.AreEqual(0.5f, collider.radius, 1e-4f);
		}

		[Test]
		public void AFlatPartIsClampedToAUsableThickness()
		{
			// A quad is zero-thick on Z; left alone that is a collider physics cannot hit.
			var host = BuildModel(Part(PrimitiveType.Quad));

			var collider = (BoxCollider)FirstColliderOf(BuildAndFit(host));

			Assert.AreEqual(1f, collider.size.x, 1e-4f);
			Assert.AreEqual(1f, collider.size.y, 1e-4f);
			Assert.AreEqual(0.001f, collider.size.z, 1e-6f, "a flat part still needs thickness.");
		}

		// ------------------------------------------------------------------
		// Shared properties reach every collider.
		// ------------------------------------------------------------------

		[Test]
		public void TriggerAndOneSharedMaterialReachEveryPartCollider()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube),
				Part(PrimitiveType.Sphere, position: new Vector3(2f, 0f, 0f)));

			host.AddComponent<PartColliders>().Initialise(
				new PartColliderData("c")
				{
					IsTrigger = new ValueProvider<bool>(true),
					Bounciness = new ValueProvider<float>(0.75f)
				},
				Array.Empty<Listener>());

			var colliders = Children(host).Select(Collider).ToArray();
			Assert.AreEqual(2, colliders.Length);

			foreach (var collider in colliders)
			{
				Assert.IsTrue(collider.isTrigger, "IsTrigger should reach every part collider.");
				Assert.IsNotNull(collider.sharedMaterial, "the physics material should reach every one.");
				Assert.AreEqual(0.75f, collider.sharedMaterial.bounciness, 1e-4f);
			}

			Assert.AreSame(colliders[0].sharedMaterial, colliders[1].sharedMaterial,
				"one material is allocated per behaviour and shared, so the single OnDestroy still frees it.");
		}

		// ------------------------------------------------------------------
		// Boundaries.
		// ------------------------------------------------------------------

		[Test]
		public void ChildEntitiesGetNoColliderFromTheirParent()
		{
			var host = BuildModel(Part(PrimitiveType.Cube));

			// A child entity is parented under its parent's GameObject, exactly as GameEntityFactory does it.
			var child = new GameObject("child entity");
			child.AddComponent<GameEntity>();
			child.transform.SetParent(host.transform, false);
			var childVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
			childVisual.transform.SetParent(child.transform, false);
			foreach (var stray in childVisual.GetComponents<Collider>())
			{
				UnityEngine.Object.DestroyImmediate(stray);
			}

			AddPartColliders(host);

			Assert.IsEmpty(childVisual.GetComponents<Collider>(),
				"a child entity's visual is that entity's to collide, not its parent's.");
		}

		[Test]
		public void WithNoVisualItThrowsNamingTheBehaviourAndTheOrdering()
		{
			// The wrongly-ordered descriptor: GameEntityFactory creates every behaviour component up front and
			// only then runs the initialisations in declaration order, so a 'model' listed *after* this one is
			// present as a component but has not built its meshes yet.
			var host = Track(new GameObject("host"));
			var model = host.AddComponent<Model>();
			model.LiveProperties = host.AddComponent<LivePropertyUpdater>();

			var behaviour = host.AddComponent<PartColliders>();

			var error = Assert.Throws<MissingComponentException>(() =>
				behaviour.Initialise(new PartColliderData("c"), Array.Empty<Listener>()));

			StringAssert.Contains("part colliders", error!.Message);
			StringAssert.Contains("before", error.Message);
		}

		// ------------------------------------------------------------------
		// Helpers
		// ------------------------------------------------------------------

		private static void AddPartColliders(GameObject host) =>
			host.AddComponent<PartColliders>().Initialise(new PartColliderData("c"), Array.Empty<Listener>());

		private static GameObject BuildAndFit(GameObject host)
		{
			AddPartColliders(host);
			return host;
		}

		private static Collider FirstColliderOf(GameObject host) => Collider(Children(host)[0]);

		private static Collider Collider(Transform part) => part.GetComponent<Collider>();

		private static Vector3 BoxWorldSize(Transform part) =>
			Vector3.Scale(part.localScale, part.GetComponent<BoxCollider>().size);

		private static Mesh UnitCubeMesh()
		{
			var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
			UnityEngine.Object.DestroyImmediate(temp);
			return mesh;
		}

		private GameObject BuildModel(params ModelPart[] parts)
		{
			var host = Track(new GameObject("model host"));
			var model = host.AddComponent<Model>();
			model.LiveProperties = host.AddComponent<LivePropertyUpdater>();
			model.Initialise(new ModelData("m", parts, NullValueProvider<Color>.Instance), Array.Empty<Listener>());
			return host;
		}

		private static ModelPart Part(PrimitiveType shape, Vector3? position = null, Vector3? size = null) =>
			new(new ValueProvider<PrimitiveType>(shape),
				position is { } p ? new ValueProvider<Vector3>(p) : NullValueProvider<Vector3>.Instance,
				NullValueProvider<Vector3>.Instance,
				size is { } s ? new ValueProvider<Vector3>(s) : NullValueProvider<Vector3>.Instance,
				NullValueProvider<Color>.Instance,
				NullValueProvider<string>.Instance,
				NullValueProvider<MirrorAxis>.Instance,
				Vector3.zero);

		private static Transform[] Children(GameObject host) =>
			Enumerable.Range(0, host.transform.childCount).Select(host.transform.GetChild).ToArray();

		private static void AssertVector(Vector3 expected, Vector3 actual, string message) =>
			Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f),
				$"{message} — expected {expected}, got {actual}.");
	}
}
