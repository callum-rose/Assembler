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

		// Clear the carried SmoothDamp velocity so a pooled reuse eases out of rest rather than inheriting the
		// previous life's momentum.
		public override void OnReuse() => _velocity = Vector3.zero;

		private void Update() => Step();

		internal void Step()
		{
			transform.position = Vector3.SmoothDamp(
				transform.position, Data.Target.Get(), ref _velocity,
				Data.SmoothTime.Get(), Mathf.Infinity, Clock.DeltaTime);
		}
	}
}
