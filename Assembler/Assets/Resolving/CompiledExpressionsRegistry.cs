using System;
using System.Collections.Generic;
using System.Linq;
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
		}

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