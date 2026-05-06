using System;
using Assembler.Generators.Attributes;

namespace Operations
{
	[GenerateEnumFromMembers(typeof(Func<float, float, float>))]
	public static class FloatFloatFloat
	{
		public static readonly Func<float, float, float> Add = (a, b) => a + b;
		public static readonly Func<float, float, float> Minus = (a, b) => a - b;
		public static readonly Func<float, float, float> Multiply = (a, b) => a * b;
		public static readonly Func<float, float, float> Divide = (a, b) => a / b;
		public static readonly Func<float, float, float> Maximum = (a, b) => a > b ? a : b;
		public static readonly Func<float, float, float> Minimum = (a, b) => a < b ? a : b;
		public static readonly Func<float, float, float> Average = (a, b) => (a + b) / 2;
		public static readonly Func<float, float, float> Modulus = (a, b) => a % b;
		public static readonly Func<float, float, float> Power = (a, b) => (float)Math.Pow(a, b); 
	}
}