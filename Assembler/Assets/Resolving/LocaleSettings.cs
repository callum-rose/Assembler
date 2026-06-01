namespace Assembler.Resolving
{
	/// <summary>
	/// Framework-level locale selection for the localisation layer. <see cref="Current"/> is the
	/// active locale; when a key is missing in it the string table falls back to the descriptor's
	/// declared default locale. Currently constructed with a hardcoded default in <c>Builder</c>;
	/// intended to be driven by the future settings/options system.
	/// </summary>
	public sealed record LocaleSettings(string Current);
}
