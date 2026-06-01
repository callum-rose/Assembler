using System.Collections.Generic;
using Assembler.Parsing.Info;

namespace Assembler.Resolving
{
	/// <summary>
	/// Runtime string table for the localization layer. Loads the descriptor's <see cref="LocalizationInfo"/>
	/// and resolves <c>!text</c> keys to template strings for the current locale, falling back to the
	/// configured fallback locale and then the descriptor's declared default locale. Missing keys return a
	/// visible marker (<c>#key#</c>) rather than throwing, so authoring gaps surface in-game.
	/// Parallels <see cref="AssetRegistry"/>, but holds plain data instead of Unity objects.
	/// </summary>
	public sealed class StringTableRegistry
	{
		private readonly LocaleSettings _locale;
		private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _locales = new();
		private string _defaultLocale = string.Empty;

		public StringTableRegistry(LocaleSettings locale)
		{
			_locale = locale;
		}

		public void LoadAll(LocalizationInfo info)
		{
			_defaultLocale = info.DefaultLocale;
			_locales.Clear();

			foreach (var kvp in info.Locales)
			{
				_locales[kvp.Key] = kvp.Value;
			}
		}

		/// <summary>
		/// Resolve a key to its template string, trying the current locale, then the fallback locale,
		/// then the descriptor's declared default locale. Returns a visible <c>#key#</c> marker if the
		/// key is absent everywhere.
		/// </summary>
		public string GetTemplate(string key) =>
			TryGetTemplate(key, out var template) ? template : $"#{key}#";

		public bool TryGetTemplate(string key, out string template)
		{
			if (TryGetFrom(_locale.Current, key, out template) ||
			    TryGetFrom(_locale.Fallback, key, out template) ||
			    TryGetFrom(_defaultLocale, key, out template))
			{
				return true;
			}

			template = string.Empty;
			return false;
		}

		private bool TryGetFrom(string locale, string key, out string template)
		{
			if (!string.IsNullOrEmpty(locale) &&
			    _locales.TryGetValue(locale, out var table) &&
			    table.TryGetValue(key, out var found))
			{
				template = found;
				return true;
			}

			template = string.Empty;
			return false;
		}
	}
}
