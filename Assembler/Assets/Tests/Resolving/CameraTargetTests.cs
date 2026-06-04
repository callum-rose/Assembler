using System.Collections.Generic;
using System.Linq;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Resolving
{
	public class CameraTargetTests
	{
		private readonly List<GameObject> _created = new();

		private Transform NewTransform(string name)
		{
			var go = new GameObject(name);
			_created.Add(go);
			return go.transform;
		}

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _created)
			{
				Object.DestroyImmediate(go);
			}

			_created.Clear();
		}

		[Test]
		public void IdTarget_resolves_the_registered_transform()
		{
			var registry = new EntityTransformRegistry();
			var player = NewTransform("player");
			registry.Register("player", player);

			ICameraTarget target = new IdCameraTarget(registry, "player");

			Assert.IsTrue(target.TryGetTransform(out var resolved));
			Assert.AreSame(player, resolved);
			CollectionAssert.AreEqual(new[] { player }, target.GetTransforms());
		}

		[Test]
		public void TagTarget_resolves_all_matching_transforms()
		{
			var a = NewTransform("a");
			var b = NewTransform("b");
			var byTag = new Dictionary<string, List<Transform>> { ["enemy"] = new() { a, b } };

			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("enemy"),
				tag => byTag.TryGetValue(tag, out var list) ? list : new List<Transform>());

			CollectionAssert.AreEquivalent(new[] { a, b }, target.GetTransforms());
			Assert.IsTrue(target.TryGetTransform(out var first));
			Assert.AreSame(a, first);
		}

		[Test]
		public void TagTarget_is_requeried_each_read_so_it_catches_spawned_entities()
		{
			var initial = NewTransform("first");
			var matches = new List<Transform> { initial };

			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("mob"),
				_ => matches);

			Assert.AreEqual(1, target.GetTransforms().Count);

			// Simulate an entity spawning after build: the same target picks it up on the next read.
			var spawned = NewTransform("spawned");
			matches.Add(spawned);

			CollectionAssert.AreEquivalent(new[] { initial, spawned }, target.GetTransforms());
		}

		[Test]
		public void TagTarget_with_no_matches_reports_no_transform()
		{
			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("ghost"),
				_ => new List<Transform>());

			Assert.IsFalse(target.TryGetTransform(out _));
			Assert.IsEmpty(target.GetTransforms());
		}
	}
}
