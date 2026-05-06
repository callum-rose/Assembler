using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class ExpressionDef
{
	[YamlMember(Alias = "id")]
	public string Id { get; set; } = null!;

	[YamlMember(Alias = "type")]
	public string Type { get; set; } = null!;

	[YamlMember(Alias = "expression")]
	public string Expression { get; set; } = null!;

	[YamlMember(Alias = "arguments")]
	public List<ValueOrReference>? Arguments { get; set; }
}
