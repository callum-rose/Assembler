using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Visual
{
	/// <summary>Emits a one-shot spray of pooled particles when Executed (typically from a collision, death, or
	/// movement trigger). The single configurable behaviour behind several "juice" effects — impact bursts, dust/
	/// scuff puffs, and death debris — which differ only in their property values and the trigger that fires them.</summary>
	/// <remarks>
	/// Particles simulate in world space, so a burst stays put after it fires rather than trailing the entity that
	/// spawned it. Direction and velocity inheritance are re-read on every fire, so wire them straight from a
	/// collision trigger's outputs (<c>Direction: !output contact_normal</c>, <c>InheritVelocity: !output other_velocity</c>)
	/// to spray along the surface normal with the impact's momentum. The remaining properties are applied once at
	/// build. This is a leaf presentation effect: it does not notify listeners.
	/// Properties:
	///   Count [int]: Particles emitted per burst (default 12). Re-read each fire.
	///   Direction [Vector3]: Axis the cone sprays along, world space (default (0,1,0) — up). Re-read each fire.
	///   Spread [float]: Cone half-angle in degrees; 0 is a tight jet, 90 a flat disc (default 25).
	///   Speed [float]: Launch speed in units/second (default 4).
	///   SpeedVariation [float]: 0..1 fraction that randomises speed downward from Speed (default 0.4).
	///   InheritVelocity [Vector3]: World velocity added to every particle, e.g. the impactor's momentum (default zero). Re-read each fire.
	///   InheritFactor [float]: Multiplier applied to InheritVelocity (default 1). Re-read each fire.
	///   Lifetime [float]: Seconds each particle lives (default 0.6).
	///   StartColour [Color]: Colour at birth (default white).
	///   EndColour [Color]: Colour at death (default = StartColour faded to alpha 0, i.e. fade out).
	///   StartSize [float]: Size at birth in world units (default 0.15).
	///   EndSize [float]: Size at death (default 0, i.e. shrink away).
	///   Gravity [float]: Gravity modifier; 0 floats, &gt;0 falls, &lt;0 rises (default 0).
	///   Drag [float]: Linear drag that slows particles over their life (default 0).
	///   Shape: How each particle renders — "billboard" (default), "cube", or "sphere" (3D meshes for debris chunks).
	///   Collision [bool]: When true, mesh/billboard particles bounce off world colliders — chunky debris that settles (default false).
	/// </remarks>
	public sealed class ParticleBurst : GameBehaviour<ParticleBurstData>, IAmExecutable
	{
		// A huge speed cap so the limit-velocity module applies drag without actually clamping speed.
		private const float NoSpeedLimit = 1e6f;

		private static Material? _particleMaterial;
		private static Mesh? _cubeMesh;
		private static Mesh? _sphereMesh;

		private GameObject _child = null!;
		private ParticleSystem _particles = null!;

		protected override void OnInitialise(ParticleBurstData data)
		{
			// Child object so the burst's world-space simulation and per-fire orientation are independent of the
			// owning entity's transform (which may keep moving and rotating after the burst fires).
			_child = new GameObject("particle burst");
			_child.transform.SetParent(transform, false);

			_particles = _child.AddComponent<ParticleSystem>();
			_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
			Configure(data);
			_particles.Clear();
			_particles.Play();
		}

		public void Execute(TriggerContext ctx)
		{
			var direction = Data.Direction.ValueOr(ctx, Vector3.up);
			if (direction.sqrMagnitude < 1e-8f)
			{
				direction = Vector3.up;
			}

			_child.transform.SetPositionAndRotation(transform.position, Quaternion.LookRotation(direction.normalized));

			var inherited = Data.InheritVelocity.ValueOr(ctx, Vector3.zero) * Data.InheritFactor.ValueOr(ctx, 1f);
			var velocity = _particles.velocityOverLifetime;
			velocity.x = inherited.x;
			velocity.y = inherited.y;
			velocity.z = inherited.z;

			_particles.Emit(Mathf.Max(0, Data.Count.ValueOr(ctx, 12)));
		}

		private void Configure(ParticleBurstData data)
		{
			var speed = data.Speed.ValueOr(4f);
			var variation = Mathf.Clamp01(data.SpeedVariation.ValueOr(0.4f));
			var startColour = data.StartColour.ValueOr(Color.white);
			var endColour = data.EndColour.ValueOr(new Color(startColour.r, startColour.g, startColour.b, 0f));
			var startSize = data.StartSize.ValueOr(0.15f);
			var endSize = data.EndSize.ValueOr(0f);

			var main = _particles.main;
			main.startLifetime = data.Lifetime.ValueOr(0.6f);
			main.startSpeed = new ParticleSystem.MinMaxCurve(speed * (1f - variation), speed);
			main.startSize = startSize;
			main.startColor = Color.white; // The actual colour ramp lives in colorOverLifetime (an absolute gradient).
			main.gravityModifier = data.Gravity.ValueOr(0f);
			main.simulationSpace = ParticleSystemSimulationSpace.World;
			main.playOnAwake = false;
			main.maxParticles = 2000;

			var emission = _particles.emission;
			emission.enabled = true;
			emission.rateOverTime = 0f; // Burst-only: particles come from explicit Emit() calls, never a steady stream.

			var shape = _particles.shape;
			shape.enabled = true;
			shape.shapeType = ParticleSystemShapeType.Cone;
			shape.angle = data.Spread.ValueOr(25f);
			shape.radius = 0.0001f;

			var velocity = _particles.velocityOverLifetime;
			velocity.enabled = true;
			velocity.space = ParticleSystemSimulationSpace.World; // Per-fire inherited velocity is set in Execute.

			var colour = _particles.colorOverLifetime;
			colour.enabled = true;
			var gradient = new Gradient();
			gradient.SetKeys(
				new[] { new GradientColorKey(startColour, 0f), new GradientColorKey(endColour, 1f) },
				new[] { new GradientAlphaKey(startColour.a, 0f), new GradientAlphaKey(endColour.a, 1f) });
			colour.color = new ParticleSystem.MinMaxGradient(gradient);

			var size = _particles.sizeOverLifetime;
			size.enabled = true;
			var sizeRatio = startSize > 0f ? endSize / startSize : 0f;
			size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, sizeRatio));

			var drag = data.Drag.ValueOr(0f);
			if (drag > 0f)
			{
				var limit = _particles.limitVelocityOverLifetime;
				limit.enabled = true;
				limit.limit = new ParticleSystem.MinMaxCurve(NoSpeedLimit);
				limit.drag = new ParticleSystem.MinMaxCurve(drag);
			}

			if (data.Collision.ValueOr(false))
			{
				var collision = _particles.collision;
				collision.enabled = true;
				collision.type = ParticleSystemCollisionType.World;
				collision.mode = ParticleSystemCollisionMode.Collision3D;
				collision.bounce = 0.4f;
				collision.dampen = 0.3f;
			}

			ConfigureRenderer(data.Shape.ValueOr(ParticleShape.Billboard));
		}

		private void ConfigureRenderer(ParticleShape shape)
		{
			var renderer = _particles.GetComponent<ParticleSystemRenderer>();
			renderer.sharedMaterial = ParticleMaterial();

			switch (shape)
			{
				case ParticleShape.Cube:
					renderer.renderMode = ParticleSystemRenderMode.Mesh;
					renderer.mesh = BuiltinMesh(ref _cubeMesh, PrimitiveType.Cube);
					break;
				case ParticleShape.Sphere:
					renderer.renderMode = ParticleSystemRenderMode.Mesh;
					renderer.mesh = BuiltinMesh(ref _sphereMesh, PrimitiveType.Sphere);
					break;
				default:
					renderer.renderMode = ParticleSystemRenderMode.Billboard;
					break;
			}
		}

		// A vertex-coloured unlit URP particle material, shared across every burst. Falls back gracefully if the
		// URP particle shader isn't found (e.g. a stripped device build without it in Always Included Shaders).
		private static Material? ParticleMaterial()
		{
			if (_particleMaterial != null)
			{
				return _particleMaterial;
			}

			var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
			_particleMaterial = shader != null ? new Material(shader) : Resources.Load<Material>("Materials/Primitive");
			return _particleMaterial;
		}

		// Grabs a built-in primitive mesh by spawning a throwaway primitive and lifting its shared mesh (which
		// outlives the GameObject). DestroyImmediate when not playing so the edit-mode sandbox build can run too.
		private static Mesh BuiltinMesh(ref Mesh? cache, PrimitiveType type)
		{
			if (cache != null)
			{
				return cache;
			}

			var temp = GameObject.CreatePrimitive(type);
			var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
			cache = mesh;

#if UNITY_EDITOR
			if (Application.isPlaying)
			{
#endif
				Destroy(temp);
#if UNITY_EDITOR
			}
			else
			{
				DestroyImmediate(temp);
			}
#endif
			return mesh;
		}
	}
}
