using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assembler.Validation
{
	/// <summary>
	/// The outcome of validating one YAML document: the list of issues found plus the source it was
	/// validated against (kept so reports can render the offending lines with a caret). Construct via
	/// <see cref="YamlStructureValidator"/>.
	/// </summary>
	public sealed class YamlValidationResult
	{
		private readonly string[] _lines;

		/// <summary>An optional name for the source (e.g. a file path) used in report headers.</summary>
		public string SourceName { get; }

		/// <summary>Every issue found, in source order.</summary>
		public IReadOnlyList<YamlValidationIssue> Issues { get; }

		internal YamlValidationResult(string sourceName, string[] lines, List<YamlValidationIssue> issues)
		{
			SourceName = sourceName;
			_lines = lines;
			Issues = issues;
		}

		/// <summary>Number of error-severity issues.</summary>
		public int ErrorCount => Issues.Count(i => i.Severity == YamlValidationSeverity.Error);

		/// <summary>Number of warning-severity issues.</summary>
		public int WarningCount => Issues.Count(i => i.Severity == YamlValidationSeverity.Warning);

		/// <summary>True when the document is structurally valid (no error-severity issues).</summary>
		public bool IsValid => ErrorCount == 0;

		/// <summary>
		/// Renders a detailed, human-readable report — one block per issue with its severity,
		/// line/column, message, and a snippet of the offending line with a caret beneath the column.
		/// Suitable for logging to the Unity console, writing to a file, or showing in-game.
		/// </summary>
		public string FormatReport()
		{
			var sb = new StringBuilder();
			foreach (var issue in Issues)
			{
				AppendIssue(sb, issue);
				sb.Append('\n');
			}

			return sb.ToString().TrimEnd('\n');
		}

		private void AppendIssue(StringBuilder sb, YamlValidationIssue issue)
		{
			var label = issue.Severity == YamlValidationSeverity.Error ? "error" : "warning";
			sb.Append("  ").Append(label);
			if (issue.HasLocation)
			{
				sb.Append(" at line ").Append(issue.Line).Append(", column ").Append(issue.Column);
			}

			sb.Append(": ").Append(issue.Message);

			if (issue.HasLocation)
			{
				AppendSnippet(sb, issue);
			}
		}

		// Renders the offending line with a caret beneath the flagged column, e.g.
		//
		//       12 |   foo: bar
		//          |   ^
		private void AppendSnippet(StringBuilder sb, YamlValidationIssue issue)
		{
			var lineNo = issue.Line;
			if (lineNo < 1 || lineNo > _lines.Length)
			{
				return;
			}

			var line = _lines[lineNo - 1];
			var gutter = new string(' ', lineNo.ToString().Length);

			var col = issue.Column < 1 ? 1 : issue.Column;
			var caretPad = col - 1;
			if (caretPad > line.Length)
			{
				caretPad = line.Length;
			}

			if (caretPad < 0)
			{
				caretPad = 0;
			}

			var caretLen = 1;
			if (issue.EndLine == issue.Line && issue.EndColumn > issue.Column)
			{
				caretLen = issue.EndColumn - issue.Column;
			}

			sb.Append('\n').Append("    ").Append(lineNo).Append(" | ").Append(line);
			sb.Append('\n').Append("    ").Append(gutter).Append(" | ")
				.Append(new string(' ', caretPad)).Append(new string('^', caretLen));
		}
	}
}
