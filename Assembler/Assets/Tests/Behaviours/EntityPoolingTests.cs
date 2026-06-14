using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Behaviours.UI;
using Assembler.Building;
using Assembler.Compiler.Compiler;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tests.Behaviours
{
	/// <summary>
	/// Covers issue #102: <c>Spawn</c> reuses despawned entity shells instead of allocating fresh GameObjects, and
	/// a reused entity starts in clean initial state (here: a pooled Rigidbody respawns with zero velocity, proving
	/// the OnReuse reset runs).
	/// </summary>
	public class EntityPoolingTests
	{
		private const string TemplateId = "bullet";

		private GameObject _root = null!;
		private GameEntityFactory _factory = null!;

		[SetUp]
		public void SetUp()
		{
			_root = new GameObject("Game");

			var clock = new RealtimeGameClock();
			var typeRegistry = BuiltInTypeRegistry.Default;

			var parseContext = new TransformContext(
				Array.Empty<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				typeRegistry,
				new Dictionary<Type, MethodInfo>(),
				new InlineExpressionAccumulator(),
				RecordSchemaRegistry.Empty);

			// A spawn-heavy bullet: a single Rigidbody (created in Awake, configured in OnInitialise), the canonical
			// carrier of transient runtime state — its velocity must not survive into a reused instance.
			var template = new ConcreteEntityInfo(
				TemplateId,
				Array.Empty<string>(),
				new ConstantSource<Vector3>(Vector3.zero),
				new ConstantSource<Vector3>(Vector3.zero),
				new BehaviourInfo[]
				{
					RigidbodyInfo.Create("body", Array.Empty<ListenerInfo>(),
						new Dictionary<string, AssemblerValue>(), parseContext)
				},
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());

			_factory = new GameEntityFactory(
				new VariableRegistry(),
				new CompiledExpressionsRegistry(typeRegistry, new ExpressionMethodCompiler()),
				new Assembler.Building.BehaviourRegistry(),
				new AssetRegistry(),
				new StringTableRegistry(new LocaleSettings("en")),
				new EntityTransformRegistry(),
				new EntityQueryService(),
				new LineOfSightService(),
				new NavGridService(NavGridSettings.Default),
				new ExclusiveGroupRegistry(clock),
				clock,
				_root.AddComponent<LivePropertyUpdater>(),
				new Dictionary<string, EntityInfo> { [TemplateId] = template },
				parseContext,
				_root.transform,
				ControlsInfo.Empty,
				ScriptableObject.CreateInstance<InputActionAsset>(),
				ScriptableObject.CreateInstance<UiPrefabLibrary>());
		}

		[TearDown]
		public void TearDown() => UnityEngine.Object.DestroyImmediate(_root);

		private void SpawnBullet() =>
			_factory.Spawn(TemplateId, Vector3.zero, Vector3.zero, new Dictionary<string, object>());

		// Active spawned instances of the template (excludes pooled, deactivated shells).
		private IReadOnlyList<GameEntity> LiveBullets() =>
			_root.GetComponentsInChildren<GameEntity>(includeInactive: true)
				.Where(e => e.TemplateId == TemplateId && e.gameObject.activeSelf)
				.ToList();

		// Every shell of the template under the root, live or pooled — what would grow if the pool didn't recycle.
		private int TotalBulletShells() =>
			_root.GetComponentsInChildren<GameEntity>(includeInactive: true)
				.Count(e => e.TemplateId == TemplateId);

		[Test]
		public void RespawnReusesPooledShellsRatherThanAllocating()
		{
			for (var i = 0; i < 3; i++)
			{
				SpawnBullet();
			}

			var firstShells = LiveBullets().Select(e => e.gameObject).ToHashSet();
			Assert.AreEqual(3, firstShells.Count);

			foreach (var bullet in LiveBullets())
			{
				_factory.Despawn(bullet);
			}

			Assert.AreEqual(0, LiveBullets().Count, "despawned bullets should be deactivated, not live");

			for (var i = 0; i < 3; i++)
			{
				SpawnBullet();
			}

			var secondShells = LiveBullets().Select(e => e.gameObject).ToHashSet();
			Assert.AreEqual(3, secondShells.Count);

			Assert.IsTrue(secondShells.SetEquals(firstShells),
				"respawn should rebuild onto the three pooled shells, not allocate new GameObjects");
			Assert.AreEqual(3, TotalBulletShells(),
				"the pool should bound the shell count — no growth across despawn/respawn churn");
		}

		[Test]
		public void ReusedEntityRespawnsWithCleanRigidbodyVelocity()
		{
			SpawnBullet();
			var bullet = LiveBullets().Single();

			var body = bullet.GetComponent<Rigidbody>();
			Assert.IsNotNull(body, "the bullet template carries a Rigidbody");
			body.linearVelocity = new Vector3(7f, 3f, 0f);
			body.angularVelocity = new Vector3(0f, 2f, 0f);

			_factory.Despawn(bullet);
			SpawnBullet();

			var reused = LiveBullets().Single();
			Assert.AreSame(bullet.gameObject, reused.gameObject, "the despawned shell should be reused");

			var reusedBody = reused.GetComponent<Rigidbody>();
			Assert.AreEqual(Vector3.zero, reusedBody.linearVelocity, "OnReuse should clear the carried linear velocity");
			Assert.AreEqual(Vector3.zero, reusedBody.angularVelocity, "OnReuse should clear the carried angular velocity");
		}

		[Test]
		public void ReusedEntityGetsAFreshSpawnId()
		{
			SpawnBullet();
			var firstId = LiveBullets().Single().Id;

			_factory.Despawn(LiveBullets().Single());
			SpawnBullet();
			var secondId = LiveBullets().Single().Id;

			Assert.AreNotEqual(firstId, secondId,
				"each spawn gets a fresh id so listener descriptors stay consistent, even when the shell is reused");
		}
	}
}
