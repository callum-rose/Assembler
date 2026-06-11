using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Unity.Cinemachine;
using UnityEngine;

namespace Assembler.Behaviours.Camera
{
	/// <summary>Emits a one-shot Cinemachine impulse when Executed (typically from a collision or other trigger),
	/// shaking every <c>camera</c> in range. Lives on any entity — no virtual camera required — and pairs with the
	/// <c>CinemachineImpulseListener</c> the <c>camera</c> behaviour already adds.</summary>
	/// <remarks>
	/// The impulse is a presentation-only signal: it moves the camera via the brain on real frame time and never
	/// feeds back into game logic. <c>Force</c> is re-read each time the behaviour fires, so it can be bound to an
	/// expression (e.g. scale shake to impact speed). <c>Duration</c> and <c>Velocity</c> are applied once at build.
	/// Properties:
	///   Force: Impulse amplitude scalar (default 1). Read on every fire, so it may be an expression/variable.
	///   Duration: How long the shake lasts in seconds (default Cinemachine's 0.2). Applied at build.
	///   Velocity [Vector3]: Direction and base magnitude of the kick (default (0,-1,0), i.e. downward). Scaled by Force.
	/// </remarks>
	public sealed class CameraShake : GameBehaviour<CameraShakeData>
	{
		private CinemachineImpulseSource _source = null!;

		protected override void OnInitialise(CameraShakeData data)
		{
			_source = gameObject.AddComponent<CinemachineImpulseSource>();
			data.Duration.UseIfValueExists(d => _source.ImpulseDefinition.ImpulseDuration = d);
			data.Velocity.UseIfValueExists(v => _source.DefaultVelocity = v);
		}

		public override void Execute(TriggerContext ctx) =>
			_source.GenerateImpulseWithForce(Data.Force.ValueOr(ctx, 1f));
	}
}
