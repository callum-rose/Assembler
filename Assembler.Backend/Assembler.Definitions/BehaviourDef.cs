using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class BehaviourDef
{
	[YamlMember(Alias = "type")]
	public string? Type { get; set; }

	[YamlMember(Alias = "id")]
	public string? Id { get; set; }

	[YamlMember(Alias = "properties")]
	public Dictionary<string, object>? Properties { get; set; }
}
