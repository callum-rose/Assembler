using System.IO;
using Assembler.Extensions;
using UnityEngine;

namespace Assembler.Remote
{
	/// <summary>
	/// On-device cache of downloaded descriptors under <see cref="Application.persistentDataPath"/>, mirroring
	/// <c>DescriptorFileWriter</c>'s pattern. Files are keyed by game id + manifest version
	/// (<c>{id}/{version}.yaml</c>), so a version bump is the cache invalidation — a stale version is simply
	/// never looked up, and old versions are pruned lazily. The manifest itself is cached too, so the shelf
	/// can still list games when the device is offline.
	///
	/// Ids and versions arrive from a remote manifest, so every path segment is passed through
	/// <see cref="FileNameSanitiser"/> before it touches the filesystem — this strips separators and so cannot
	/// escape the cache directory.
	/// </summary>
	public sealed class RemoteGameCache
	{
		private const string ManifestFileName = "manifest.json";

		public string FolderPath { get; } = Path.Combine(Application.persistentDataPath, "RemoteGameCache");

		/// <summary>True (and sets <paramref name="path"/>) if this exact game+version is already on disk.</summary>
		public bool TryGetCached(string gameId, string version, out string path)
		{
			path = DescriptorPath(gameId, version);
			return File.Exists(path);
		}

		/// <summary>Persist a freshly-downloaded descriptor and prune other versions of the same game.
		/// Returns the path to build from.</summary>
		public string Write(string gameId, string version, string yaml)
		{
			var path = DescriptorPath(gameId, version);
			var dir = Path.GetDirectoryName(path);

			if (!string.IsNullOrEmpty(dir))
			{
				Directory.CreateDirectory(dir);
			}

			File.WriteAllText(path, yaml);
			PruneOtherVersions(gameId, keepPath: path);
			return path;
		}

		/// <summary>Cache the raw manifest JSON for offline fallback. Best-effort; IO failures are swallowed.</summary>
		public void WriteManifest(string json)
		{
			try
			{
				Directory.CreateDirectory(FolderPath);
				File.WriteAllText(Path.Combine(FolderPath, ManifestFileName), json);
			}
			catch (IOException)
			{
				// A failed cache write must never break the live flow — the network copy already succeeded.
			}
		}

		/// <summary>The last cached manifest JSON, or null if none has been stored.</summary>
		public string? ReadCachedManifest()
		{
			var path = Path.Combine(FolderPath, ManifestFileName);
			return File.Exists(path) ? File.ReadAllText(path) : null;
		}

		private string DescriptorPath(string gameId, string version) =>
			Path.Combine(FolderPath, SafeSegment(gameId, "game"), SafeSegment(version, "v") + ".yaml");

		private static string SafeSegment(string value, string fallback)
		{
			var sanitised = FileNameSanitiser.Sanitise(value);
			return string.IsNullOrEmpty(sanitised) ? fallback : sanitised;
		}

		private void PruneOtherVersions(string gameId, string keepPath)
		{
			var dir = Path.Combine(FolderPath, SafeSegment(gameId, "game"));

			if (!Directory.Exists(dir))
			{
				return;
			}

			foreach (var file in Directory.GetFiles(dir, "*.yaml"))
			{
				if (file == keepPath)
				{
					continue;
				}

				try
				{
					File.Delete(file);
				}
				catch (IOException)
				{
					// Leaving a stale version behind is harmless — it just won't be looked up.
				}
			}
		}
	}
}
