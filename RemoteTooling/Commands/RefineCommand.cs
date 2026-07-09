namespace Assembler.RemoteTooling.Commands;

/// <summary>
/// <c>refine &lt;game-id&gt; "&lt;change request&gt;"</c> — fetch a published descriptor, apply a
/// natural-language change via the generator, then republish under the same id (bumping its version).
/// </summary>
public static class RefineCommand
{
	public static int Run(IReadOnlyList<string> args)
	{
		var log = new Logger();
		try
		{
			if (args.Count < 2)
			{
				throw new AppException("usage: refine <game-id> \"<change request>\"");
			}

			var id = args[0];
			var change = args[1];

			var current = Path.Combine(Config.StoreDir(), "games", id, "descriptor.yaml");
			if (!File.Exists(current))
			{
				throw new AppException($"no published game with id '{id}' (looked for {current})");
			}

			log.Info($"Refining '{id}': {change}");
			var revised = GameGenerator.Refine(change, File.ReadAllText(current), log);
			if (string.IsNullOrWhiteSpace(revised))
			{
				throw new AppException("refinement produced an empty descriptor");
			}

			// Hand off to publish under the same id — it validates, bumps the version, commits and pushes.
			var work = Directory.CreateTempSubdirectory("assembler-remote-refine-");
			try
			{
				var revisedPath = Path.Combine(work.FullName, $"{id}.yaml");
				File.WriteAllText(revisedPath, revised);
				Console.Out.WriteLine(PublishCommand.Publish(revisedPath, id, log));
				return 0;
			}
			finally
			{
				try { work.Delete(recursive: true); }
				catch { /* best-effort temp cleanup */ }
			}
		}
		catch (AppException ex)
		{
			log.Err(ex.Message);
			return 1;
		}
	}
}
