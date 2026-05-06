using System;
using Assembler.Generators.Attributes;

namespace Operations
{
	[GenerateEnumFromMembers(typeof(Func<string, string, string>))]
	public static class StringStringString
	{
		public static readonly Func<string, string, string> Concat = (first, second) => first + second;
		
		public static Func<string, string> CurryFirst(Func<string, string, string> func, string fixedValue) => b => func(fixedValue, b);
		public static Func<string, string> CurrySecond(Func<string, string, string> func, string fixedValue) => a => func(a, fixedValue);
	}
}