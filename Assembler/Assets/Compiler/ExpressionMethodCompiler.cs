using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Assembler.Compiler.Compiler
{
	public class ExpressionMethodCompiler
	{
		private readonly Dictionary<string, List<MethodInfo>> _registeredMethods = new();
		private readonly Dictionary<string, Type> _registeredTypes = new();

		public void RegisterMethod(string name, MethodInfo methodInfo)
		{
			if (!_registeredMethods.ContainsKey(name))
			{
				_registeredMethods[name] = new List<MethodInfo>();
			}

			_registeredMethods[name].Add(methodInfo);
		}

		public void RegisterStaticMethods(Type type)
		{
			var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

			foreach (var method in methods)
			{
				RegisterMethod(method.Name, method);
			}
		}

		public void RegisterType(Type type, string? alias = null)
		{
			// Register by fully qualified name
			_registeredTypes[type.FullName ?? type.Name] = type;

			// Register by simple name
			_registeredTypes[type.Name] = type;

			// Register by alias if provided
			if (alias != null)
			{
				_registeredTypes[alias] = type;
			}
		}

		public Delegate Compile(string code, Type returnType, out Type delegateType, params (Type type, string name)[] parameters)
		{
			var lexer = new Lexer(code);
			var tokens = lexer.Tokenize();

			var parser = new Parser(tokens);

			// Register methods with parser
			foreach (var methodGroup in _registeredMethods)
			{
				foreach (var method in methodGroup.Value)
				{
					parser.RegisterMethod(methodGroup.Key, method);
				}
			}

			// Register types with parser
			foreach (var type in _registeredTypes)
			{
				parser.RegisterType(type.Key, type.Value);
			}

			// Create parameter expressions first
			var paramExprs = parameters.Select(p => Expression.Parameter(p.type, p.name)).ToArray();

			// Register parameters with parser so it uses the same instances
			foreach (var paramExpr in paramExprs)
			{
				parser.RegisterParameter(paramExpr);
			}

			var body = parser.ParseMethodBody(new Dictionary<string, Type>());

			delegateType = GetDelegateType(returnType, parameters.Select(p => p.type).ToArray());
			var lambda = Expression.Lambda(delegateType, body, paramExprs);

			return lambda.Compile();
		}

		public Func<TResult> CompileFunc<TResult>(string code)
		{
			return (Func<TResult>)Compile(code, typeof(TResult), out _);
		}

		public Func<T, TResult> CompileFunc<T, TResult>(string code, string paramName)
		{
			return (Func<T, TResult>)Compile(code, typeof(TResult), out _, (typeof(T), paramName));
		}

		public Func<TParam0, TParam1, TResult> CompileFunc<TParam0, TParam1, TResult>(string code, string paramName0,
			string paramName1)
		{
			return (Func<TParam0, TParam1, TResult>)Compile(code,
				typeof(TResult),
				out _,
				(typeof(TParam0), paramName0),
				(typeof(TParam1), paramName1));
		}

		public Action CompileAction(string code)
		{
			return (Action)Compile(code, typeof(void), out _);
		}

		public Action<T> CompileAction<T>(string code, string paramName)
		{
			return (Action<T>)Compile(code, typeof(void), out _, (typeof(T), paramName));
		}

		public Action<T1, T2> CompileAction<T1, T2>(string code,
			string param1Name = "input1",
			string param2Name = "input2")
		{
			return (Action<T1, T2>)Compile(code, typeof(void), out _, (typeof(T1), param1Name), (typeof(T2), param2Name));
		}

		private static Type GetDelegateType(Type returnType, Type[] parameterTypes)
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
			else
			{
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
}