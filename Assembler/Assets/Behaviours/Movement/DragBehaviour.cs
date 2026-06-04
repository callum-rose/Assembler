using System;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Exponentially decays a shared velocity variable each frame, modelling linear drag.</summary>
	/// <remarks>
	/// Layers onto the shared-velocity system: it reads and rewrites the same <c>!var velocity</c> that an
	/// <c>acceleration</c> feeds and a <c>velocity</c> integrator consumes. Decay is
	/// <c>velocity *= exp(-Coefficient * deltaTime)</c> — unconditionally stable and frame-rate independent.
	/// Properties:
	///   Velocity [Vector3]: Writable shared velocity variable to decay (required, e.g. !var velocity).
	///   Coefficient: Drag rate per second; larger values bleed speed off faster.
	/// </remarks>
	public class DragBehaviour : GameBehaviour<DragData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		protected override void OnInitialise(DragData data)
		{
			if (data.Velocity is NullValueProvider<Vector3>)
			{
				throw new InvalidOperationException(
					$"drag behaviour '{data.Id}' requires a writable Velocity variable, e.g. !var velocity.");
			}
		}

		private void Update()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			var decay = Mathf.Exp(-Data.Coefficient.Get(ctx) * Clock.DeltaTime);
			Data.Velocity.Set(Data.Velocity.Get(ctx) * decay);
		}
	}
}
