using UnityEngine;

namespace Assembler.Time
{
	/// <summary>
	/// Ticks the shared <see cref="RealtimeGameClock"/> once per frame, before any behaviour reads it.
	/// </summary>
	/// <remarks>
	/// <see cref="DefaultExecutionOrderAttribute"/> with a large negative order guarantees this
	/// <see cref="Update"/> runs ahead of every gameplay behaviour's <c>Update</c>; Unity's default
	/// Update order is otherwise undefined and hierarchy parenting does not constrain it.
	/// </remarks>
	[DefaultExecutionOrder(-10000)]
	public sealed class GameClockDriver : MonoBehaviour
	{
		public RealtimeGameClock Clock { get; set; } = new RealtimeGameClock();

		private void Update() => Clock.Tick();
	}
}
