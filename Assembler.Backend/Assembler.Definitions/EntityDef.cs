using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class EntityDef
{
	[YamlMember(Alias = "id")]
	public string? Id { get; set; }

	[YamlMember(Alias = "tags")]
	public List<string>? Tags { get; set; }

	[YamlMember(Alias = "position")]
	public string? Position { get; set; }

	[YamlMember(Alias = "rotation")]
	public string? Rotation { get; set; }

	[YamlMember(Alias = "behaviours")]
	public List<BehaviourDef>? Behaviours { get; set; }
}
