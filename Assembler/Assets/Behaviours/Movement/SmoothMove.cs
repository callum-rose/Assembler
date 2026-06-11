using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Movement
{
	/// <summary>Eases the entity toward <c>Target</c> with a critically-damped spring (<c>Vector3.SmoothDamp</c>).</summary>
	/// <remarks>
	/// Carries an internal velocity between frames, so motion accelerates out of rest and decelerates into
	/// the target. <c>SmoothTime</c> is roughly the time to reach the target.
	/// Properties:
	///   Target: World-space position to ease toward.
	///   SmoothTime: Approximate time (seconds) to reach the target; larger is slower and softer.
	/// </remarks>
	public class SmoothMove : GameBehaviour<SmoothMoveData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private Vector3 _velocity;

		private void Update()
		{
			Step();
		}

		internal void Step()
		{
			var ctx = TriggerContext.Empty;
			transform.position = Vector3.SmoothDamp(
				transform.position, Data.Target.Get(ctx), ref _velocity,
				Data.SmoothTime.Get(ctx), Mathf.Infinity, Clock.DeltaTime);
		}
	}
}
