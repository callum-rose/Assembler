using System;
using System.Linq;

namespace Assembler.Compiler.Compiler
{
	public static class DelegateTypeHelper
	{
		// Kept as an explicit switch rather than collapsing to BCL Expression.GetDelegateType on purpose:
		// GetDelegateType emits a brand-new delegate type via Reflection.Emit for arities beyond the built-in
		// Action/Func families, which is unavailable under IL2CPP/AOT (a Phase 0 concern — the compiler must
		// run in AOT player builds). Naming each Action<...>/Func<...> instantiation directly keeps every path
		// to a closed generic MakeGenericType, with no dynamic type generation. Do not "simplify" this away.
		public static Type GetDelegateType(Type returnType, Type[] parameterTypes)
		{
			if (returnType == typeof(void))
			{
				return parameterTypes.Length switch
				{
					0 => typeof(Action),
					1 => typeof(Action<>).MakeGenericType(parameterTypes),
					2 => typeof(Action<,>).MakeGenericType(parameterTypes),
					3 => typeof(Action<,,>).MakeGenericType(parameterTypes),
					4 => typeof(Action<,,,>).MakeGenericType(parameterTypes),
					5 => typeof(Action<,,,,>).MakeGenericType(parameterTypes),
					6 => typeof(Action<,,,,,>).MakeGenericType(parameterTypes),
					7 => typeof(Action<,,,,,,>).MakeGenericType(parameterTypes),
					8 => typeof(Action<,,,,,,,>).MakeGenericType(parameterTypes),
					9 => typeof(Action<,,,,,,,,>).MakeGenericType(parameterTypes),
					10 => typeof(Action<,,,,,,,,,>).MakeGenericType(parameterTypes),
					11 => typeof(Action<,,,,,,,,,,>).MakeGenericType(parameterTypes),
					12 => typeof(Action<,,,,,,,,,,,>).MakeGenericType(parameterTypes),
					13 => typeof(Action<,,,,,,,,,,,,>).MakeGenericType(parameterTypes),
					14 => typeof(Action<,,,,,,,,,,,,,>).MakeGenericType(parameterTypes),
					15 => typeof(Action<,,,,,,,,,,,,,,>).MakeGenericType(parameterTypes),
					16 => typeof(Action<,,,,,,,,,,,,,,,>).MakeGenericType(parameterTypes),
					_ => throw new NotSupportedException()
				};
			}

			var allTypes = parameterTypes.Append(returnType).ToArray();

			return allTypes.Length switch
			{
				1 => typeof(Func<>).MakeGenericType(allTypes),
				2 => typeof(Func<,>).MakeGenericType(allTypes),
				3 => typeof(Func<,,>).MakeGenericType(allTypes),
				4 => typeof(Func<,,,>).MakeGenericType(allTypes),
				5 => typeof(Func<,,,,>).MakeGenericType(allTypes),
				6 => typeof(Func<,,,,,>).MakeGenericType(allTypes),
				7 => typeof(Func<,,,,,,>).MakeGenericType(allTypes),
				8 => typeof(Func<,,,,,,,>).MakeGenericType(allTypes),
				9 => typeof(Func<,,,,,,,,>).MakeGenericType(allTypes),
				10 => typeof(Func<,,,,,,,,,>).MakeGenericType(allTypes),
				11 => typeof(Func<,,,,,,,,,,>).MakeGenericType(allTypes),
				12 => typeof(Func<,,,,,,,,,,,>).MakeGenericType(allTypes),
				13 => typeof(Func<,,,,,,,,,,,,>).MakeGenericType(allTypes),
				14 => typeof(Func<,,,,,,,,,,,,,>).MakeGenericType(allTypes),
				15 => typeof(Func<,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
				16 => typeof(Func<,,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
				17 => typeof(Func<,,,,,,,,,,,,,,,,>).MakeGenericType(allTypes),
				_ => throw new NotSupportedException()
			};
		}
	}
}
