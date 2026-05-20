using System;

namespace Assembler.Parsing.Info
{
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class YamlNameAttribute : Attribute
	{
		public string Name { get; }

		public YamlNameAttribute(string name) => Name = name;
	}
}
