using System;
using System.Collections.Generic;
using Assembler.Building;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class GameOverReachabilityTests
	{
		// --- false reject: a !gameover must be found wherever it lives ---

		[Test]
		public void TopLevelGameOverOnConcreteEntity_IsReachable()
		{
			var entity = Entity("player", GameOverBehaviour());

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(Game(entities: new[] { entity })));
		}

		[Test]
		public void NoGameOverAnywhere_IsNotReachable()
		{
			var entity = Entity("player", Plain("body"));

			Assert.IsFalse(GameOverReachability.HasReachableGameOver(Game(entities: new[] { entity })));
		}

		[Test]
		public void NestedGameOverInStateMachineHook_IsReachable()
		{
			// Regression: a !gameover authored in a state machine's OnEnter hook lives in States[].OnEnter,
			// not in the behaviour's top-level Listeners, and was previously missed.
			var machine = new StateMachineInfo(
				"ai",
				Array.Empty<ListenerInfo>(),
				"guard_state",
				"patrol",
				new[]
				{
					new StateInfo("patrol", Array.Empty<ListenerInfo>(), Array.Empty<ListenerInfo>()),
					new StateInfo("caught", new ListenerInfo[] { new GameOverListenerInfo() }, Array.Empty<ListenerInfo>())
				},
				Array.Empty<TransitionInfo>());

			var entity = Entity("guard", machine);

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(Game(entities: new[] { entity })));
		}

		[Test]
		public void GameOverOnNestedChildEntity_IsReachable()
		{
			var child = new ChildEntityInfo(
				"flag",
				TemplateRefId: null,
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>(),
				None<Vector3>.Instance,
				None<Vector3>.Instance,
				new BehaviourInfo[] { GameOverBehaviour() },
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());

			var entity = Entity("root", new[] { Plain("body") }, new[] { child });

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(Game(entities: new[] { entity })));
		}

		// --- false accept: a !gameover only in a dead template must not count ---

		[Test]
		public void GameOverOnlyInNeverInstantiatedTemplate_IsNotReachable()
		{
			var template = Entity("enemy", GameOverBehaviour());
			var entity = Entity("player", Plain("body"));

			Assert.IsFalse(GameOverReachability.HasReachableGameOver(
				Game(entities: new[] { entity }, templates: new EntityInfo[] { template })));
		}

		[Test]
		public void GameOverInTemplateInstantiatedByPlacement_IsReachable()
		{
			var template = Entity("enemy", GameOverBehaviour());
			var placement = new PlacementInfo(
				"wave",
				"enemy",
				new ConstantSource<List<Vector3>>(new List<Vector3> { Vector3.zero }),
				None<Vector3>.Instance,
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>());

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(
				Game(templates: new EntityInfo[] { template }, placements: new[] { placement })));
		}

		[Test]
		public void GameOverInTemplateInstantiatedBySpawner_IsReachable()
		{
			var template = Entity("enemy", GameOverBehaviour());
			var spawner = Spawner("spawn", new ConstantSource<string>("enemy"));
			var entity = Entity("spawner host", spawner);

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(
				Game(entities: new[] { entity }, templates: new EntityInfo[] { template })));
		}

		[Test]
		public void GameOverInTemplateInstantiatedByChildRef_IsReachable()
		{
			var template = Entity("enemy", GameOverBehaviour());
			var child = new ChildEntityInfo(
				"spawned",
				TemplateRefId: "enemy",
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>(),
				None<Vector3>.Instance,
				None<Vector3>.Instance,
				Array.Empty<BehaviourInfo>(),
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());
			var entity = Entity("root", new[] { Plain("body") }, new[] { child });

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(
				Game(entities: new[] { entity }, templates: new EntityInfo[] { template })));
		}

		[Test]
		public void GameOverReachableTransitivelyThroughTemplates_IsReachable()
		{
			// placed "spawner template" spawns "enemy", which carries the only !gameover.
			var enemy = Entity("enemy", GameOverBehaviour());
			var spawnerTemplate = Entity("spawner template", Spawner("spawn", new ConstantSource<string>("enemy")));
			var placement = new PlacementInfo(
				"wave",
				"spawner template",
				new ConstantSource<List<Vector3>>(new List<Vector3> { Vector3.zero }),
				None<Vector3>.Instance,
				new Dictionary<string, AssemblerValue>(),
				Array.Empty<string>());

			Assert.IsTrue(GameOverReachability.HasReachableGameOver(
				Game(templates: new EntityInfo[] { enemy, spawnerTemplate }, placements: new[] { placement })));
		}

		[Test]
		public void GameOverInTemplateSpawnedByDynamicId_IsNotReachable()
		{
			// A computed spawn target can't be resolved statically; the guard conservatively demands an
			// unambiguous game-over path rather than trusting an id only known at runtime.
			var template = Entity("enemy", GameOverBehaviour());
			var spawner = Spawner("spawn", new ValueReferenceSource<string>("which_enemy"));
			var entity = Entity("spawner host", spawner);

			Assert.IsFalse(GameOverReachability.HasReachableGameOver(
				Game(entities: new[] { entity }, templates: new EntityInfo[] { template })));
		}

		// --- helpers ---

		private static BehaviourInfo Plain(string id) =>
			new OnStartTriggerInfo(id, Array.Empty<ListenerInfo>());

		private static BehaviourInfo GameOverBehaviour() =>
			new OnStartTriggerInfo("end", new ListenerInfo[] { new GameOverListenerInfo() });

		private static SpawnerInfo Spawner(string id, ValueSource<string> templateId) =>
			new(id,
				Array.Empty<ListenerInfo>(),
				templateId,
				Array.Empty<SpawnTemplateInfo>(),
				None<string>.Instance,
				None<Vector3>.Instance,
				None<Vector3>.Instance,
				new Dictionary<string, ValueSource<object>>());

		private static ConcreteEntityInfo Entity(string id, params BehaviourInfo[] behaviours) =>
			Entity(id, behaviours, Array.Empty<ChildEntityInfo>());

		private static ConcreteEntityInfo Entity(
			string id,
			IReadOnlyList<BehaviourInfo> behaviours,
			IReadOnlyList<ChildEntityInfo> children) =>
			new(id,
				Array.Empty<string>(),
				None<Vector3>.Instance,
				None<Vector3>.Instance,
				behaviours,
				Array.Empty<ValueInfo>(),
				children);

		private static GameInfo Game(
			IReadOnlyList<ConcreteEntityInfo>? entities = null,
			IReadOnlyList<EntityInfo>? templates = null,
			IReadOnlyList<PlacementInfo>? placements = null) =>
			new(new AboutInfo("t", "d"),
				new WorldInfo(2, Color.black),
				new PhysicsInfo(Vector3.zero),
				Array.Empty<AssetInfo>(),
				LocalisationInfo.Empty,
				Array.Empty<ValueInfo>(),
				Array.Empty<ExpressionInfo>(),
				templates ?? Array.Empty<EntityInfo>(),
				entities ?? Array.Empty<ConcreteEntityInfo>(),
				placements ?? Array.Empty<PlacementInfo>());
	}
}
