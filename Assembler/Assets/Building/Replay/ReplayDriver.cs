using UnityEngine;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Pumps the <see cref="ReplayPlayer"/> once per frame. Runs after the clock driver (-10000) but before any
	/// gameplay behaviour, so the recorded activations for the tick are injected before behaviours read them.
	/// Added to the game root only in replay mode.
	/// </summary>
	[DefaultExecutionOrder(-9999)]
	public sealed class ReplayDriver : MonoBehaviour
	{
		public ReplayPlayer Player { get; set; } = null!;

		private void Update() => Player.PlayTick();
	}
}
