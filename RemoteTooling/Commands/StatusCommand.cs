using System.Text.Json;

namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>status</c> — report what the generation daemon is doing right now: whether it's alive, the job in
/// flight (which issue, brief, and phase), the queue of open <c>generate</c>-labelled issues still waiting,
/// the last finished job, and running totals. Reads the daemon's <see cref="DaemonState"/> heartbeat file
/// for the live picture and queries GitHub for the queue. Pass <c>--json</c> for machine-readable output.
/// </summary>
public static class StatusCommand
{
	public static int Run(IReadOnlyList<string> args)
	{
		var json = args.Contains("--json");

		var state = DaemonState.TryRead();
		var repo = state?.Repo is { Length: > 0 } r ? r : Config.StoreRepo;
		var label = state?.Label is { Length: > 0 } l ? l : Config.GenLabel;
		var queue = FetchQueue(repo, label);

		if (json)
		{
			return PrintJson(state, queue);
		}

		return PrintHuman(state, repo, label, queue);
	}

	private static int PrintHuman(DaemonState? state, string? repo, string label, IReadOnlyList<QueueItem> queue)
	{
		var now = DateTimeOffset.Now;

		if (state is null)
		{
			Console.WriteLine("Generation daemon: ○ no status file — the daemon hasn't run on this machine (or was cleaned up).");
		}
		else
		{
			var alive = IsAlive(state, now);
			var upFor = Ago(state.StartedAt, now);
			Console.WriteLine(alive
				? $"Generation daemon: ● running (pid {state.Pid}, up {upFor})"
				: $"Generation daemon: ○ not running — stale status (pid {state.Pid}, last seen {Ago(state.LastPollAt, now)} ago)");
			Console.WriteLine($"  repo {repo ?? "?"}   label {label}   poll {state.PollSeconds}s");
			Console.WriteLine($"  last poll {Ago(state.LastPollAt, now)} ago");
			Console.WriteLine($"  totals   {state.PublishedCount} published   {state.FailedCount} failed");

			Console.WriteLine();
			if (state.Current.Count > 0)
			{
				Console.WriteLine($"Now generating ({state.Current.Count} in flight):");
				foreach (var cur in state.Current.OrderBy(c => c.Issue))
				{
					Console.WriteLine($"  #{cur.Issue}  \"{Trim(cur.Brief, 70)}\"   phase: {cur.Phase.ToString().ToLowerInvariant()}   ({Ago(cur.StartedAt, now)})");
				}
			}
			else
			{
				Console.WriteLine("Now generating: (idle — nothing in flight)");
			}
		}

		Console.WriteLine();
		if (repo is null)
		{
			Console.WriteLine("Queue: (set ASSEMBLER_STORE_REPO to list open 'generate' issues)");
		}
		else if (queue.Count == 0)
		{
			Console.WriteLine($"Queue: no open '{label}' issues.");
		}
		else
		{
			Console.WriteLine($"Queue ({queue.Count} open '{label}' issue{(queue.Count == 1 ? "" : "s")}):");
			var generating = (state?.Current ?? []).Select(c => c.Issue).ToHashSet();
			foreach (var item in queue)
			{
				var marker = generating.Contains(item.Number) ? "→ generating now" : $"queued {Ago(item.CreatedAt, now)} ago";
				Console.WriteLine($"  #{item.Number,-5} {Trim(item.Title, 56),-56}  {marker}");
			}
		}

		if (state?.LastFinished is { } last)
		{
			Console.WriteLine();
			var result = last.Phase == DaemonPhase.Published
				? $"✅ published '{last.PublishedId}'"
				: "❌ failed";
			var when = last.FinishedAt is { } f ? $"{Ago(f, now)} ago" : "";
			Console.WriteLine($"Last finished:  #{last.Issue}  {result}   {when}");
		}

		return 0;
	}

	private static int PrintJson(DaemonState? state, IReadOnlyList<QueueItem> queue)
	{
		var now = DateTimeOffset.Now;
		var payload = new
		{
			running = state is not null && IsAlive(state, now),
			daemon = state,
			queue = queue.Select(q => new { q.Number, q.Title, createdAt = q.CreatedAt }),
		};

		Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions
		{
			WriteIndented = true,
			DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
		}));
		return 0;
	}

	// A daemon is live if its process still exists; if we can't tell (e.g. it's on another machine), fall
	// back to a recent heartbeat — three missed polls means it's almost certainly gone.
	private static bool IsAlive(DaemonState state, DateTimeOffset now)
	{
		if (ProcessExists(state.Pid))
		{
			return true;
		}

		var staleAfter = TimeSpan.FromSeconds(Math.Max(state.PollSeconds, 1) * 3 + 10);
		return now - state.LastPollAt < staleAfter;
	}

	private static bool ProcessExists(int pid)
	{
		if (pid <= 0)
		{
			return false;
		}

		try
		{
			using var _ = System.Diagnostics.Process.GetProcessById(pid);
			return true;
		}
		catch (ArgumentException)
		{
			return false; // no such process
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static IReadOnlyList<QueueItem> FetchQueue(string? repo, string label)
	{
		if (string.IsNullOrEmpty(repo) || !Proc.Which("gh"))
		{
			return [];
		}

		var response = Proc.Capture("gh", ["api", $"repos/{repo}/issues?state=open&labels={label}&per_page=50"]);
		if (response.ExitCode != 0 || string.IsNullOrWhiteSpace(response.StdOut))
		{
			return [];
		}

		var items = new List<QueueItem>();
		try
		{
			using var doc = JsonDocument.Parse(response.StdOut);
			foreach (var issue in doc.RootElement.EnumerateArray())
			{
				// The issues endpoint also returns PRs — skip those.
				if (issue.TryGetProperty("pull_request", out var pr) && pr.ValueKind != JsonValueKind.Null)
				{
					continue;
				}

				var number = issue.GetProperty("number").GetInt32();
				var title = issue.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
				var created = issue.TryGetProperty("created_at", out var c) && c.ValueKind == JsonValueKind.String
					&& DateTimeOffset.TryParse(c.GetString(), out var parsed)
					? parsed
					: DateTimeOffset.Now;
				items.Add(new QueueItem(number, title, created));
			}
		}
		catch (JsonException)
		{
			return [];
		}

		return items;
	}

	private static string Ago(DateTimeOffset then, DateTimeOffset now)
	{
		var span = now - then;
		if (span < TimeSpan.Zero)
		{
			span = TimeSpan.Zero;
		}

		if (span.TotalSeconds < 60)
		{
			return $"{(int)span.TotalSeconds}s";
		}

		if (span.TotalMinutes < 60)
		{
			return $"{(int)span.TotalMinutes}m{span.Seconds:D2}s";
		}

		if (span.TotalHours < 24)
		{
			return $"{(int)span.TotalHours}h{span.Minutes:D2}m";
		}

		return $"{(int)span.TotalDays}d{span.Hours:D2}h";
	}

	private static string Trim(string text, int max)
	{
		var oneLine = text.ReplaceLineEndings(" ").Trim();
		return oneLine.Length <= max ? oneLine : oneLine[..(max - 1)] + "…";
	}

	private sealed record QueueItem(int Number, string Title, DateTimeOffset CreatedAt);
}
