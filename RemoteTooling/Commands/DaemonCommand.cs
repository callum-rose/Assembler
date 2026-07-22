using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>daemon</c> — poll the store repo's open issues labelled <c>generate</c> and fulfil each via publish.
/// On pick-up it comments to say it's started; on success it comments the outcome (plus any behaviour-catalogue
/// feedback the generator volunteered) and closes the issue; on failure it comments why and drops the label so
/// the issue stays open for inspection without being retried. Single-flight: a second daemon on the same
/// machine exits immediately.
/// </summary>
public static class DaemonCommand
{
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

		FileStream lockFile;
		try
		{
			lockFile = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
		}
		catch (IOException)
		{
			DaemonLog($"another daemon holds {lockPath} — exiting");
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
				lockFile.Dispose();
				File.Delete(lockPath);
			}
			catch { /* best-effort */ }
		}

		// Release the single-flight lock on graceful shutdown (Ctrl-C, or `launchctl unload`'s SIGTERM)
		// so a KeepAlive restart isn't locked out by a stale lock.
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

		DaemonLog($"generation daemon started — repo={repo} label={label} poll={pollSeconds}s");

		while (true)
		{
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

			// Prefer a non-empty body as the brief; fall back to the title.
			var brief = string.IsNullOrWhiteSpace(body) ? title : body;
			Fulfil(repo, label, number, brief);
		}
	}

	private static void Fulfil(string repo, string label, int number, string brief)
	{
		DaemonLog($"fulfilling #{number}: {brief}");
		Comment(repo, number, "🛠️ The generation daemon has picked up this task and is generating the game now. "
			+ "I'll comment again with the result.");

		var captured = new StringBuilder();
		var log = new Logger(captured);
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
			Comment(repo, number, $"❌ Generation failed:\n```\n{Tail(captured.ToString(), 20)}\n```");
			// Leave the issue open (drop the label) so it isn't retried every poll.
			Proc.Capture("gh", ["api", "-X", "DELETE", $"repos/{repo}/issues/{number}/labels/{label}"]);
		}
	}

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
