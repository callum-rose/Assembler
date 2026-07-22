using System.Text;

namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>publish "&lt;brief&gt;" | path/to/descriptor.yaml [game-id]</c> — obtain a descriptor (generate one
/// from a brief, or take an existing file), validate that it builds, then commit + push it to the store
/// repo and upsert <c>manifest.json</c>. If validation fails, the validator's report is fed back to the
/// generator to fix and the result re-validated — looping until the descriptor builds cleanly (there is no
/// attempt cap; only a generator that emits nothing at all is terminal).
/// </summary>
public static class PublishCommand
{
	// Generation (the multi-minute claude run) is safe to overlap, but validation and the git publish are
	// not: two Unity batch builds on one project corrupt its Library, and two writers race the store's
	// working tree + manifest.json. So the daemon may call Publish from several threads at once, but the
	// build-and-publish half of each call is funnelled through this single gate. Generation happens before
	// it's taken, so N briefs still generate concurrently and only serialise to validate + push.
	private static readonly object BuildPublishGate = new();

	public static int Run(IReadOnlyList<string> args)
	{
		var log = new Logger();
		try
		{
			var outcome = Publish(
				args.Count > 0 ? args[0] : null,
				args.Count > 1 ? args[1] : null,
				log);
			Console.Out.WriteLine(outcome.Id); // the id is the payload — daemon/refine read it from stdout
			return 0;
		}
		catch (AppException ex)
		{
			log.Err(ex.Message);
			return 1;
		}
	}

	/// <summary>
	/// Publish a descriptor and return its game id plus any behaviour-catalogue feedback the generator
	/// volunteered. Reused in-process by <c>refine</c> and the daemon. Throws <see cref="AppException"/>
	/// on any failure (leaving a failed-validation descriptor on disk).
	/// </summary>
	public static PublishOutcome Publish(string? input, string? forcedId, Logger log)
	{
		if (string.IsNullOrEmpty(input))
		{
			throw new AppException("usage: publish \"<brief>\" | path/to/descriptor.yaml [game-id]");
		}

		if (!Proc.Which("gh"))
		{
			throw new AppException("gh is required (brew install gh; gh auth login)");
		}

		var storeDir = Config.StoreDir();
		if (!Directory.Exists(Path.Combine(storeDir, ".git")))
		{
			throw new AppException($"store repo not found at {storeDir} — run `assembler-remote setup` first");
		}

		var engineDir = Config.EngineDir();
		var validateScript = Path.Combine(engineDir, "Tools", "validate-game.sh");
		if (!IsExecutable(validateScript))
		{
			throw new AppException($"validate-game.sh not found (or not executable) under {engineDir}/Tools");
		}

		var work = Directory.CreateTempSubdirectory("assembler-remote-");
		var keepWork = false;
		try
		{
			var descriptor = Path.Combine(work.FullName, "descriptor.yaml");

			string title;
			string? feedback = null;
			if (File.Exists(input))
			{
				File.Copy(input, descriptor);
				title = Path.GetFileNameWithoutExtension(input);
				log.Info($"Using existing descriptor: {input}");
			}
			else
			{
				title = input;
				log.Info($"Generating descriptor for: {input}");
				var generated = GameGenerator.GenerateNew(input, log);
				File.WriteAllText(descriptor, generated.Descriptor);
				feedback = generated.Feedback;
				if (new FileInfo(descriptor).Length == 0)
				{
					throw new AppException("generation produced an empty descriptor");
				}
			}

			var id = !string.IsNullOrEmpty(forcedId) ? forcedId : Store.Slugify(title);
			if (string.IsNullOrEmpty(id))
			{
				throw new AppException($"could not derive a game id from '{title}' — pass one explicitly");
			}

			// Validate, and on failure feed the report back to the generator to fix, then re-validate — the
			// same way the skill is driven by hand. The loop keeps going until the descriptor builds cleanly
			// (there is deliberately no attempt cap). Only validation and the git publish are the serial
			// critical section (see BuildPublishGate); the multi-minute fix run happens OUTSIDE the gate so it
			// overlaps other workers' generation instead of blocking them.
			for (var attempt = 1; ; attempt++)
			{
				string report;
				lock (BuildPublishGate)
				{
					log.Info($"Validating '{id}' (attempt {attempt}; booting Unity sandbox — this is slow)…");

					// Tee the validator's output into a buffer as well as the log, so a failure can be fed
					// back to the generator verbatim (the script prints only its concise per-stage report).
					var captured = new StringBuilder();
					var validationExit = Proc.Stream(validateScript, [descriptor], workingDir: null, env: null,
						line => { captured.AppendLine(line); log.Raw(line); });

					if (validationExit == 0)
					{
						var (owner, repo) = Store.OwnerRepo(storeDir, Config.StoreRemote);
						var branch = Config.StoreBranch;
						var version = Store.Version(descriptor);
						var url = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/games/{id}/descriptor.yaml";

						var gameDir = Path.Combine(storeDir, "games", id);
						Directory.CreateDirectory(gameDir);
						File.Copy(descriptor, Path.Combine(gameDir, "descriptor.yaml"), overwrite: true);

						Store.UpsertManifest(Path.Combine(storeDir, "manifest.json"), id, title, url, version);

						Proc.Run("git", ["-C", storeDir, "add", "-A"]);
						var commit = Proc.Capture("git", ["-C", storeDir, "commit", "-q", "-m", $"Publish {id} ({version})"]);
						if (commit.ExitCode != 0)
						{
							log.Info("nothing changed");
							return new PublishOutcome(id, feedback);
						}

						if (Proc.Run("git", ["-C", storeDir, "push", "-q", Config.StoreRemote, $"HEAD:{branch}"]) != 0)
						{
							throw new AppException($"git push to {Config.StoreRemote} {branch} failed");
						}

						log.Info($"Published '{id}' v{version} → {url}");
						return new PublishOutcome(id, feedback);
					}

					report = captured.ToString();
				}

				// Validation failed and the gate is released. Hand the report and current descriptor back to
				// the generator, write its fix over the working descriptor, and re-validate on the next loop.
				// The only terminal failure is the generator producing nothing at all — an empty descriptor
				// can't be validated or fixed, so retrying it would just spin.
				log.Info($"Validation failed — asking the generator to fix it (attempt {attempt + 1})…");
				var fixResult = GameGenerator.Fix(TailReport(report), File.ReadAllText(descriptor), log);
				if (string.IsNullOrWhiteSpace(fixResult.Descriptor))
				{
					keepWork = true;
					throw new AppException($"fix generation produced an empty descriptor — not publishing. Descriptor left at: {descriptor}");
				}

				File.WriteAllText(descriptor, fixResult.Descriptor);
				feedback = fixResult.Feedback ?? feedback; // keep the latest volunteered catalogue feedback
			}
		}
		finally
		{
			if (!keepWork)
			{
				try { work.Delete(recursive: true); }
				catch { /* best-effort temp cleanup */ }
			}
		}
	}

	// The validator prints a concise per-stage report, but guard against a pathologically long one before
	// feeding it to the generator: keep the tail, which is where the failing stage and its error land.
	private static string TailReport(string report, int maxLines = 200)
	{
		var lines = report.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
		return lines.Length <= maxLines ? report : string.Join('\n', lines[^maxLines..]);
	}

	private static bool IsExecutable(string path)
	{
		if (!File.Exists(path))
		{
			return false;
		}

		// This tool runs on the dev Mac; the exec-bit check is Unix-only. Elsewhere, existence is enough.
		if (OperatingSystem.IsWindows())
		{
			return true;
		}

		return (File.GetUnixFileMode(path)
			& (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
	}
}

/// <summary>A published game's id plus any behaviour-catalogue feedback the generator volunteered
/// (null when publishing an existing descriptor file rather than generating from a brief).</summary>
public sealed record PublishOutcome(string Id, string? Feedback);
