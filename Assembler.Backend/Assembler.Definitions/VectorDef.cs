using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class VectorDef
{
	[YamlMember(Alias = "x")]
	public ValueOrReference<float>? X { get; set; }

	[YamlMember(Alias = "y")]
	public ValueOrReference<float>? Y { get; set; }

	[YamlMember(Alias = "z")]
	public ValueOrReference<float>? Z { get; set; }
}
