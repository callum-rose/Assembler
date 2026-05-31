using System.Collections.Generic;
using System.Linq;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// The outcome of a validation pass: every error found, accumulated in one walk of the tree.
	/// </summary>
	public sealed class ValidationResult
	{
		public IReadOnlyList<ValidationError> Errors { get; }

		public bool IsValid => Errors.Count == 0;

		public ValidationResult(IReadOnlyList<ValidationError> errors) => Errors = errors;

		/// <summary>Throws a single <see cref="ValidationException"/> carrying all errors if invalid.</summary>
		public void ThrowIfInvalid()
		{
			if (!IsValid)
			{
				throw new ValidationException(Errors);
			}
		}

		public override string ToString() =>
			IsValid ? "Validation passed." : string.Join("\n", Errors.Select(e => e.ToString()));
	}
}
