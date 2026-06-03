using Assembler.Building.Replay;
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
	/// Optional configuration for a build, threading determinism controls (seed, clock mode, record/replay) through
	/// <see cref="Builder.Build(Assembler.Parsing.Info.GameInfo, Assembler.Parsing.Controls.ControlsInfo, BuildOptions)"/>.
	/// <see cref="Default"/> reproduces the original realtime, unseeded, no-replay behaviour.
	/// </summary>
	/// <remarks>
	/// In <see cref="ReplayMode.Record"/> mode, supply a <see cref="Recorder"/> to capture into; the builder
	/// initialises and wires it, and you call <c>Recorder.Build()</c> afterwards. In <see cref="ReplayMode.Replay"/>
	/// mode, supply a <see cref="Player"/> built from the recorded session; the builder validates its descriptor hash
	/// and forces the clock/seed to match the recording. Record/replay is only meaningful via the YAML-path build
	/// entry (which can compute the descriptor hash).
	/// </remarks>
	public sealed record BuildOptions(
		InputPlatform? OverridePlatform = null,
		uint? RandomSeed = null,
		ClockMode Clock = ClockMode.Realtime,
		float FixedDeltaTime = 1f / 60f,
		ReplayMode Replay = ReplayMode.Off,
		ReplayRecorder? Recorder = null,
		ReplayPlayer? Player = null)
	{
		public static readonly BuildOptions Default = new();
	}
}
