using System.Collections.Generic;
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
		}

		[Test]
		public void TagTarget_resolves_the_first_matching_transform()
		{
			var a = NewTransform("a");
			var b = NewTransform("b");
			var byTag = new Dictionary<string, List<Transform>> { ["enemy"] = new() { a, b } };

			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("enemy"),
				tag => byTag.TryGetValue(tag, out var list) ? list : new List<Transform>());

			Assert.IsTrue(target.TryGetTransform(out var first));
			Assert.AreSame(a, first);
		}

		[Test]
		public void TagTarget_is_requeried_each_read_so_it_catches_spawned_entities()
		{
			var matches = new List<Transform>();

			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("mob"),
				_ => matches);

			Assert.IsFalse(target.TryGetTransform(out _), "no entity carries the tag yet");

			// Simulate an entity spawning after build: the same target picks it up on the next read.
			var spawned = NewTransform("spawned");
			matches.Add(spawned);

			Assert.IsTrue(target.TryGetTransform(out var resolved));
			Assert.AreSame(spawned, resolved);
		}

		[Test]
		public void TagTarget_with_no_matches_reports_no_transform()
		{
			ICameraTarget target = new TagCameraTarget(
				new ValueProvider<string>("ghost"),
				_ => new List<Transform>());

			Assert.IsFalse(target.TryGetTransform(out _));
		}

		[Test]
		public void NoCameraTarget_never_resolves_a_transform()
		{
			Assert.IsFalse(NoCameraTarget.Instance.TryGetTransform(out _));
		}
	}
}
