using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class WorldDef
{
	[YamlMember(Alias = "dimensionality")]
	public int Dimensionality { get; set; }

	[YamlMember(Alias = "background color")]
	public string? BackgroundColor { get; set; }
}
