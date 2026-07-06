using System.Collections.Generic;
using System.Text.Json;

namespace Assembler.Remote
{
	/// <summary>
	/// Deserialises a <c>manifest.json</c> body into a <see cref="GameManifest"/>. Defensive by design — a
	/// malformed document or a malformed entry never throws; the bad entry is dropped and the rest of the list
	/// is kept (mirrors <c>AssetManifestExtractor</c>). Returns <see cref="GameManifest.Empty"/> on any parse
	/// failure or when the body is blank.
	/// </summary>
	public static class GameManifestParser
	{
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			AllowTrailingCommas = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
		};

		public static GameManifest Parse(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return GameManifest.Empty;
			}

			ManifestDto? dto;
			try
			{
				dto = JsonSerializer.Deserialize<ManifestDto>(json!, JsonOptions);
			}
			catch (JsonException)
			{
				return GameManifest.Empty;
			}

			if (dto?.games == null)
			{
				return GameManifest.Empty;
			}

			var entries = new List<GameManifestEntry>();

			foreach (var e in dto.games)
			{
				if (e == null
					|| string.IsNullOrWhiteSpace(e.id)
					|| string.IsNullOrWhiteSpace(e.descriptorUrl)
					|| string.IsNullOrWhiteSpace(e.version))
				{
					continue;
				}

				entries.Add(new GameManifestEntry(
					e.id!.Trim(),
					string.IsNullOrWhiteSpace(e.title) ? e.id!.Trim() : e.title!.Trim(),
					string.IsNullOrWhiteSpace(e.description) ? null : e.description!.Trim(),
					e.descriptorUrl!.Trim(),
					e.version!.Trim()));
			}

			return new GameManifest(entries);
		}

		private sealed class ManifestDto
		{
			public int version { get; set; }
			public List<GameEntryDto?>? games { get; set; }
		}

		private sealed class GameEntryDto
		{
			public string? id { get; set; }
			public string? title { get; set; }
			public string? description { get; set; }
			public string? descriptorUrl { get; set; }
			public string? version { get; set; }
		}
	}
}
