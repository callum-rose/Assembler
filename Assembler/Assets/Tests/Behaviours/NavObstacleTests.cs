using System;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	// End-to-end cover for the `nav obstacle` behaviour: a real GameObject with a BoxCollider, driven through
	// OnInitialise / Execute / OnDestroy, so the collider-bounds footprint and the block->free path are exercised
	// the way the Bomberman soft blocks use them (block on build, free the cell synchronously when a blast hits).
	public class NavObstacleTests
	{
		// A flippable Blocked source, standing in for the `!var solid` a `bool variable setter` clears before the
		// obstacle is Executed.
		private sealed class MutableBool : IValueProvider<bool>
		{
			public bool Value = true;
			public bool Get(TriggerContext ctx) => Value;
			object IValueProvider.Get(TriggerContext ctx) => Value;
		}

		private GameObject _go = null!;

		[TearDown]
		public void TearDown()
		{
			if (_go != null)
			{
				UnityEngine.Object.DestroyImmediate(_go);
			}
		}

		// Cell size 1, integer cell centres, no static obstacles — only the dynamic overlay under test blocks.
		private static NavGridService Service() =>
			new(NavGridSettings.Default with { ObstacleTag = "" });

		private NavObstacle MakeObstacle(NavGridService nav, Vector3 position, MutableBool blocked)
		{
			_go = new GameObject("soft") { transform = { position = position } };
			_go.AddComponent<BoxCollider>().size = new Vector3(0.9f, 0.9f, 0.9f);
			var obstacle = _go.AddComponent<NavObstacle>();
			obstacle.Nav = nav;
			obstacle.Initialise(new NavObstacleData("o", blocked), Array.Empty<Listener>());
			return obstacle;
		}

		[Test]
		public void RegistersItsCellOnInitialise()
		{
			var nav = Service();
			var at = new Vector3(2f, 1f, 0f);
			MakeObstacle(nav, at, new MutableBool { Value = true });

			Assert.IsFalse(nav.IsWalkable(at, 0f), "the obstacle blocks the cell it sits on at build time");
			Assert.IsTrue(nav.IsWalkable(new Vector3(3f, 1f, 0f), 0f), "a neighbouring cell stays open");
		}

		[Test]
		public void ExecuteWithBlockedFalseFreesItsCell()
		{
			var nav = Service();
			var at = new Vector3(2f, 1f, 0f);
			var blocked = new MutableBool { Value = true };
			var obstacle = MakeObstacle(nav, at, blocked);
			Assert.IsFalse(nav.IsWalkable(at, 0f), "blocked while solid");

			// Mirrors the burn chain: the `clear solid` setter runs, then the obstacle is Executed.
			blocked.Value = false;
			obstacle.Execute(TriggerContext.Empty);

			Assert.IsTrue(nav.IsWalkable(at, 0f), "Executing with Blocked false frees the cell synchronously");
		}

		// Note: the OnDestroy leak-guard path (Destroy -> RemoveObstacle) can't be covered here — Unity lifecycle
		// callbacks (Awake/OnDestroy) don't fire under EditMode tests. The service-level RemoveObstacle it calls is
		// covered by NavGridServiceTests.TogglingADynamicObstacleOffReopensItsCell.
	}
}
