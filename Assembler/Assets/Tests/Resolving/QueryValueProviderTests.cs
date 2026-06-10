using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class QueryValueProviderTests
	{
		private GameObject _target = null!;

		[TearDown]
		public void TearDown()
		{
			if (_target != null)
			{
				Object.DestroyImmediate(_target);
			}
		}

		private EntityQueryService ServiceWithEnemyAt(Vector3 position)
		{
			_target = new GameObject("enemy") { transform = { position = position } };
			var service = new EntityQueryService();
			service.Register("enemy", _target.transform, new[] { "enemy" });
			return service;
		}

		private static ResolutionContext ContextWith(EntityQueryService query) =>
			// Only EntityQuery and the constant From/MaxRange providers are exercised here.
			new(null!, null!, null!, null!, null, null!, query, null!);

		[Test]
		public void NearestIdResolvesToEntityId()
		{
			var service = ServiceWithEnemyAt(new Vector3(3, 0, 0));
			var source = new QuerySource<string>(QueryKind.NearestId, "enemy",
				new ConstantSource<Vector3>(Vector3.zero), new ConstantSource<float>(10f));

			var provider = source.Resolve(ContextWith(service));

			Assert.AreEqual("enemy", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void NearestPositionResolvesToEntityPosition()
		{
			var service = ServiceWithEnemyAt(new Vector3(3, 4, 0));
			var source = new QuerySource<Vector3>(QueryKind.NearestPosition, "enemy",
				new ConstantSource<Vector3>(Vector3.zero), new ConstantSource<float>(10f));

			var provider = source.Resolve(ContextWith(service));

			Assert.AreEqual(new Vector3(3, 4, 0), provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void NearestPositionFallsBackToFromWhenNoTarget()
		{
			var service = ServiceWithEnemyAt(new Vector3(50, 0, 0));
			var from = new Vector3(1, 1, 0);
			var source = new QuerySource<Vector3>(QueryKind.NearestPosition, "enemy",
				new ConstantSource<Vector3>(from), new ConstantSource<float>(5f));

			var provider = source.Resolve(ContextWith(service));

			Assert.AreEqual(from, provider.Get(TriggerContext.Empty));
		}
	}
}
