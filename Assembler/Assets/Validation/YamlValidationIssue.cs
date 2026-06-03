namespace Assembler.Validation
{
	/// <summary>How serious a <see cref="YamlValidationIssue"/> is.</summary>
	public enum YamlValidationSeverity
	{
		/// <summary>The file is structurally invalid; it will not parse correctly.</summary>
		Error,

		/// <summary>The file parses, but something is suspicious (e.g. an empty document).</summary>
		Warning,
	}

	/// <summary>
	/// A single problem found while validating YAML, optionally pinned to a source location.
	/// Positions are 1-based; <see cref="Line"/> == 0 means the issue has no specific location.
	/// </summary>
	public sealed class YamlValidationIssue
	{
		public YamlValidationSeverity Severity { get; }
		public string Message { get; }
		public int Line { get; }
		public int Column { get; }
		public int EndLine { get; }
		public int EndColumn { get; }

		public YamlValidationIssue(
			YamlValidationSeverity severity,
			string message,
			int line = 0,
			int column = 0,
			int endLine = 0,
			int endColumn = 0)
		{
			Severity = severity;
			Message = message;
			Line = line;
			Column = column;
			EndLine = endLine;
			EndColumn = endColumn;
		}

		/// <summary>True when this issue points at a concrete line in the source.</summary>
		public bool HasLocation => Line > 0;
	}
}
