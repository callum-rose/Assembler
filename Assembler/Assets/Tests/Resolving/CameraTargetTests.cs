using System;
using System.Collections.Generic;
using Assembler.Parsing.Info.Behaviours;
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
				UnityEngine.Object.DestroyImmediate(go);
			}

			_created.Clear();
		}

		[Test]
		public void Provider_returns_the_wrapped_transform()
		{
			var player = NewTransform("player");

			IValueProvider<Transform> target = new CameraTargetProvider(() => player);

			Assert.AreSame(player, target.Get());
		}

		[Test]
		public void Provider_is_requeried_each_read_so_it_catches_spawned_entities()
		{
			var matches = new List<Transform>();

			// Models a tag target: first match (or null) re-evaluated on every read.
			IValueProvider<Transform> target = new CameraTargetProvider(
				() => matches.Count > 0 ? matches[0] : null);

			Assert.IsNull(target.Get(), "no entity carries the tag yet");

			// Simulate an entity spawning after build: the same provider picks it up on the next read.
			var spawned = NewTransform("spawned");
			matches.Add(spawned);

			Assert.AreSame(spawned, target.Get());
		}

		[Test]
		public void Provider_returns_null_when_the_target_is_absent()
		{
			IValueProvider<Transform> target = new CameraTargetProvider(() => null);

			Assert.IsNull(target.Get());
		}

		[Test]
		public void Resolver_maps_an_absent_source_to_the_null_provider()
		{
			var resolved = CameraTargetResolver.Resolve(
				NoCameraTargetSource.Instance,
				ctx: null!,
				resolveByEntityTag: _ => Array.Empty<Transform>());

			Assert.AreSame(NullValueProvider<Transform>.Instance, resolved);
		}
	}
}
