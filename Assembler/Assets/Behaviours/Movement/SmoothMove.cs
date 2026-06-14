using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
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
	public class SmoothMove : PerFrameBehaviour<SmoothMoveData>
	{
		private Vector3 _velocity;

		internal override void Step()
		{
			transform.position = Vector3.SmoothDamp(
				transform.position, Data.Target.Get(), ref _velocity,
				Data.SmoothTime.Get(), Mathf.Infinity, Clock.DeltaTime);
		}
	}
}
