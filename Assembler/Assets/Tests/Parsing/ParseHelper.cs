using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;

namespace Tests.Parsing
{
	/// <summary>
	/// The standard YAML → <see cref="GameInfo"/> pipeline used across the parsing tests: deserialise with
	/// <see cref="GameFileParser"/>, then transform the DTO with <see cref="Transformer"/>. Shared here so
	/// the two steps aren't hand-copied (and allowed to drift) in every fixture.
	/// </summary>
	internal static class ParseHelper
	{
		internal static GameInfo ParseGame(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));
	}
}
