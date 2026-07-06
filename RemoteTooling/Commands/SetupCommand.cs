namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>setup [repo-name]</c> — one-time creation of the store: a local clone seeded with an empty
/// <c>manifest.json</c>, the public GitHub repo behind it, and the <c>generate</c> issue label the
/// daemon listens on. Idempotent — safe to re-run.
/// </summary>
public static class SetupCommand
{
	public static int Run(IReadOnlyList<string> args)
	{
		var log = new Logger();
		try
		{
			var repoName = args.Count > 0 ? args[0] : "assembler-games";
			var storeDir = Config.StoreDir(repoName);
			var label = Config.GenLabel;

			if (!Proc.Which("gh"))
			{
				throw new AppException("gh is required (brew install gh; gh auth login)");
			}

			var whoami = Proc.Capture("gh", ["api", "user", "--jq", ".login"]);
			if (whoami.ExitCode != 0 || whoami.StdOut.Trim().Length == 0)
			{
				throw new AppException($"gh api user failed — are you logged in? {whoami.StdErr.Trim()}");
			}

			var owner = whoami.StdOut.Trim();

			if (Directory.Exists(Path.Combine(storeDir, ".git")))
			{
				log.Info($"store already exists at {storeDir} — skipping local init");
			}
			else
			{
				log.Info($"creating local store at {storeDir}");
				Directory.CreateDirectory(Path.Combine(storeDir, "games"));
				Proc.Run("git", ["-C", storeDir, "init", "-q", "-b", "main"]);
				File.WriteAllText(Path.Combine(storeDir, "manifest.json"), "{ \"version\": 1, \"games\": [] }\n");
				File.WriteAllText(Path.Combine(storeDir, "README.md"), StoreReadme(repoName, label));
				File.WriteAllText(Path.Combine(storeDir, "games", ".gitkeep"), "");
				Proc.Run("git", ["-C", storeDir, "add", "-A"]);
				Proc.Run("git", ["-C", storeDir, "commit", "-q", "-m", "Initialise game store"]);
			}

			// Create the GitHub repo if it doesn't exist, then ensure the remote and push.
			if (Proc.Capture("gh", ["repo", "view", $"{owner}/{repoName}"]).ExitCode == 0)
			{
				log.Info($"GitHub repo {owner}/{repoName} already exists");
				if (Proc.Capture("git", ["-C", storeDir, "remote", "get-url", "origin"]).ExitCode != 0)
				{
					Proc.Run("git", ["-C", storeDir, "remote", "add", "origin", $"https://github.com/{owner}/{repoName}.git"]);
				}
			}
			else
			{
				log.Info($"creating public GitHub repo {owner}/{repoName}");
				Proc.Run("gh", ["repo", "create", $"{owner}/{repoName}", "--public", "--source", storeDir, "--remote", "origin", "--push"]);
			}

			Proc.Run("git", ["-C", storeDir, "push", "-q", "-u", "origin", "main"]); // harmless if already pushed

			// Ensure the request label exists (ignore "already exists").
			Proc.Capture("gh", ["label", "create", label, "--repo", $"{owner}/{repoName}", "--description", "Assembler game generation request", "--color", "5319E7"]);

			var manifestUrl = $"https://raw.githubusercontent.com/{owner}/{repoName}/main/manifest.json";
			log.Info("Store ready.");
			Console.WriteLine();
			Console.WriteLine($"  Local store : {storeDir}");
			Console.WriteLine($"  GitHub repo : https://github.com/{owner}/{repoName}");
			Console.WriteLine($"  Manifest URL: {manifestUrl}");
			Console.WriteLine();
			Console.WriteLine("Next:");
			Console.WriteLine("  1. Set GameShelf._manifestUrl (Bootstrap scene) to the Manifest URL above.");
			Console.WriteLine("  2. Publish a game:  assembler-remote publish \"a simple dodge-the-blocks game\"");
			Console.WriteLine($"  3. Run the daemon:  ASSEMBLER_STORE_REPO={owner}/{repoName} assembler-remote daemon");
			return 0;
		}
		catch (AppException ex)
		{
			log.Err(ex.Message);
			return 1;
		}
	}

	private static string StoreReadme(string repoName, string label) =>
		$"""
        # {repoName}

        Remote game store for the Assembler player app. `manifest.json` is the shelf index; each game's
        YAML descriptor lives under `games/<id>/descriptor.yaml`. Published by the `assembler-remote`
        dev tool in the engine repo and served to the app over raw.githubusercontent.com.

        **Generate a game from anywhere:** open an issue labelled `{label}` — the title (or body) is the brief.
        The generation daemon on the dev Mac picks it up, builds the game, publishes it here, and closes the issue.
        """;
}
