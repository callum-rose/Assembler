using System.Collections.Generic;

namespace Assembler.Generation.Verification
{
	public abstract record GenerationResult(IReadOnlyList<Attempt> Attempts);

	public sealed record SuccessfulGeneration(
		string YamlPath,
		IReadOnlyList<Attempt> Attempts) : GenerationResult(Attempts);

	public sealed record FailedGeneration(
		string? YamlPath,
		IReadOnlyList<Attempt> Attempts) : GenerationResult(Attempts);
}
