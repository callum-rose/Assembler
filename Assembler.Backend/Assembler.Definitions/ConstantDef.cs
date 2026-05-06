using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class ConstantDef
{
	[YamlMember(Alias = "id")] public string? Id { get; set; }
}

public class ConstantDef<T> : ConstantDef
{
	[YamlMember(Alias = "value")] public ValueOrReference<T>? Value { get; set; }
}