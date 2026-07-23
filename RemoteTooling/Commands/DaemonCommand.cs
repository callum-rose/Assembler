using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>daemon</c> — poll the store repo's open issues labelled <c>generate</c> and fulfil each via publish.
/// On pick-up it comments to say it's started; on success it comments the outcome (plus any behaviour-catalogue
/// feedback the generator volunteered) and closes the issue. Publish itself validates the generated descriptor
/// and, when validation fails, feeds the validator's report back to the generator to fix and re-validates —
/// looping until it builds cleanly, the way the skill is driven by hand. Only a hard failure (e.g. the
/// generator emitting nothing) ends the job: it then comments why and drops the label, leaving the issue open
/// for inspection without being retried every poll. Single-flight: a second daemon on the same machine exits
/// immediately.
///
/// Throughout, it writes a heartbeat/progress snapshot to <see cref="DaemonState"/> so a separate
/// <c>status</c> invocation can report what it's generating right now.
/// </summary>
public static class DaemonCommand
{
	// Guards the shared state below: the publish pipeline streams its progress from a background thread
	// (via the Logger's onInfo hook) while the poll thread also reads/writes, so mutations are serialised.
	private static readonly object StateGate = new();

	// Caps how many issues are fulfilled at once. Only their generation stages truly overlap; validation
	// and publish still serialise inside PublishCommand, so this mainly bounds concurrent `claude` runs.
	private static readonly SemaphoreSlim Slots = new(Config.MaxConcurrent, Config.MaxConcurrent);

	// Issue numbers currently being worked (queued or running). A poll skips any already here so a job
	// that spans several poll cycles isn't picked up a second time and generated twice.
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> InFlight = new();

	// Non-blocking exclusive advisory lock (Unix only). The kernel releases it automatically when the fd
	// closes or the owning process dies — including a SIGKILL/crash/reboot that never runs our cleanup —
	// so a dead daemon can never wedge future launchd restarts.
	[DllImport("libc", SetLastError = true)]
	private static extern int flock(int fd, int operation);

	private const int LOCK_EX = 2; // exclusive
	private const int LOCK_NB = 4; // fail instead of blocking if already held

	// Take the exclusive advisory lock for the lifetime of the process. Returns 0 on success.
	private static int FlockExclusiveNonBlocking(FileStream file)
	{
		var fd = (int)file.SafeFileHandle.DangerousGetHandle();
		return flock(fd, LOCK_EX | LOCK_NB);
	}

	public static int Run(IReadOnlyList<string> args)
	{
		var repo = Config.StoreRepo;
		if (string.IsNullOrEmpty(repo))
		{
			DaemonLog("ERROR: set ASSEMBLER_STORE_REPO=owner/repo");
			return 1;
		}

		if (!Proc.Which("gh"))
		{
			DaemonLog("ERROR: gh not found");
			return 1;
		}

		var label = Config.GenLabel;
		var pollSeconds = Config.PollSeconds;
		var lockPath = Path.Combine(Path.GetTempPath(), "assembler-generation-daemon.lock");

		// Single-flight guard. It must survive a hard kill (SIGKILL, crash, reboot) without wedging every
		// future launchd restart, so we rely on the OS dropping the lock when the holder dies rather than on
		// a file we have to remember to delete. The lock file itself is allowed to persist between runs — its
		// mere existence means nothing; only a live holder of the OS lock counts.
		//   • Windows: an exclusive FileShare.None handle the OS releases on process death.
		//   • Unix: an advisory flock() the kernel drops when the fd closes or the process exits.
		FileStream lockFile;
		try
		{
			lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
		}
		catch (IOException)
		{
			// Windows: another live daemon holds the handle exclusively. (A dead holder's handle is already
			// closed by the OS, so this open would have succeeded.)
			DaemonLog($"another daemon holds {lockPath} — exiting");
			return 0;
		}

		// Unix: FileShare.None isn't enforced across processes, so arbitrate with an advisory flock. A dead
		// holder's lock is already gone, so a non-zero return means a genuinely-live daemon owns it.
		if (!OperatingSystem.IsWindows() && FlockExclusiveNonBlocking(lockFile) != 0)
		{
			DaemonLog($"another daemon holds {lockPath} — exiting");
			lockFile.Dispose();
			return 0;
		}

		var released = 0;
		void ReleaseLock()
		{
			if (Interlocked.Exchange(ref released, 1) != 0)
			{
				return; // release exactly once, whichever handler fires first
			}

			try
			{
				lockFile.Dispose(); // drops the flock / releases the exclusive handle; the file may remain
				File.Delete(DaemonState.Path); // a stopped daemon has no live status
			}
			catch { /* best-effort */ }
		}

		// Promptly clear the live-status snapshot on graceful shutdown (Ctrl-C, or `launchctl unload`'s
		// SIGTERM). The OS lock itself is released automatically when the process exits — a hard kill that
		// skips this handler is fine, unlike the old delete-the-lock-file scheme it replaces.
		AppDomain.CurrentDomain.ProcessExit += (_, _) => ReleaseLock();
		var signals = new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM, PosixSignal.SIGQUIT }
			.Select(signal => PosixSignalRegistration.Create(signal, ctx =>
			{
				ctx.Cancel = true;
				ReleaseLock();
				Environment.Exit(0);
			}))
			.ToList();
		_ = signals; // keep the registrations alive for the lifetime of the process

		var now = DateTimeOffset.Now;
		_state = new DaemonState
		{
			Pid = Environment.ProcessId,
			Repo = repo,
			Label = label,
			PollSeconds = pollSeconds,
			StartedAt = now,
			LastPollAt = now,
		};
		WriteState(s => s);

		DaemonLog($"generation daemon v{BuildInfo.Version} started — repo={repo} label={label} "
		+ $"poll={pollSeconds}s maxConcurrent={Config.MaxConcurrent}");

		while (true)
		{
			WriteState(s => s with { LastPollAt = DateTimeOffset.Now }); // heartbeat

			try
			{
				PollOnce(repo, label);
			}
			catch (Exception ex)
			{
				DaemonLog($"poll error: {ex.Message}");
			}

			Thread.Sleep(TimeSpan.FromSeconds(pollSeconds));
		}
	}

	// The live snapshot; mutated only through WriteState under StateGate.
	private static DaemonState _state = new();

	/// <summary>Apply <paramref name="update"/> to the current state and persist it, atomically.</summary>
	private static void WriteState(Func<DaemonState, DaemonState> update)
	{
		lock (StateGate)
		{
			_state = update(_state);
			_state.Write();
		}
	}

	/// <summary>Replace the in-flight job for <paramref name="issue"/> via <paramref name="update"/>, leaving
	/// every other worker's job untouched.</summary>
	private static void UpdateJob(int issue, Func<DaemonJob, DaemonJob> update) => WriteState(s => s with
	{
		Current = s.Current.Select(j => j.Issue == issue ? update(j) : j).ToArray(),
	});

	/// <summary>The in-flight list without the job for <paramref name="issue"/>.</summary>
	private static IReadOnlyList<DaemonJob> Remove(IReadOnlyList<DaemonJob> jobs, int issue) =>
		jobs.Where(j => j.Issue != issue).ToArray();

	private static void PollOnce(string repo, string label)
	{
		var response = Proc.Capture("gh", ["api", $"repos/{repo}/issues?state=open&labels={label}&per_page=20"]);
		if (response.ExitCode != 0 || string.IsNullOrWhiteSpace(response.StdOut))
		{
			return;
		}

		using var doc = JsonDocument.Parse(response.StdOut);
		foreach (var issue in doc.RootElement.EnumerateArray())
		{
			// A "pull_request" member marks a PR, which the issues endpoint also returns — skip those.
			if (issue.TryGetProperty("pull_request", out var pr) && pr.ValueKind != JsonValueKind.Null)
			{
				continue;
			}

			var number = issue.GetProperty("number").GetInt32();
			var title = issue.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
			var body = issue.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String
				? b.GetString() ?? ""
				: "";

			// Feed both the title and the body to the generator. The title often carries the game's name
			// or core hook that the body then assumes, so dropping it loses part of the intent. Combine
			// them when both are present; otherwise use whichever one the issue actually has.
			var brief = (string.IsNullOrWhiteSpace(title), string.IsNullOrWhiteSpace(body)) switch
			{
				(false, false) => $"{title.Trim()}\n\n{body.Trim()}",
				(false, true) => title.Trim(),
				(true, false) => body.Trim(),
				_ => "",
			};

			// Claim the issue before launching so a later poll (this generation can outlast several)
			// doesn't pick it up again; the claim is released when the worker finishes.
			if (!InFlight.TryAdd(number, 0))
			{
				continue;
			}

			_ = Task.Run(() => Worker(repo, label, number, brief));
		}
	}

	// Fulfil one issue on its own thread, bounded by Slots so at most MaxConcurrent run at once. Never
	// throws: a worker that died must still release its slot and its in-flight claim.
	private static void Worker(string repo, string label, int number, string brief)
	{
		Slots.Wait();
		try
		{
			Fulfil(repo, label, number, brief);
		}
		catch (Exception ex)
		{
			DaemonLog($"worker error #{number}: {ex.Message}");
		}
		finally
		{
			Slots.Release();
			InFlight.TryRemove(number, out _);
		}
	}

	private static void Fulfil(string repo, string label, int number, string brief)
	{
		DaemonLog($"fulfilling #{number}: {brief}");
		var job = new DaemonJob(number, brief, DaemonPhase.Picked, DateTimeOffset.Now);
		WriteState(s => s with { Current = [.. s.Current, job] });

		Comment(repo, number, "🛠️ The generation daemon has picked up this task and is generating the game now. "
			+ "I'll comment again with the result.");

		var captured = new StringBuilder();
		// Derive the coarse phase from the publish pipeline's own progress logging so status can show it live.
		// Update only this issue's job — other workers' jobs share the Current list.
		var log = new Logger(captured, onInfo: line => UpdateJob(number, j => j with { Phase = PhaseFrom(line, j.Phase) }));
		PublishOutcome? outcome = null;
		try
		{
			outcome = PublishCommand.Publish(brief, forcedId: null, log);
		}
		catch (AppException ex)
		{
			log.Err(ex.Message);
		}
		catch (Exception ex)
		{
			log.Err(ex.ToString());
		}

		if (outcome is not null)
		{
			DaemonLog($"published '{outcome.Id}' for #{number}");
			WriteState(s => s with
			{
				Current = Remove(s.Current, number),
				PublishedCount = s.PublishedCount + 1,
				LastFinished = job with
				{
					Phase = DaemonPhase.Published,
					FinishedAt = DateTimeOffset.Now,
					PublishedId = outcome.Id,
				},
			});

			var body = new StringBuilder($"✅ Published `{outcome.Id}`. It should appear on the shelf shortly.");
			if (!string.IsNullOrWhiteSpace(outcome.Feedback))
			{
				body.Append($"\n\n**Generator feedback on the behaviour catalogue:**\n\n{outcome.Feedback}");
			}

			Comment(repo, number, body.ToString());
			Proc.Capture("gh", ["api", "-X", "PATCH", $"repos/{repo}/issues/{number}", "-f", "state=closed"]);
		}
		else
		{
			DaemonLog($"FAILED #{number}");
			WriteState(s => s with
			{
				Current = Remove(s.Current, number),
				FailedCount = s.FailedCount + 1,
				LastFinished = job with { Phase = DaemonPhase.Failed, FinishedAt = DateTimeOffset.Now },
			});

			Comment(repo, number, $"❌ Generation failed:\n```\n{Tail(captured.ToString(), 20)}\n```");
			// Leave the issue open (drop the label) so it isn't retried every poll.
			Proc.Capture("gh", ["api", "-X", "DELETE", $"repos/{repo}/issues/{number}/labels/{label}"]);
		}
	}

	// Map a publish progress line to the phase it announces. Unrecognised lines keep the current phase,
	// so incidental logging never regresses what status shows.
	private static DaemonPhase PhaseFrom(string line, DaemonPhase current) => line switch
	{
		_ when line.StartsWith("Generating descriptor", StringComparison.Ordinal) => DaemonPhase.Generating,
		_ when line.StartsWith("Validation failed — asking the generator to fix", StringComparison.Ordinal) => DaemonPhase.Fixing,
		_ when line.StartsWith("Validating", StringComparison.Ordinal) => DaemonPhase.Validating,
		_ when line.StartsWith("Published", StringComparison.Ordinal) => DaemonPhase.Publishing,
		_ => current,
	};

	private static void Comment(string repo, int number, string body) =>
		Proc.Capture("gh", ["api", $"repos/{repo}/issues/{number}/comments", "-f", $"body={body}"]);

	private static string Tail(string text, int lines)
	{
		var all = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
		return string.Join('\n', all.Length <= lines ? all : all[^lines..]);
	}

	private static void DaemonLog(string message) =>
		Console.WriteLine($"{DateTime.Now:yyyy-MM-ddTHH:mm:ss} {message}");
}
