using Assembler.Input;

namespace Assembler.Building
{
	/// <summary>How the game clock advances time. See the Determinism (Level 1) section in CLAUDE.md.</summary>
	public enum ClockMode
	{
		/// <summary>Wall-clock delta per frame (default, non-deterministic timing).</summary>
		Realtime,

		/// <summary>Constant delta per tick — required for deterministic replay.</summary>
		FixedStep
	}

	/// <summary>Record/replay mode for input at the trigger boundary.</summary>
	public enum ReplayMode
	{
		/// <summary>No recording or replay.</summary>
		Off,

		/// <summary>Capture per-tick input activations to a replay.</summary>
		Record,

		/// <summary>Drive input from a previously captured replay.</summary>
		Replay
	}

	/// <summary>
	/// Optional configuration for a build, threading determinism controls (seed, clock mode, replay) through
	/// <see cref="Builder.Build(Assembler.Parsing.Info.GameInfo, Assembler.Parsing.Controls.ControlsInfo, BuildOptions)"/>.
	/// <see cref="Default"/> reproduces the original realtime, unseeded, no-replay behaviour. Extended by later
	/// determinism phases (clock selection, record/replay wiring).
	/// </summary>
	public sealed record BuildOptions(
		InputPlatform? OverridePlatform = null,
		uint? RandomSeed = null,
		ClockMode Clock = ClockMode.Realtime,
		float FixedDeltaTime = 1f / 60f,
		ReplayMode Replay = ReplayMode.Off)
	{
		public static readonly BuildOptions Default = new();
	}
}
