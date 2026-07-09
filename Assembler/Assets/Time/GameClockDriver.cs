using UnityEngine;

namespace Assembler.Time
{
	/// <summary>
	/// Ticks the shared game clock once per frame, before any behaviour reads it, and owns the
	/// <see cref="UnityEngine.Time.captureDeltaTime"/> global for the game's lifetime.
	/// </summary>
	/// <remarks>
	/// <see cref="DefaultExecutionOrderAttribute"/> with a large negative order guarantees this
	/// <see cref="Update"/> runs ahead of every gameplay behaviour's <c>Update</c>; Unity's default
	/// Update order is otherwise undefined and hierarchy parenting does not constrain it.
	///
	/// A fixed-step clock reports a non-zero <see cref="IAdvancingGameClock.CaptureDeltaTime"/>, which is
	/// written to <see cref="UnityEngine.Time.captureDeltaTime"/> in <see cref="OnEnable"/> so Unity's frame
	/// cadence and physics accumulator advance by that constant step. Because that global outlives the play
	/// session (it would otherwise freeze the editor at the capture rate), it is always restored to <c>0</c> in
	/// <see cref="OnDisable"/> — the realtime clock reports <c>0</c>, so the wall-clock path is unaffected.
	/// </remarks>
	[DefaultExecutionOrder(-10000)]
	public sealed class GameClockDriver : MonoBehaviour
	{
		public IAdvancingGameClock Clock { get; set; } = new RealtimeGameClock();

		private void OnEnable() => UnityEngine.Time.captureDeltaTime = Clock.CaptureDeltaTime;

		private void Update() => Clock.Tick();

		// Restore Unity's global time on teardown (or disable) so a fixed-step run never leaks its capture
		// cadence into the editor or a subsequent game. Idempotent for the realtime clock (0 → 0).
		private void OnDisable() => UnityEngine.Time.captureDeltaTime = 0f;
	}
}
