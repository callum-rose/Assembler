using System.Collections.Generic;

namespace Assembler.Remote
{
	/// <summary>
	/// The remote shelf index: the list of games available to download and play, parsed from the
	/// <c>manifest.json</c> served by the remote store (see <see cref="GameManifestParser"/>). An empty
	/// manifest is a valid state (nothing published yet).
	/// </summary>
	public sealed record GameManifest(IReadOnlyList<GameManifestEntry> Games)
	{
		public static GameManifest Empty { get; } = new(System.Array.Empty<GameManifestEntry>());
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
