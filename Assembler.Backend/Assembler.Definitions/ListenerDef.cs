using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class ListenerDef
{
	[YamlMember(Alias = "entity ref")]
	public string? Entity { get; set; }

	[YamlMember(Alias = "behaviour ref")]
	public string? Behaviour { get; set; }
}