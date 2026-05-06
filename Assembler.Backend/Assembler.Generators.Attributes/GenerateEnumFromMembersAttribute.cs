using System;

namespace Assembler.Generators.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GenerateEnumFromMembersAttribute : Attribute
{
    public Type FieldType { get; }

    public GenerateEnumFromMembersAttribute(Type fieldType)
    {
        FieldType = fieldType;
    }
}