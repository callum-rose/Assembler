using System.Linq.Expressions;
using Assembler.Generators.Attributes;

namespace Operations
{
	[GenerateEnumFromMembers(typeof(Func<bool, bool>))]
	public static class BoolBool
	{
		public static readonly Func<bool, bool> Not = a => !a;
	}
	
	[GenerateEnumFromMembers(typeof(Func<bool, bool, bool>))]
	public static class BoolBoolBool
	{
		public static readonly Expression<Func<bool, bool, bool>> And = (a, b) => a && b;
		public static readonly Func<bool, bool, bool> Or = (a, b) => a || b;
		public static readonly Func<bool, bool, bool> Xor = (a, b) => a ^ b;
		public static readonly Func<bool, bool, bool> Same = (a, b) => a == b;
	}
}