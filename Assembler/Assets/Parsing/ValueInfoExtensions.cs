using System.Collections.Generic;
using Assembler.Parsing.Info;

namespace Assembler.Parsing
{
	public static class ValueInfoExtensions
	{
		/// <summary>
		/// Finds the constant/variable registered under <paramref name="id"/> among the
		/// already-resolved values and returns its flattened <see cref="AssemblerValue"/>.
		/// Throws a <see cref="ParsingException"/> if no value with that id exists.
		/// </summary>
		public static AssemblerValue ResolveValue(this IReadOnlyList<ValueInfo> values, string id)
		{
			foreach (var value in values)
			{
				if (value.Id == id)
				{
					return value.Value;
				}
			}

			throw new ParsingException($"Cannot resolve reference '{id}'");
		}
	}
}
