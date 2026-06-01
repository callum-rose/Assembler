using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Assembler.Extensions
{
	public static class StringExtensions
	{
		// Converts a free-form string such as "base offset" into a camelCase
		// identifier "baseOffset" by splitting on any non-alphanumeric runs.
		public static string ToCamelCase(this string value)
		{
			var parts = Regex.Split(value, "[^A-Za-z0-9]+").Where(p => p.Length > 0).ToArray();

			if (parts.Length == 0)
			{
				return value;
			}

			var sb = new StringBuilder();

			for (int i = 0; i < parts.Length; i++)
			{
				var part = parts[i];
				var first = i == 0 ? char.ToLowerInvariant(part[0]) : char.ToUpperInvariant(part[0]);
				sb.Append(first);
				sb.Append(part, 1, part.Length - 1);
			}

			return sb.ToString();
		}
	}
}
