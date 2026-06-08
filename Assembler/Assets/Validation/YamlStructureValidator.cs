using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Assembler.Validation
{
	/// <summary>
	/// A basic, schema-agnostic structural validator for YAML. It checks that a document is
	/// well-formed and free of common structural mistakes, reporting — with line/column — where and
	/// why it is invalid. It deliberately does NOT validate against the game-descriptor schema; that
	/// is the job of the runtime parser/transformer once the YAML itself is known-good.
	///
	/// This lives in a runtime assembly (no editor or platform dependencies) so it can run inside a
	/// player build on any platform — e.g. to validate a descriptor before the engine loads it — as
	/// well as from the editor and the headless command-line entry point (Editor.YamlValidatorBatch).
	///
	/// What it catches:
	///   * Syntax errors  — bad indentation, tabs used for indentation, unterminated quotes/flows,
	///                       mis-aligned block mappings, etc. (anything the YAML parser rejects).
	///   * Duplicate keys — two entries with the same key in one mapping (silently drops data, so it
	///                       is treated as an error).
	///   * Empty document — a file with no content (reported as a warning).
	///
	/// The custom descriptor tags (!vec, !colour, !var, ...) need no special handling: at the parser
	/// level a tag is just a property on a node, so tagged values validate fine without the DTOs.
	/// </summary>
	public static class YamlStructureValidator
	{
		/// <summary>
		/// Validates a YAML string. <paramref name="sourceName"/> is an optional label (e.g. a file
		/// path) used only in report headers. Never throws for malformed YAML — the problem is
		/// returned as an issue in the result.
		/// </summary>
		public static YamlValidationResult Validate(string yaml, string sourceName = null)
		{
			yaml ??= string.Empty;
			var lines = SplitLines(yaml);
			var issues = new List<YamlValidationIssue>();

			try
			{
				var parser = new Parser(new StringReader(yaml));
				var sawContent = false;

				parser.Consume<StreamStart>();
				while (!parser.Accept<StreamEnd>(out _))
				{
					parser.Consume<DocumentStart>();
					if (!parser.Accept<DocumentEnd>(out _))
					{
						sawContent = true;
						WalkNode(parser, issues);
					}

					parser.Consume<DocumentEnd>();
				}

				parser.Consume<StreamEnd>();

				if (!sawContent)
				{
					issues.Add(new YamlValidationIssue(
						YamlValidationSeverity.Warning, "document is empty (no content)"));
				}
			}
			catch (YamlException ex)
			{
				// The parser stops at the first structural error; report it with full position info.
				issues.Add(new YamlValidationIssue(
					YamlValidationSeverity.Error,
					CleanMessage(ex.Message),
					(int)ex.Start.Line, (int)ex.Start.Column,
					(int)ex.End.Line, (int)ex.End.Column));
			}
			catch (Exception ex)
			{
				issues.Add(new YamlValidationIssue(
					YamlValidationSeverity.Error, "unexpected parse failure: " + ex.Message));
			}

			return new YamlValidationResult(sourceName, lines, issues);
		}

		/// <summary>
		/// Convenience wrapper that reads a file and validates its contents. Only usable on platforms
		/// with file-system access; on platforms without it, load the text yourself and call
		/// <see cref="Validate"/>. A read failure is returned as an error issue, not thrown.
		/// </summary>
		public static YamlValidationResult ValidateFile(string path)
		{
			try
			{
				return Validate(File.ReadAllText(path), path);
			}
			catch (Exception ex)
			{
				var issues = new List<YamlValidationIssue>
				{
					new YamlValidationIssue(
						YamlValidationSeverity.Error, "could not read file: " + ex.Message),
				};
				return new YamlValidationResult(path, Array.Empty<string>(), issues);
			}
		}

		// Recursive-descent walk over the parser event stream. Validates well-formedness implicitly
		// (malformed input throws a YamlException out of Consume) and detects duplicate mapping keys.
		private static void WalkNode(IParser parser, List<YamlValidationIssue> issues)
		{
			if (parser.Accept<Scalar>(out _))
			{
				parser.Consume<Scalar>();
				return;
			}

			if (parser.Accept<AnchorAlias>(out _))
			{
				parser.Consume<AnchorAlias>();
				return;
			}

			if (parser.Accept<MappingStart>(out _))
			{
				parser.Consume<MappingStart>();
				var seenKeys = new Dictionary<string, int>();

				while (!parser.Accept<MappingEnd>(out _))
				{
					// Key.
					if (parser.Accept<Scalar>(out var keyScalar))
					{
						parser.Consume<Scalar>();
						var key = keyScalar.Value;
						if (seenKeys.TryGetValue(key, out var firstLine))
						{
							issues.Add(new YamlValidationIssue(
								YamlValidationSeverity.Error,
								$"duplicate key '{key}' in mapping (first defined at line {firstLine}); " +
								"the earlier value will be silently discarded",
								(int)keyScalar.Start.Line, (int)keyScalar.Start.Column,
								(int)keyScalar.End.Line, (int)keyScalar.End.Column));
						}
						else
						{
							seenKeys[key] = (int)keyScalar.Start.Line;
						}
					}
					else
					{
						// Complex (non-scalar) key — uncommon; consume without a duplicate check.
						WalkNode(parser, issues);
					}

					// Value.
					WalkNode(parser, issues);
				}

				parser.Consume<MappingEnd>();
				return;
			}

			if (parser.Accept<SequenceStart>(out _))
			{
				parser.Consume<SequenceStart>();
				while (!parser.Accept<SequenceEnd>(out _))
				{
					WalkNode(parser, issues);
				}

				parser.Consume<SequenceEnd>();
				return;
			}

			// Anything else: consume a single event so the walk can never spin forever.
			parser.Consume<ParsingEvent>();
		}

		private static string[] SplitLines(string text) =>
			text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

		// YamlDotNet prefixes messages with a "(Line: X, Col: Y, Idx: Z) - " stamp; we render the
		// location ourselves, so strip that redundant prefix from the message body.
		private static string CleanMessage(string message)
		{
			if (message == null)
			{
				return string.Empty;
			}

			var dash = message.IndexOf(") - ", StringComparison.Ordinal);
			if (message.StartsWith("(Line:", StringComparison.Ordinal) && dash >= 0)
			{
				message = message.Substring(dash + 4);
			}

			return message.Trim();
		}
	}
}
