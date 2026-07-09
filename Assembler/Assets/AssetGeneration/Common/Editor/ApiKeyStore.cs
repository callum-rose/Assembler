#nullable enable

using UnityEditor;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// One canonical <see cref="EditorPrefs"/> key per provider so a key entered in one generation
	/// window is seen by the others. Each window used to invent its own namespace
	/// (<c>Assembler.ImageGen.ApiKey.*</c>, <c>Assembler.TextToVoxel.ImageApiKey.*</c>, <c>Meshy.ImageTo3D.ApiKey</c>,
	/// …), so the same key had to be typed up to three times. <see cref="Load"/> reads the canonical
	/// key and, when it's blank, falls back to any legacy key so no already-saved key is lost.
	/// </summary>
	/// <remarks>
	/// The Anthropic key is deliberately NOT routed through here — it is already shared project-wide
	/// under <c>Assembler.Generation.ApiKey</c> (the game-generation windows read the same key), and
	/// re-namespacing it would split those windows off. Provider ids are plain strings so this stays
	/// free of any provider enum.
	/// </remarks>
	public static class ApiKeyStore
	{
		private const string Prefix = "Assembler.ApiKey.";

		/// <summary>The canonical pref key for a provider id (e.g. <c>Meshy</c>, <c>Image.GoogleGemini</c>).</summary>
		public static string PrefKey(string providerId) => Prefix + providerId;

		/// <summary>
		/// The saved key for a provider: the canonical value, or — when that is blank — the first
		/// non-empty <paramref name="legacyKeys"/> pref (the old per-window keys), or <c>""</c>.
		/// </summary>
		public static string Load(string providerId, params string[] legacyKeys)
		{
			var canonical = EditorPrefs.GetString(PrefKey(providerId), "");
			if (!string.IsNullOrEmpty(canonical))
			{
				return canonical;
			}

			foreach (var legacy in legacyKeys)
			{
				var value = EditorPrefs.GetString(legacy, "");
				if (!string.IsNullOrEmpty(value))
				{
					return value;
				}
			}

			return "";
		}

		/// <summary>Persist a provider's key under its canonical pref key.</summary>
		public static void Save(string providerId, string key) =>
			EditorPrefs.SetString(PrefKey(providerId), key);
	}
}
