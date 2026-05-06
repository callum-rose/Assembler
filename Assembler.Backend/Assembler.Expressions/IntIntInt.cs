using System;
using Assembler.Generators.Attributes;

namespace Operations
{
	[GenerateEnumFromMembers(typeof(Func<int, int, int>))]
	public static class IntIntInt
	{
		public static readonly Func<int, int, int> Add = (a, b) => a + b;
		public static readonly Func<int, int, int> Minus = (a, b) => a - b;
		public static readonly Func<int, int, int> Multiply = (a, b) => a * b;
		public static readonly Func<int, int, int> Divide = (a, b) => a / b;
		public static readonly Func<int, int, int> Maximum = (a, b) => a > b ? a : b;
		public static readonly Func<int, int, int> Minimum = (a, b) => a < b ? a : b;
		public static readonly Func<int, int, int> Average = (a, b) => (a + b) / 2;
		public static readonly Func<int, int, int> Modulus = (a, b) => a % b;
		public static readonly Func<int, int, int> Power = (a, b) => (int)Math.Pow(a, b);
	}
}