using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assembler.RemoteTooling;

/// <summary>
/// A snapshot the running daemon writes so a separate <c>status</c> invocation can see what it's doing.
/// The daemon rewrites it on every poll (a heartbeat) and whenever a job changes phase; <c>status</c>
/// only reads it. It lives next to the single-flight lock in the temp dir — purely ephemeral runtime
/// state, recreated on the next poll, so losing it (reboot, temp cleanup) costs nothing.
/// </summary>
public sealed record DaemonState
{
	public int Pid { get; init; }
	public string Repo { get; init; } = "";
	public string Label { get; init; } = "";
	public int PollSeconds { get; init; }
	public DateTimeOffset StartedAt { get; init; }

	/// <summary>Updated every poll; how <c>status</c> tells a live daemon from a stale state file.</summary>
	public DateTimeOffset LastPollAt { get; init; }

	public int PublishedCount { get; init; }
	public int FailedCount { get; init; }

	/// <summary>The job in flight right now, or <c>null</c> when the daemon is idle between polls.</summary>
	public DaemonJob? Current { get; init; }

	/// <summary>The most recently finished job (published or failed), for a bit of recent history.</summary>
	public DaemonJob? LastFinished { get; init; }

	/// <summary>Path of the state file — a sibling of the daemon's single-flight lock.</summary>
	public static string Path { get; } =
		System.IO.Path.Combine(System.IO.Path.GetTempPath(), "assembler-generation-daemon.state.json");

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>
	/// Write atomically (temp file + move) so a concurrent <see cref="TryRead"/> never sees a half-written
	/// file. Best-effort: a failure to persist status must never take the daemon down.
	/// </summary>
	public void Write()
	{
		try
		{
			var tmp = Path + ".tmp";
			File.WriteAllText(tmp, JsonSerializer.Serialize(this, JsonOptions));
			File.Move(tmp, Path, overwrite: true);
		}
		catch { /* status is a convenience; never let it break the daemon */ }
	}

	/// <summary>Read the last-written state, or <c>null</c> if no daemon has written one (or it's unreadable).</summary>
	public static DaemonState? TryRead()
	{
		try
		{
			return File.Exists(Path)
				? JsonSerializer.Deserialize<DaemonState>(File.ReadAllText(Path), JsonOptions)
				: null;
		}
		catch
		{
			return null;
		}
	}
}

/// <summary>One generation job the daemon is (or was) fulfilling: the issue it came from, the brief, and
/// which phase of publish it reached.</summary>
public sealed record DaemonJob(
	int Issue,
	string Brief,
	DaemonPhase Phase,
	DateTimeOffset StartedAt)
{
	/// <summary>Set on <see cref="DaemonState.LastFinished"/> once the job ends; null while it's in flight.</summary>
	public DateTimeOffset? FinishedAt { get; init; }

	/// <summary>The published game id, when the job succeeded.</summary>
	public string? PublishedId { get; init; }
}

/// <summary>Coarse phases a job passes through, derived from the publish pipeline's own progress logging.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DaemonPhase
{
	Picked,
	Generating,
	Validating,
	Publishing,
	Published,
	Failed,
}
