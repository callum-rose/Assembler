using System;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class ParticleBurstTests
	{
		// A burst owns a child ParticleSystem and only emits when Executed — never as a steady stream — so a
		// freshly built behaviour sits at zero particles until a trigger fires it.
		[Test]
		public void ParticleBurst_EmitsCountParticlesOnExecute()
		{
			var go = new GameObject("burst host");
			try
			{
				var behaviour = go.AddComponent<ParticleBurst>();
				behaviour.Initialise(Data(count: new ValueProvider<int>(8)), Array.Empty<Listener>());

				var particles = go.GetComponentInChildren<ParticleSystem>();
				Assert.IsNotNull(particles, "particle burst should add a child ParticleSystem.");
				Assert.AreEqual(0, particles.particleCount, "no particles should exist before the burst fires.");

				behaviour.Execute(TriggerContext.Empty);

				Assert.AreEqual(8, particles.particleCount, "Execute should emit Count particles.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// Direction is re-read each fire (so it can be wired from a collision normal); the child orients so its
		// forward axis — the cone's spray axis — matches the requested direction.
		[Test]
		public void ParticleBurst_OrientsAlongDirectionEachFire()
		{
			var go = new GameObject("burst host");
			try
			{
				var behaviour = go.AddComponent<ParticleBurst>();
				behaviour.Initialise(
					Data(direction: new ValueProvider<Vector3>(Vector3.right)),
					Array.Empty<Listener>());

				behaviour.Execute(TriggerContext.Empty);

				var particles = go.GetComponentInChildren<ParticleSystem>();
				Assert.That(Vector3.Dot(particles.transform.forward, Vector3.right), Is.GreaterThan(0.99f),
					"the cone should spray along the requested direction.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// The "death debris" preset renders chunky 3D meshes rather than billboards.
		[Test]
		public void ParticleBurst_MeshShapeUsesMeshRenderMode()
		{
			var go = new GameObject("burst host");
			try
			{
				var behaviour = go.AddComponent<ParticleBurst>();
				behaviour.Initialise(
					Data(shape: new ValueProvider<ParticleShape>(ParticleShape.Cube)),
					Array.Empty<Listener>());

				var renderer = go.GetComponentInChildren<ParticleSystemRenderer>();
				Assert.AreEqual(ParticleSystemRenderMode.Mesh, renderer.renderMode);
				Assert.IsNotNull(renderer.mesh, "a mesh-shaped burst should assign a particle mesh.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// Every property is optional; an all-defaults burst must build and fire without throwing.
		private static ParticleBurstData Data(
			IValueProvider<int>? count = null,
			IValueProvider<Vector3>? direction = null,
			IValueProvider<ParticleShape>? shape = null) =>
			new("burst",
				count ?? NullValueProvider<int>.Instance,
				direction ?? NullValueProvider<Vector3>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<Vector3>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<Color>.Instance,
				NullValueProvider<Color>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				NullValueProvider<float>.Instance,
				shape ?? NullValueProvider<ParticleShape>.Instance,
				NullValueProvider<bool>.Instance);
	}
}
