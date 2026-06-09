using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class RigidbodyPropertyProviderTests
	{
		private GameObject _gameObject = null!;
		private Rigidbody _rigidbody = null!;
		private EntityTransformRegistry _registry = null!;

		[SetUp]
		public void SetUp()
		{
			_gameObject = new GameObject("entity");
			_rigidbody = _gameObject.AddComponent<Rigidbody>();
			_registry = new EntityTransformRegistry();
			_registry.Register("entity", _gameObject.transform);
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(_gameObject);

		private ResolutionContext Context(EntityTransformRegistry registry) =>
			// Only EntityTransforms is exercised when resolving a RigidbodyPropertySource; the other
			// registries are not touched, so they can be left null for this focused test.
			new(null!, null!, null!, null!, null, registry, null!);

		private IValueProvider<Vector3> Resolve(RigidbodyProperty property) =>
			new RigidbodyPropertySource<Vector3>("entity", property).Resolve(Context(_registry));

		[Test]
		public void ResolvesLinearVelocityFromRigidbody()
		{
			_rigidbody.linearVelocity = new Vector3(1, 2, 3);

			Assert.AreEqual(new Vector3(1, 2, 3), Resolve(RigidbodyProperty.Velocity).Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesAngularVelocityFromRigidbody()
		{
			_rigidbody.angularVelocity = new Vector3(4, 5, 6);

			Assert.AreEqual(new Vector3(4, 5, 6), Resolve(RigidbodyProperty.AngularVelocity).Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesPositionFromRigidbody()
		{
			_rigidbody.position = new Vector3(7, 8, 9);

			Assert.AreEqual(new Vector3(7, 8, 9), Resolve(RigidbodyProperty.Position).Get(TriggerContext.Empty));
		}

		[Test]
		public void ReadIsLiveNotSnapshotted()
		{
			_rigidbody.linearVelocity = new Vector3(1, 1, 1);
			var provider = Resolve(RigidbodyProperty.Velocity);

			Assert.AreEqual(new Vector3(1, 1, 1), provider.Get(TriggerContext.Empty));

			_rigidbody.linearVelocity = new Vector3(5, 6, 7);
			Assert.AreEqual(new Vector3(5, 6, 7), provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void SetWritesBackToRigidbody()
		{
			Resolve(RigidbodyProperty.Velocity).Set(new Vector3(9, 9, 9));

			Assert.AreEqual(new Vector3(9, 9, 9), _rigidbody.linearVelocity);
		}

		[Test]
		public void NoRigidbodyReadsAsZero()
		{
			var without = new GameObject("no rigidbody");
			var registry = new EntityTransformRegistry();
			registry.Register("entity", without.transform);

			try
			{
				var provider = new RigidbodyPropertySource<Vector3>("entity", RigidbodyProperty.Velocity)
					.Resolve(Context(registry));

				Assert.AreEqual(Vector3.zero, provider.Get(TriggerContext.Empty));
			}
			finally
			{
				Object.DestroyImmediate(without);
			}
		}
	}
}
