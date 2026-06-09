using System;

namespace Assembler.Resolving
{
	/// <summary>
	/// Thrown when resolving or building a descriptor's value pipeline fails for a reason attributable
	/// to the descriptor — an unknown variable, an unsupported value type, a missing expression, a
	/// callable-name collision, etc. The parsing stage has <c>ParsingException</c>; this is its analogue
	/// for the resolve/build stage, so the sandbox validator and the generation fix-loop can distinguish
	/// a user-descriptor error from an internal engine bug (which stays a plain exception).
	/// </summary>
	public class ResolveException : Exception
	{
		public ResolveException(string message) : base(message) { }

		public ResolveException(string message, Exception innerException) : base(message, innerException) { }
	}
}
