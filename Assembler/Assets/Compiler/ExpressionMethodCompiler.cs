using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Assembler.Compiler.Compiler
{
	public class ExpressionMethodCompiler
	{
		private readonly Dictionary<string, HashSet<MethodInfo>> _registeredMethods = new();
		private readonly Dictionary<string, Type> _registeredTypes = new();
		private readonly Dictionary<string, (Delegate @delegate, Type[] paramTypes, Type returnType)> _registeredExpressions = new();

		public void RegisterMethod(string name, MethodInfo methodInfo)
		{
			if (!_registeredMethods.ContainsKey(name))
			{
				_registeredMethods[name] = new HashSet<MethodInfo>();
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

		// Registers an already-compiled expression delegate so other expressions can
		// invoke it by name as a local method call.
		public void RegisterExpression(string name, Delegate @delegate, Type[] paramTypes, Type returnType)
		{
			_registeredExpressions[name] = (@delegate, paramTypes, returnType);
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
			try
			{
				return CompileInternal(code, returnType, out delegateType, parameters);
			}
			catch (ArgumentException ex)
			{
				// The parser positions every error it raises as a CompileException. A raw ArgumentException
				// escaping from the Expression factory (e.g. a type mismatch at an unguarded site) carries no
				// position, so re-throw it as a CompileException to keep the contract callers — the LLM
				// fix-loop and Tools/check-expression.sh — depend on. There is no token here, so position 0,0.
				throw new CompileException($"Type conversion failed: {ex.Message}", 0, 0, ex);
			}
		}

		private Delegate CompileInternal(string code, Type returnType, out Type delegateType, params (Type type, string name)[] parameters)
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

			// Register previously-compiled expressions as callable local methods
			foreach (var expression in _registeredExpressions)
			{
				parser.RegisterLocalMethod(expression.Key, expression.Value.@delegate, expression.Value.paramTypes,
					expression.Value.returnType);
			}

			// Create parameter expressions first
			var paramExprs = parameters.Select(p => Expression.Parameter(p.type, p.name)).ToArray();

			// Register parameters with parser so it uses the same instances
			foreach (var paramExpr in paramExprs)
			{
				parser.RegisterParameter(paramExpr);
			}

			var body = parser.ParseMethodBody(new Dictionary<string, Type>(), returnType);

			delegateType = DelegateTypeHelper.GetDelegateType(returnType, parameters.Select(p => p.type).ToArray());
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

	}
}
