using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Assembler.RemoteTooling;

/// <summary>
/// Store-repo helpers: turning a title into a game id, versioning a descriptor by content hash,
/// discovering the store's owner/repo from its git remote, and upserting <c>manifest.json</c>.
/// </summary>
public static partial class Store
{
	/// <summary>Lowercase, collapse non-alphanumerics to hyphens, trim, cap at 48 chars. Matches the old shell <c>slugify</c>.</summary>
	public static string Slugify(string title)
	{
		var hyphenated = NonAlphaNumeric().Replace(title.ToLowerInvariant(), "-").Trim('-');
		return hyphenated.Length > 48 ? hyphenated[..48] : hyphenated;
	}

	/// <summary>The first 8 hex chars of the descriptor's SHA-256 — the client-visible version. Matches <c>shasum -a 256 | cut -c1-8</c>.</summary>
	public static string Version(string descriptorPath) =>
		Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(descriptorPath)))[..8];

	/// <summary>Resolve the store's GitHub <c>(owner, repo)</c> from its git remote, falling back to the authed user.</summary>
	public static (string Owner, string Repo) OwnerRepo(string storeDir, string remote)
	{
		var url = Proc.Capture("git", ["-C", storeDir, "remote", "get-url", remote]).StdOut.Trim();

		var view = Proc.Capture("gh", ["repo", "view", url, "--json", "owner", "--jq", ".owner.login"]);
		var owner = view.ExitCode == 0 && view.StdOut.Trim().Length > 0
			? view.StdOut.Trim()
			: Proc.Capture("gh", ["api", "user", "--jq", ".login"]).StdOut.Trim();

		var repo = Path.GetFileName(url);
		if (repo.EndsWith(".git", StringComparison.Ordinal))
		{
			repo = repo[..^4];
		}

		return (owner, repo);
	}

	/// <summary>
	/// Insert or replace the game entry in <c>manifest.json</c> (keyed by id, appended last),
	/// preserving the file's <c>version</c>. Equivalent to the old jq expression.
	/// </summary>
	public static void UpsertManifest(string manifestPath, string id, string title, string descriptorUrl, string version)
	{
		var root = File.Exists(manifestPath)
			? JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject ?? new JsonObject()
			: new JsonObject();

		root["version"] = root["version"] is JsonNode v && v.GetValueKind() == JsonValueKind.Number
			? v.GetValue<int>()
			: 1;

		var games = new JsonArray();
		if (root["games"] is JsonArray existing)
		{
			foreach (var game in existing)
			{
				if (game is JsonObject entry && entry["id"]?.GetValue<string>() == id)
				{
					continue; // drop the previous entry for this id; the fresh one is appended below
				}

				games.Add(game?.DeepClone());
			}
		}

		games.Add(new JsonObject
		{
			["id"] = id,
			["title"] = title,
			["descriptorUrl"] = descriptorUrl,
			["version"] = version,
		});
		root["games"] = games;

		var options = new JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};
		File.WriteAllText(manifestPath, root.ToJsonString(options) + "\n");
	}

	[GeneratedRegex("[^a-z0-9]+")]
	private static partial Regex NonAlphaNumeric();
}
