using System;

namespace Assembler.Generators.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public class InjectAttribute : Attribute
{
	public string Name { get; }
	public string Description { get; }
	public string Example { get; }

	public InjectAttribute(string name, string description = "", string example = "")
	{
		Name = name;
		Description = description;
		Example = example;
	}
}