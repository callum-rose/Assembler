namespace Assembler.RemoteTooling;

/// <summary>
/// The env-var configuration surface, kept identical to the bash scripts it replaces
/// (see <c>RemoteTooling/README.md</c>).
/// </summary>
public static class Config
{
	public static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

	/// <summary>Local clone of the store repo. Setup defaults it to <c>~/Developer/&lt;repo-name&gt;</c>.</summary>
	public static string StoreDir(string repoName = "assembler-games") =>
		Env("ASSEMBLER_STORE_DIR") ?? Path.Combine(Home, "Developer", repoName);

	public static string StoreBranch => Env("ASSEMBLER_STORE_BRANCH") ?? "main";

	public static string StoreRemote => Env("ASSEMBLER_STORE_REMOTE") ?? "origin";

	public static string GenLabel => Env("ASSEMBLER_GEN_LABEL") ?? "generate";

	public static string ClaudeBin => Env("CLAUDE_CLI_PATH") ?? "claude";

	public static string? StoreRepo => Env("ASSEMBLER_STORE_REPO");

	public static int PollSeconds =>
		int.TryParse(Env("ASSEMBLER_POLL_SECONDS"), out var n) && n > 0 ? n : 30;

	/// <summary>The Unity project root that holds <c>Tools/validate-game.sh</c>.</summary>
	public static string EngineDir() =>
		Env("ASSEMBLER_ENGINE_DIR")
		?? AutoDetectEngineDir()
		?? throw new AppException(
			"could not locate the Unity project (looked for an ancestor with Assembler/Tools/validate-game.sh) — set ASSEMBLER_ENGINE_DIR");

	private static string? Env(string key)
	{
		var value = Environment.GetEnvironmentVariable(key);
		return string.IsNullOrEmpty(value) ? null : value;
	}

	// Walk up from the working directory and the app's own location looking for the Unity project.
	// The tooling lives at <repo>/RemoteTooling and the Unity project at <repo>/Assembler, so from
	// anywhere inside the repo we find <ancestor>/Assembler/Tools/validate-game.sh (or, if run from
	// inside the Unity project itself, <ancestor>/Tools/validate-game.sh).
	private static string? AutoDetectEngineDir()
	{
		foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
		{
			for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
			{
				if (File.Exists(Path.Combine(dir.FullName, "Tools", "validate-game.sh")))
				{
					return dir.FullName;
				}

				if (File.Exists(Path.Combine(dir.FullName, "Assembler", "Tools", "validate-game.sh")))
				{
					return Path.Combine(dir.FullName, "Assembler");
				}
			}
		}

		return null;
	}
}
