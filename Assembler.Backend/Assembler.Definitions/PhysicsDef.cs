using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class PhysicsDef
{
	[YamlMember(Alias = "gravity")]
	public ValueOrReference? Gravity { get; set; }
}
