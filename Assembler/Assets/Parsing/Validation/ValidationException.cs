using System;
using System.Collections.Generic;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// Thrown when validation fails. Carries every accumulated <see cref="ValidationError"/> so a
	/// single throw reports all problems at once. The message lists them one per line.
	/// </summary>
	public sealed class ValidationException : Exception
	{
		public IReadOnlyList<ValidationError> Errors { get; }

		public ValidationException(IReadOnlyList<ValidationError> errors)
			: base(BuildMessage(errors))
		{
			Errors = errors;
		}

		private static string BuildMessage(IReadOnlyList<ValidationError> errors)
		{
			var lines = new string[errors.Count + 1];
			lines[0] = $"Validation failed with {errors.Count} error(s):";

			for (var i = 0; i < errors.Count; i++)
			{
				lines[i + 1] = "  - " + errors[i];
			}

			return string.Join("\n", lines);
		}
	}
}
