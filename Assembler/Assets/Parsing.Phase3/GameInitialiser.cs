using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using Compiler;

namespace Assembler.Parsing.Phase3.Parsing.Phase3
{
	public static class GameInitialiser
	{
		public static (VariableRegistry, CompiledExpressionsRegistry) Initialise(GameInfo gameInfo,
			IReadOnlyDictionary<string, Type> typeRegistry)
		{
			var variableRegistry = new VariableRegistry();

			foreach (var variableInfo in gameInfo.Variables)
			{
				variableRegistry.Register(variableInfo);
			}

			var compiler = new ExpressionMethodCompiler();
			var compiledExpressionsRegistry = new CompiledExpressionsRegistry(typeRegistry, compiler);

			foreach (var expressionInfo in gameInfo.Expressions)
			{
				compiledExpressionsRegistry.CompileAndRegister(expressionInfo);
			}

			return (variableRegistry, compiledExpressionsRegistry);
		}
	}
}