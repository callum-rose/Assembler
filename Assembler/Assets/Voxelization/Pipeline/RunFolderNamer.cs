using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Names a run's output folder. One short text call turns the set manifest
	/// into a descriptive kebab-case slug (e.g. "pirate-cove-props") so a run
	/// folder reads as what it contains rather than an opaque timestamp. The
	/// caller keeps the date prefix; this only supplies the descriptive tail.
	/// </summary>
	public sealed class RunFolderNamer
	{
		public const string Stage = "0-name";
		private const int MaxSlugLength = 48;

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public RunFolderNamer(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<string> NameAsync(SetManifest manifest, CancellationToken ct)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.FolderNameUser(manifest)),
			};

			var response = await _gateway.SendAsync(
				Stage, _config.ManifestModel, VoxelizationPrompts.FolderNameSystem, messages, ct).ConfigureAwait(false);

			return Slugify(response);
		}

		/// <summary>
		/// Reduces a model reply to a filesystem-safe kebab slug: first non-empty
		/// line, lowercased, every run of non-alphanumerics collapsed to a single
		/// hyphen, trimmed and length-capped. Falls back to "set" when nothing
		/// usable survives, so a folder name is always produced.
		/// </summary>
		public static string Slugify(string raw)
		{
			var line = (raw ?? string.Empty)
				.Split('\n')
				.Select(l => l.Trim())
				.FirstOrDefault(l => l.Length > 0) ?? string.Empty;

			var builder = new StringBuilder(line.Length);
			var pendingHyphen = false;
			foreach (var c in line)
			{
				if (char.IsLetterOrDigit(c))
				{
					if (pendingHyphen && builder.Length > 0)
					{
						builder.Append('-');
					}

					pendingHyphen = false;
					builder.Append(char.ToLowerInvariant(c));
				}
				else
				{
					pendingHyphen = true;
				}
			}

			var slug = builder.ToString();
			if (slug.Length > MaxSlugLength)
			{
				slug = slug[..MaxSlugLength].TrimEnd('-');
			}

			return slug.Length > 0 ? slug : "set";
		}
	}
}
