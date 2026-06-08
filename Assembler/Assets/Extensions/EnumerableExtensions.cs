using System.Collections.Generic;
using System.Linq;

namespace Assembler.Extensions
{
	public static class EnumerableExtensions
	{
		public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source) => source ?? Enumerable.Empty<T>();
	}
}
