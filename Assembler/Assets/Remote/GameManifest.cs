using System.Collections.Generic;
using System.Text.Json;

namespace Assembler.Remote
{
	/// <summary>
	/// The remote shelf index: the list of games available to download and play. Parsed from the
	/// <c>manifest.json</c> served by the remote store. Parsing is defensive — a malformed document or
	/// a malformed entry never throws; it is dropped and the rest of the list is kept (mirrors
	/// <c>AssetManifestExtractor</c>). An empty manifest is a valid state (nothing published yet).
	/// </summary>
	public sealed record GameManifest(IReadOnlyList<GameManifestEntry> Games)
	{
		public static GameManifest Empty { get; } = new(System.Array.Empty<GameManifestEntry>());

		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			PropertyNameCaseInsensitive = true,
			AllowTrailingCommas = true,
			ReadCommentHandling = JsonCommentHandling.Skip,
		};

		/// <summary>
		/// Deserialise a <c>manifest.json</c> body. Returns <see cref="Empty"/> on any parse failure and
		/// silently drops entries missing a required field (<c>id</c> / <c>descriptorUrl</c> / <c>version</c>).
		/// </summary>
		public static GameManifest Parse(string? json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return Empty;
			}

			ManifestDto? dto;
			try
			{
				dto = JsonSerializer.Deserialize<ManifestDto>(json!, JsonOptions);
			}
			catch (JsonException)
			{
				return Empty;
			}

			if (dto?.games == null)
			{
				return Empty;
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

	/// <summary>One playable game in the remote shelf. <see cref="Version"/> is the cache key — bump it
	/// on the store side whenever the descriptor changes so clients re-download.</summary>
	public sealed record GameManifestEntry(
		string Id,
		string Title,
		string? Description,
		string DescriptorUrl,
		string Version);
}
