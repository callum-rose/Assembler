namespace Assembler.Parsing;

/// <summary>
/// Thrown when game configuration validation fails.
/// Contains detailed error messages about what references are invalid.
/// </summary>
public class YamlValidationException : Exception
{
	public YamlValidationException(string message) : base(message)
	{
	}

	public YamlValidationException(string message, Exception innerException)
		: base(message, innerException)
	{
	}
}

