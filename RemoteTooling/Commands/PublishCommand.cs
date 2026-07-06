namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>publish "&lt;brief&gt;" | path/to/descriptor.yaml [game-id]</c> — obtain a descriptor (generate one
/// from a brief, or take an existing file), validate that it builds, then commit + push it to the store
/// repo and upsert <c>manifest.json</c>.
/// </summary>
public static class PublishCommand
{
	public static int Run(IReadOnlyList<string> args)
	{
		var log = new Logger();
		try
		{
			var id = Publish(
				args.Count > 0 ? args[0] : null,
				args.Count > 1 ? args[1] : null,
				log);
			Console.Out.WriteLine(id); // the id is the payload — daemon/refine read it from stdout
			return 0;
		}
		catch (AppException ex)
		{
			log.Err(ex.Message);
			return 1;
		}
	}

	/// <summary>
	/// Publish a descriptor and return its game id. Reused in-process by <c>refine</c> and the daemon.
	/// Throws <see cref="AppException"/> on any failure (leaving a failed-validation descriptor on disk).
	/// </summary>
	public static string Publish(string? input, string? forcedId, Logger log)
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
				File.WriteAllText(descriptor, GameGenerator.GenerateNew(input));
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

			log.Info($"Validating '{id}' (booting Unity sandbox — this is slow)…");
			var validationExit = Proc.Stream(validateScript, [descriptor], workingDir: null, env: null, log.Raw);
			if (validationExit != 0)
			{
				keepWork = true; // keep the descriptor around so it can be inspected / refined
				throw new AppException($"validation failed — not publishing. Descriptor left at: {descriptor}");
			}

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
				return id;
			}

			if (Proc.Run("git", ["-C", storeDir, "push", "-q", Config.StoreRemote, $"HEAD:{branch}"]) != 0)
			{
				throw new AppException($"git push to {Config.StoreRemote} {branch} failed");
			}

			log.Info($"Published '{id}' v{version} → {url}");
			return id;
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
