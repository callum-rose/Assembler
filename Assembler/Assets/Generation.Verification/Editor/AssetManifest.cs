using System.Collections.Generic;
using System.Text.Json;
using Assembler.Anthropic;

namespace Assembler.Generation.Verification.Editor
{
	/// <summary>
	/// Pulls the typed asset manifest out of a descriptor-generator reply. Claude emits
	/// a fenced <c>```assets</c> JSON array of <c>{type, id, path, prompt}</c>; this
	/// parses + sanitises it into <see cref="AssetRequest"/>s. Returns an empty list
	/// (never null) when the block is absent or malformed — a game may need no assets.
	/// </summary>
	public static class AssetManifestExtractor
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			AllowTrailingCommas = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
		};

		public static IReadOnlyList<AssetRequest> Extract(string? rawReply)
		{
			var result = new List<AssetRequest>();
			if (string.IsNullOrWhiteSpace(rawReply))
			{
				return result;
			}

			var json = FencedBlockExtractor.Extract(rawReply!, "assets");
			if (string.IsNullOrWhiteSpace(json))
			{
				return result;
			}

			List<AssetEntryDto>? entries;
			try
			{
				entries = JsonSerializer.Deserialize<List<AssetEntryDto>>(json!, JsonOptions);
			}
			catch (JsonException)
			{
				return result;
			}

			if (entries == null)
			{
				return result;
			}

			foreach (var e in entries)
			{
				if (e == null)
				{
					continue;
				}

				var type = e.type;
				var rawId = e.id;
				var prompt = e.prompt;
				if (string.IsNullOrWhiteSpace(type))
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(rawId))
				{
					continue;
				}

				if (string.IsNullOrWhiteSpace(prompt))
				{
					continue;
				}

				var id = DescriptorFileWriter.Sanitise(rawId);
				if (string.IsNullOrEmpty(id))
				{
					continue;
				}

				var path = SanitisePath(e.path) ?? id;

				result.Add(new AssetRequest(type!.Trim(), id, path, prompt!.Trim()));
			}

			return result;
		}

		/// <summary>
		/// Sanitise each path segment but keep the '/' separators, so a Resources-relative
		/// path like <c>Voxels/&lt;slug&gt;/player</c> survives intact.
		/// </summary>
		private static string? SanitisePath(string? rawPath)
		{
			if (string.IsNullOrWhiteSpace(rawPath))
			{
				return null;
			}

			var segments = rawPath!.Replace('\\', '/').Split('/');
			var clean = new List<string>();
			foreach (var seg in segments)
			{
				var s = DescriptorFileWriter.Sanitise(seg);
				if (!string.IsNullOrEmpty(s))
				{
					clean.Add(s);
				}
			}

			return clean.Count == 0 ? null : string.Join("/", clean);
		}

		private sealed class AssetEntryDto
		{
			public string? type { get; set; }
			public string? id { get; set; }
			public string? path { get; set; }
			public string? prompt { get; set; }
		}
	}
}
