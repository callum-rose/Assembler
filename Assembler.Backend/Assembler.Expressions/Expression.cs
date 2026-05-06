using Assembler.Definitions;
using DynamicExpresso;

namespace Assembler.Models;

public static class Expression
{
	private static readonly IReadOnlyList<string> Alphabet =
		"abcdefghijklmnopqrstuvwxyz".Select(c => c.ToString()).ToArray();

	public static Lambda ToLambda(this ExpressionDef expressionDef)
	{
		var interpreter = new Interpreter();

		foreach (var (variableName, value) in Alphabet.Zip(expressionDef.Arguments))
		{
			interpreter.SetVariable(variableName, value, value.GetType());
		}

		return interpreter.Parse(expressionDef.Expression);
	}
}