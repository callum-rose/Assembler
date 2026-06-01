namespace Assembler.Resolving
{
	/// <summary>
	/// Framework-level locale selection for the localization layer. <see cref="Current"/> is the
	/// active locale; <see cref="Fallback"/> is consulted when a key is missing in the current locale.
	/// Currently constructed with hardcoded defaults in <c>Builder</c>; intended to be driven by the
	/// future settings/options system.
	/// </summary>
	public sealed record LocaleSettings(string Current, string Fallback);
}
