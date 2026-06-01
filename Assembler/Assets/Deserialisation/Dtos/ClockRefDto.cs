namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// Deserialised form of a <c>!clock &lt;property&gt;</c> value tag (e.g. <c>!clock deltaTime</c>).
	/// Carries the requested clock property name; resolved at runtime against the injected game clock.
	/// </summary>
	public sealed record ClockRefDto
	{
		public string? Property { get; init; }
	}
}
