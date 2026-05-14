using System;

namespace Assembler.Parsing
{
	public class ParsingException : Exception
	{
		public ParsingException(string message) : base(message) { }
		public ParsingException(string message, Exception innerException) : base(message, innerException) { }
	}
}