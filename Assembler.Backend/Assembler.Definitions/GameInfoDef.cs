using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class GameInfoDef
{
	[YamlMember(Alias = "title")]
	public string? Title { get; set; }

	[YamlMember(Alias = "description")]
	public string? Description { get; set; }
}
