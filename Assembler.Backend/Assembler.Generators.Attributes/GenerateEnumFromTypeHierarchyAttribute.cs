using System;

namespace Assembler.Generators.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GenerateEnumFromTypeHierarchyAttribute : Attribute
{
	public bool IncludeAbstractClasses { get; }

	public GenerateEnumFromTypeHierarchyAttribute(bool includeAbstractClasses = false)
	{
		IncludeAbstractClasses = includeAbstractClasses;
	}
}