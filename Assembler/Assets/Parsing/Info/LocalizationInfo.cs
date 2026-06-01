using System.Collections.Generic;

namespace Assembler.Parsing.Info
{
	/// <summary>
	/// The localization string table, parsed from the top-level <c>Localization:</c> descriptor block.
	/// Maps each locale to its (key -&gt; template) table. Consumed by the runtime string-table registry,
	/// which resolves <c>!text</c> keys against the current locale with a fallback. May be empty (the
	/// feature is pre-emptive — a descriptor need not declare any strings).
	/// </summary>
	public sealed record LocalizationInfo(
		string DefaultLocale,
		IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales)
	{
		public static readonly LocalizationInfo Empty = new(
			string.Empty,
			new Dictionary<string, IReadOnlyDictionary<string, string>>());
	}
}
