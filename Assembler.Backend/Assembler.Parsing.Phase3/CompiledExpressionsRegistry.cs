using Assembler.Compiler;
using Assembler.Parsing2.Info;

namespace Assembler.Parsing.Phase3;

public class CompiledExpressionsRegistry
{
	private readonly IReadOnlyDictionary<string, Type> _typeRegistry;
	private readonly ExpressionMethodCompiler _compiler;

	private readonly Dictionary<string, (Type delegateType, Delegate @delegate)> _compiledExpressions = new();

	public CompiledExpressionsRegistry(IReadOnlyDictionary<string, Type> typeRegistry, ExpressionMethodCompiler compiler)
	{
		_typeRegistry = typeRegistry;
		_compiler = compiler;
	}

	public void CompileAndRegister(ExpressionInfo expressionInfo)
	{
		var compiledExpression = _compiler.Compile(
			expressionInfo.Expression,
			_typeRegistry[expressionInfo.ReturnType],
			out var delegateType,
			expressionInfo.Arguments.Select(a => (_typeRegistry[a.type], a.name)).ToArray());

		_compiledExpressions[expressionInfo.Id] = (delegateType, compiledExpression);
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