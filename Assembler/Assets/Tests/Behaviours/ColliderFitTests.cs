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
	/// Covers <c>Fit</c> on <c>box collider</c>/<c>sphere collider</c>: fitting one collider to the whole
	/// visual (<c>bounds</c>), one per visual part (<c>parts</c>), and the untouched authored path
	/// (<c>none</c>).
	/// </summary>
	/// <remarks>
	/// EditMode on purpose (Tests.Behaviours is Editor-only). <c>Model</c> strips the collider
	/// <c>GameObject.CreatePrimitive</c> bundles onto each part, and in play mode that <c>Destroy</c> defers
	/// to end of frame — so a play-mode "exactly one BoxCollider per part" assertion could see two. EditMode
	/// takes the <c>DestroyImmediate</c> branch and is clean.
	/// </remarks>
	public class ColliderFitTests : BehaviourTestFixture
	{
		// ------------------------------------------------------------------
		// Fit: bounds — one collider on the entity, size *and* centre fitted.
		// ------------------------------------------------------------------

		[Test]
		public void FitBounds_Box_SetsSizeAndCentreFromTheWholeVisual()
		{
			// A 2x1x2 base sitting on the origin with a 1x2x1 post on top of it: the union spans y 0–3, so its
			// centre is 1.5 above the entity origin. This is the case a hand-written Size cannot express —
			// box collider never touched `center` before Fit existed.
			var host = BuildModel(
				Part(PrimitiveType.Cube, size: new Vector3(2f, 1f, 2f), anchor: "bottom"),
				Part(PrimitiveType.Cube, size: new Vector3(1f, 2f, 1f), position: new Vector3(0f, 1f, 0f),
					anchor: "bottom"));

			var collider = FitBox(host, ColliderFit.Bounds);

			AssertVector(new Vector3(2f, 3f, 2f), collider.size, "the fitted box spans both parts");
			AssertVector(new Vector3(0f, 1.5f, 0f), collider.center,
				"the fitted box is centred on the visual, not on the entity origin");
		}

		[Test]
		public void FitBounds_Box_RecentresOnTheOriginForMirroredParts()
		{
			// A part at x=2 mirrored on X emits a twin at x=-2, so the union straddles the origin again.
			var host = BuildModel(Part(PrimitiveType.Cube, position: new Vector3(2f, 0f, 0f),
				mirror: MirrorAxis.X));

			var collider = FitBox(host, ColliderFit.Bounds);

			AssertVector(new Vector3(5f, 1f, 1f), collider.size, "the fitted box spans both twins");
			AssertVector(Vector3.zero, collider.center, "symmetric parts put the centre back on the origin");
		}

		[Test]
		public void FitBounds_Box_GrowsToContainARotatedPart()
		{
			// A unit cube spun 45° about Y: its axis-aligned box has to grow to √2 on X and Z to hold the
			// corners. Encapsulating only the min/max of the local bounds would miss them.
			var host = BuildModel(Part(PrimitiveType.Cube, rotation: new Vector3(0f, 45f, 0f)));

			var collider = FitBox(host, ColliderFit.Bounds);

			AssertVector(new Vector3(Mathf.Sqrt(2f), 1f, Mathf.Sqrt(2f)), collider.size,
				"the AABB grows to contain the rotated corners");
			AssertVector(Vector3.zero, collider.center, "a rotation about the centre leaves the centre alone");
		}

		[Test]
		public void FitBounds_Box_ClampsAFlatVisualToAUsableThickness()
		{
			// A quad is zero-thick on Z. Left alone that is a degenerate collider physics cannot hit.
			var host = BuildModel(Part(PrimitiveType.Quad));

			var collider = FitBox(host, ColliderFit.Bounds);

			Assert.AreEqual(1f, collider.size.x, 1e-4f);
			Assert.AreEqual(1f, collider.size.y, 1e-4f);
			Assert.Greater(collider.size.z, 0f, "a flat visual must still yield a collider with thickness.");
			Assert.AreEqual(0.001f, collider.size.z, 1e-6f);
		}

		[Test]
		public void FitBounds_Sphere_CentresOnTheVisualAndSpansItsLongestHalfExtent()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube, size: new Vector3(2f, 1f, 2f), anchor: "bottom"),
				Part(PrimitiveType.Cube, size: new Vector3(1f, 2f, 1f), position: new Vector3(0f, 1f, 0f),
					anchor: "bottom"));

			var collider = FitSphere(host, ColliderFit.Bounds);

			AssertVector(new Vector3(0f, 1.5f, 0f), collider.center, "the sphere sits on the visual's centre");
			// Half-extents are (1, 1.5, 1): the radius spans the longest, not the enclosing √(1+2.25+1).
			Assert.AreEqual(1.5f, collider.radius, 1e-4f,
				"the radius is the largest half-extent, not the enclosing radius.");
		}

		// ------------------------------------------------------------------
		// Fit: parts — one collider per visual part, on the part itself.
		// ------------------------------------------------------------------

		[Test]
		public void FitParts_Box_PutsOneFittedColliderOnEachPart()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube, size: new Vector3(2f, 1f, 2f)),
				Part(PrimitiveType.Cylinder, size: new Vector3(1f, 3f, 1f)));

			FitBox(host, ColliderFit.Parts);

			Assert.IsNull(host.GetComponent<BoxCollider>(),
				"parts mode puts the colliders on the parts, not on the entity root.");

			var parts = Children(host);
			Assert.AreEqual(2, parts.Length, "the model should have built one child per part.");

			foreach (var part in parts)
			{
				var colliders = part.GetComponents<BoxCollider>();
				Assert.AreEqual(1, colliders.Length, $"'{part.name}' should carry exactly one fitted collider.");

				var local = part.GetComponent<MeshRenderer>().localBounds;
				AssertVector(local.size, colliders[0].size, $"'{part.name}' is sized from its own mesh");
				AssertVector(local.center, colliders[0].center, $"'{part.name}' is centred on its own mesh");
			}

			// The payoff: sizing in the part's own local space means the part transform's scale supplies the
			// authored world size — including the cylinder's 2-unit-tall mesh, which Size normalises away.
			AssertVector(new Vector3(2f, 1f, 2f), WorldSize(parts[0]), "the cube part's world size");
			AssertVector(new Vector3(1f, 3f, 1f), WorldSize(parts[1]), "the cylinder part's world size");
		}

		[Test]
		public void FitParts_Sphere_FitsEachPartsOwnMesh()
		{
			var host = BuildModel(
				Part(PrimitiveType.Sphere, size: new Vector3(2f, 2f, 2f)),
				Part(PrimitiveType.Cube, position: new Vector3(4f, 0f, 0f)));

			FitSphere(host, ColliderFit.Parts);

			Assert.IsNull(host.GetComponent<SphereCollider>(),
				"parts mode puts the colliders on the parts, not on the entity root.");

			foreach (var part in Children(host))
			{
				var colliders = part.GetComponents<SphereCollider>();
				Assert.AreEqual(1, colliders.Length, $"'{part.name}' should carry exactly one fitted collider.");

				var local = part.GetComponent<MeshRenderer>().localBounds;
				AssertVector(local.center, colliders[0].center, $"'{part.name}' is centred on its own mesh");
				Assert.AreEqual(Mathf.Max(local.extents.x, Mathf.Max(local.extents.y, local.extents.z)),
					colliders[0].radius, 1e-4f, $"'{part.name}' spans its own largest half-extent.");
			}
		}

		[Test]
		public void FitParts_AppliesTriggerAndOneSharedMaterialToEveryCollider()
		{
			var host = BuildModel(
				Part(PrimitiveType.Cube),
				Part(PrimitiveType.Cube, position: new Vector3(2f, 0f, 0f)));

			var behaviour = host.AddComponent<AutoAddBoxColliderBehaviour>();
			behaviour.Initialise(
				new BoxColliderData("c")
				{
					Fit = new ValueProvider<ColliderFit>(ColliderFit.Parts),
					IsTrigger = new ValueProvider<bool>(true),
					Bounciness = new ValueProvider<float>(0.75f)
				},
				Array.Empty<Listener>());

			var colliders = Children(host).Select(p => p.GetComponent<BoxCollider>()).ToArray();
			Assert.AreEqual(2, colliders.Length);

			foreach (var collider in colliders)
			{
				Assert.IsTrue(collider.isTrigger, "IsTrigger should reach every collider the behaviour added.");
				Assert.IsNotNull(collider.sharedMaterial, "the physics material should reach every collider.");
				Assert.AreEqual(0.75f, collider.sharedMaterial.bounciness, 1e-4f);
			}

			Assert.AreSame(colliders[0].sharedMaterial, colliders[1].sharedMaterial,
				"one material is allocated per behaviour and shared, so the single OnDestroy still frees it.");
		}

		// ------------------------------------------------------------------
		// Boundaries — child entities, and nothing to fit to.
		// ------------------------------------------------------------------

		[Test]
		public void Fit_ExcludesTheVisualsOfChildEntities()
		{
			var host = BuildModel(Part(PrimitiveType.Cube));

			// A child entity is parented under its parent's GameObject, exactly as GameEntityFactory does it,
			// so a naive GetComponentsInChildren would fold its visual into the parent's collider.
			var child = new GameObject("child entity");
			child.AddComponent<GameEntity>();
			child.transform.SetParent(host.transform, false);
			child.transform.localPosition = new Vector3(100f, 0f, 0f);
			GameObject.CreatePrimitive(PrimitiveType.Cube).transform.SetParent(child.transform, false);

			var collider = FitBox(host, ColliderFit.Bounds);

			AssertVector(Vector3.one, collider.size, "the child entity's cube is not this entity's to fit");
			AssertVector(Vector3.zero, collider.center, "the child entity's cube did not drag the centre out");
		}

		[Test]
		public void Fit_WithTheColliderListedBeforeItsVisual_ThrowsNamingTheOrdering()
		{
			// The wrongly-ordered descriptor. GameEntityFactory creates every behaviour component up front and
			// only then runs the initialisations in declaration order, so a 'model' listed *after* the collider
			// is present as a component but has not built its meshes yet.
			var host = Track(new GameObject("host"));
			var model = host.AddComponent<Model>();
			model.LiveProperties = host.AddComponent<LivePropertyUpdater>();

			var behaviour = host.AddComponent<AutoAddBoxColliderBehaviour>();

			var error = Assert.Throws<MissingComponentException>(() => behaviour.Initialise(
				new BoxColliderData("c") { Fit = new ValueProvider<ColliderFit>(ColliderFit.Bounds) },
				Array.Empty<Listener>()));

			StringAssert.Contains("box collider", error!.Message);
			StringAssert.Contains("before", error.Message);
		}

		[Test]
		public void Fit_WithNoVisualBehaviourAtAll_ThrowsNamingTheBehaviour()
		{
			var host = Track(new GameObject("host"));
			var behaviour = host.AddComponent<AutoAddSphereColliderBehaviour>();

			var error = Assert.Throws<MissingComponentException>(() => behaviour.Initialise(
				new SphereColliderData("c") { Fit = new ValueProvider<ColliderFit>(ColliderFit.Parts) },
				Array.Empty<Listener>()));

			StringAssert.Contains("sphere collider", error!.Message);
			StringAssert.Contains("model", error.Message);
		}

		// ------------------------------------------------------------------
		// Fit: none — the pre-Fit behaviour, unchanged.
		// ------------------------------------------------------------------

		[Test]
		public void FitNone_UsesTheAuthoredSizeAndLeavesTheCentreUntouched()
		{
			// A visual is present and deliberately off-centre: none must ignore it entirely.
			var host = BuildModel(Part(PrimitiveType.Cube, position: new Vector3(0f, 5f, 0f)));

			var behaviour = host.AddComponent<AutoAddBoxColliderBehaviour>();
			behaviour.Initialise(
				new BoxColliderData("c")
				{
					Size = new ValueProvider<Vector3>(new Vector3(3f, 3f, 3f)),
					Fit = new ValueProvider<ColliderFit>(ColliderFit.None)
				},
				Array.Empty<Listener>());

			var collider = host.GetComponent<BoxCollider>();
			AssertVector(new Vector3(3f, 3f, 3f), collider.size, "Size is authored, never fitted");
			AssertVector(Vector3.zero, collider.center, "none leaves centre at Unity's default");
		}

		[Test]
		public void OmittedFit_IsTheSameAsNone()
		{
			var host = BuildModel(Part(PrimitiveType.Cube, position: new Vector3(0f, 5f, 0f)));

			var behaviour = host.AddComponent<AutoAddBoxColliderBehaviour>();
			behaviour.Initialise(new BoxColliderData("c"), Array.Empty<Listener>());

			var collider = host.GetComponent<BoxCollider>();
			AssertVector(Vector3.one, collider.size, "an omitted Size keeps Unity's default box");
			AssertVector(Vector3.zero, collider.center, "an omitted Fit leaves centre at Unity's default");
		}

		[Test]
		public void OmittedFit_OnASphere_KeepsUnitysDefaults()
		{
			var host = BuildModel(Part(PrimitiveType.Cube, position: new Vector3(0f, 5f, 0f)));

			var behaviour = host.AddComponent<AutoAddSphereColliderBehaviour>();
			behaviour.Initialise(
				new SphereColliderData("c") { Radius = new ValueProvider<float>(2f) },
				Array.Empty<Listener>());

			var collider = host.GetComponent<SphereCollider>();
			Assert.AreEqual(2f, collider.radius, 1e-4f, "Radius is authored, never fitted.");
			AssertVector(Vector3.zero, collider.center, "an omitted Fit leaves centre at Unity's default");
		}

		// ------------------------------------------------------------------
		// Helpers
		// ------------------------------------------------------------------

		private BoxCollider FitBox(GameObject host, ColliderFit fit)
		{
			host.AddComponent<AutoAddBoxColliderBehaviour>().Initialise(
				new BoxColliderData("c") { Fit = new ValueProvider<ColliderFit>(fit) },
				Array.Empty<Listener>());

			return host.GetComponent<BoxCollider>();
		}

		private SphereCollider FitSphere(GameObject host, ColliderFit fit)
		{
			host.AddComponent<AutoAddSphereColliderBehaviour>().Initialise(
				new SphereColliderData("c") { Fit = new ValueProvider<ColliderFit>(fit) },
				Array.Empty<Listener>());

			return host.GetComponent<SphereCollider>();
		}

		private GameObject BuildModel(params ModelPart[] parts)
		{
			var host = Track(new GameObject("model host"));
			var model = host.AddComponent<Model>();
			model.LiveProperties = host.AddComponent<LivePropertyUpdater>();
			model.Initialise(new ModelData("m", parts, NullValueProvider<Color>.Instance), Array.Empty<Listener>());
			return host;
		}

		private static ModelPart Part(PrimitiveType shape,
			Vector3? position = null,
			Vector3? rotation = null,
			Vector3? size = null,
			string? anchor = null,
			MirrorAxis? mirror = null) =>
			new(new ValueProvider<PrimitiveType>(shape),
				Provider(position),
				Provider(rotation),
				Provider(size),
				NullValueProvider<Color>.Instance,
				NullValueProvider<string>.Instance,
				mirror is { } m ? new ValueProvider<MirrorAxis>(m) : NullValueProvider<MirrorAxis>.Instance,
				anchor is null ? Vector3.zero : ModelAnchor.Parse(anchor, "test"));

		private static IValueProvider<Vector3> Provider(Vector3? value) =>
			value is { } v ? new ValueProvider<Vector3>(v) : NullValueProvider<Vector3>.Instance;

		private static Transform[] Children(GameObject host) =>
			Enumerable.Range(0, host.transform.childCount).Select(host.transform.GetChild).ToArray();

		// The world dimensions a parts-mode collider ends up with: its mesh-local size through the part's scale.
		private static Vector3 WorldSize(Transform part) =>
			Vector3.Scale(part.localScale, part.GetComponent<BoxCollider>().size);

		private static void AssertVector(Vector3 expected, Vector3 actual, string message) =>
			Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f),
				$"{message} — expected {expected}, got {actual}.");
	}
}
