namespace Assembler.Generation.Verification
{
	public abstract record Attempt(int AttemptNumber);

	public sealed record RequestFailedAttempt(
		int AttemptNumber,
		string Error) : Attempt(AttemptNumber);

	public sealed record InvalidResponseAttempt(
		int AttemptNumber,
		string? Feedback,
		string Error) : Attempt(AttemptNumber);

	public sealed record BuildAttempt(
		int AttemptNumber,
		string Yaml,
		string? Feedback,
		BuildResult BuildResult) : Attempt(AttemptNumber);
}
