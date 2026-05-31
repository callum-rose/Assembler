using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Assembler.Compiler.Compiler;
using Assembler.Parsing.Info;

namespace Assembler.Resolving
{
	public class CompiledExpressionsRegistry
	{
		private readonly IReadOnlyDictionary<string, Type> _typeRegistry;
		private readonly ExpressionMethodCompiler _compiler;

		private readonly Dictionary<string, (Type delegateType, Delegate @delegate)> _compiledExpressions = new();
		private readonly Dictionary<string, ExpressionInfo> _expressionInfos = new();

		public CompiledExpressionsRegistry(IReadOnlyDictionary<string, Type> typeRegistry, ExpressionMethodCompiler compiler)
		{
			_typeRegistry = typeRegistry;
			_compiler = compiler;

			// Globally register LINQ extension methods so expressions can use
			// .Any/.Where/.Select/.Count etc. on List<T> arguments without
			// per-expression RegisterTypeStatics. Also register Color so it's
			// constructible from any expression that consumes the new "colour"
			// argument type.
			_compiler.RegisterStaticMethods(typeof(System.Linq.Enumerable));
			_compiler.RegisterType(typeof(UnityEngine.Color));
		}

		public void CompileAndRegister(ExpressionInfo expressionInfo)
		{
			foreach (var typeName in expressionInfo.RegisterTypes)
			{
				var type = Type.GetType(typeName);
				
				if (type is null)
				{
					type = AppDomain.CurrentDomain.GetAssemblies()
						.SelectMany(a => a.GetTypes())
						.FirstOrDefault(t => t.FullName == typeName);
					
					if (type is null)
					{
						throw new Exception($"Type not found for name: {typeName}");
					}
				}
				
				_compiler.RegisterType(type);
			}

			foreach (var typeName in expressionInfo.RegisterTypeStatics)
			{
				var type = Type.GetType(typeName);
				
				if (type is null)
				{
					type = AppDomain.CurrentDomain.GetAssemblies()
						.SelectMany(a => a.GetTypes())
						.FirstOrDefault(t => t.FullName == typeName);
					
					if (type is null)
					{
						throw new Exception($"Type not found for name: {typeName}");
					}
				}
				
				_compiler.RegisterStaticMethods(type);
			}
			
			var compiledExpression = _compiler.Compile(
				expressionInfo.Expression,
				_typeRegistry[expressionInfo.ReturnType],
				out var delegateType,
				expressionInfo.Arguments.Select(a => (_typeRegistry[a.type], a.name)).ToArray());

			_compiledExpressions[expressionInfo.Id] = (delegateType, compiledExpression);
			_expressionInfos[expressionInfo.Id] = expressionInfo;

			// Make this expression callable by name from later-compiled expressions.
			var callableName = GetCallableName(expressionInfo);
			var paramTypes = expressionInfo.Arguments.Select(a => _typeRegistry[a.type]).ToArray();
			_compiler.RegisterExpression(callableName, compiledExpression, paramTypes,
				_typeRegistry[expressionInfo.ReturnType]);
		}

		// Compiles all expressions, ordering them so that an expression is compiled
		// after every other expression it calls. This lets expressions reference each
		// other regardless of their declaration order in the game descriptor.
		public void CompileAndRegisterAll(IReadOnlyList<ExpressionInfo> expressions)
		{
			var byCallableName = new Dictionary<string, ExpressionInfo>();
			foreach (var info in expressions)
			{
				var name = GetCallableName(info);
				if (byCallableName.TryGetValue(name, out var existing))
				{
					throw new Exception(
						$"Expression callable-name collision: '{name}' is produced by both '{existing.Id}' and " +
						$"'{info.Id}'. Set a 'CallableAs' alias on one of them to disambiguate.");
				}

				byCallableName[name] = info;
			}

			var ordered = new List<ExpressionInfo>();
			var visited = new HashSet<string>();
			var onStack = new HashSet<string>();

			void Visit(ExpressionInfo info)
			{
				if (!visited.Add(info.Id))
				{
					return;
				}

				onStack.Add(info.Id);

				foreach (var entry in byCallableName)
				{
					if (entry.Value.Id == info.Id)
					{
						continue;
					}

					if (CallsExpression(info.Expression, entry.Key))
					{
						if (onStack.Contains(entry.Value.Id))
						{
							throw new Exception(
								$"Cyclic expression dependency detected involving '{info.Id}' and '{entry.Value.Id}'.");
						}

						Visit(entry.Value);
					}
				}

				onStack.Remove(info.Id);
				ordered.Add(info);
			}

			foreach (var info in expressions)
			{
				Visit(info);
			}

			foreach (var info in ordered)
			{
				CompileAndRegister(info);
			}
		}

		private static string GetCallableName(ExpressionInfo info) =>
			string.IsNullOrWhiteSpace(info.CallableAlias) ? ToCallableName(info.Id) : info.CallableAlias!;

		// Converts an expression id such as "base offset" into a valid identifier
		// "baseOffset" that can be used as a method-call name inside expression code.
		private static string ToCallableName(string id)
		{
			var parts = Regex.Split(id, "[^A-Za-z0-9]+").Where(p => p.Length > 0).ToArray();

			if (parts.Length == 0)
			{
				return id;
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

		private static bool CallsExpression(string body, string callableName) =>
			Regex.IsMatch(body, $@"(?<![A-Za-z0-9_]){Regex.Escape(callableName)}\s*\(");

		public (Type delegateType, Delegate @delegate) GetCompiled(string id)
		{
			if (!_compiledExpressions.TryGetValue(id, out var typeAndDelegate))
			{
				throw new Exception($"Compiled expression not found for id: {id}");
			}
			return typeAndDelegate;
		}

		public ExpressionInfo GetInfo(string id)
		{
			if (!_expressionInfos.TryGetValue(id, out var info))
			{
				throw new Exception($"Expression info not found for id: {id}");
			}
			return info;
		}

		public Type ResolveType(string typeName)
		{
			if (!_typeRegistry.TryGetValue(typeName, out var type))
			{
				throw new Exception($"Type not registered: {typeName}");
			}
			return type;
		}

		public T Get<T>(string id) where T : Delegate
		{
			if (!_compiledExpressions.TryGetValue(id, out var typeAndDelegate))
			{
				throw new Exception($"Compiled expression not found for id: {id}");
			}

			if (typeAndDelegate.delegateType != typeof(T))
			{
				throw new Exception(
					$"Compiled expression type mismatch for id: {id}, expected {typeof(T)}, got {typeAndDelegate.delegateType}");
			}

			return (T)typeAndDelegate.@delegate;
		}
	}
}