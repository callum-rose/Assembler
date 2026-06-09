using System;

namespace Assembler.Compiler.Compiler
{
	/// <summary>
	/// Thrown when source fed to the expression compiler is invalid — a lexer or parser error in the
	/// user's code, as opposed to an internal compiler bug (which surfaces as a plain exception).
	/// Carries the <see cref="Line"/> and <see cref="Column"/> of the offending token so callers can
	/// point at the exact spot. The formatted <see cref="Exception.Message"/> always ends with the
	/// position, so error reporting that only surfaces the message still carries it.
	/// </summary>
	public class CompileException : Exception
	{
		public int Line { get; }
		public int Column { get; }

		public CompileException(string message, int line, int column)
			: base($"{message} at line {line}, column {column}")
		{
			Line = line;
			Column = column;
		}

		public CompileException(string message, int line, int column, Exception innerException)
			: base($"{message} at line {line}, column {column}", innerException)
		{
			Line = line;
			Column = column;
		}
	}
}
