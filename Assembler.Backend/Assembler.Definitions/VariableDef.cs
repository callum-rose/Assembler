using YamlDotNet.Serialization;

namespace Assembler.Definitions;

public class VariableDef
{
	[YamlMember(Alias = "id")] public string? Id { get; set; }
}

public class VariableDef<T> : VariableDef
{
	[YamlMember(Alias = "initial value")] public ValueOrReference<T>? InitialValue { get; set; }
}