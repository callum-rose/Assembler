using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class TransformPropertyProviderTests
	{
		private GameObject _gameObject = null!;
		private EntityTransformRegistry _registry = null!;

		[SetUp]
		public void SetUp()
		{
			_gameObject = new GameObject("entity");
			_registry = new EntityTransformRegistry();
			_registry.Register("entity", _gameObject.transform);
		}

		[TearDown]
		public void TearDown() => Object.DestroyImmediate(_gameObject);

		private ResolutionContext Context() =>
			// Only EntityTransforms is exercised when resolving an EntityPropertySource; the other
			// registries are not touched, so they can be left null for this focused test.
			new(null!, null!, null!, null!, null, _registry, null!, null!);

		private IWriteValueProvider<Vector3> Resolve(EntityProperty property) =>
			new EntityPropertySource<Vector3>("entity", property).Resolve(Context()).AsWritable();

		[Test]
		public void ResolvesPositionFromTransform()
		{
			_gameObject.transform.position = new Vector3(1, 2, 3);

			Assert.AreEqual(new Vector3(1, 2, 3), Resolve(EntityProperty.Position).Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesRotationAsEulerAngles()
		{
			_gameObject.transform.eulerAngles = new Vector3(10, 20, 30);

			// Compared against the live transform read (not the assigned value) to avoid quaternion round-trip drift.
			Assert.AreEqual(_gameObject.transform.eulerAngles, Resolve(EntityProperty.Rotation).Get(TriggerContext.Empty));
		}

		[Test]
		public void ResolvesScaleAsLocalScale()
		{
			_gameObject.transform.localScale = new Vector3(2, 3, 4);

			Assert.AreEqual(new Vector3(2, 3, 4), Resolve(EntityProperty.Scale).Get(TriggerContext.Empty));
		}

		[Test]
		public void ReadIsLiveNotSnapshotted()
		{
			_gameObject.transform.position = new Vector3(1, 1, 1);
			var provider = Resolve(EntityProperty.Position);

			Assert.AreEqual(new Vector3(1, 1, 1), provider.Get(TriggerContext.Empty));

			_gameObject.transform.position = new Vector3(5, 6, 7);
			Assert.AreEqual(new Vector3(5, 6, 7), provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void SetWritesBackToTransform()
		{
			Resolve(EntityProperty.Scale).Set(new Vector3(9, 9, 9));

			Assert.AreEqual(new Vector3(9, 9, 9), _gameObject.transform.localScale);
		}
	}
}
