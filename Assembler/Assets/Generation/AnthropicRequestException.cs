using System;

namespace Assembler.Generation
{
	public sealed class AnthropicRequestException : Exception
	{
		public int StatusCode { get; }

		public AnthropicRequestException(int statusCode, string message) : base(message)
		{
			StatusCode = statusCode;
		}

		public AnthropicRequestException(int statusCode, string message, Exception inner) : base(message, inner)
		{
			StatusCode = statusCode;
		}
	}
}