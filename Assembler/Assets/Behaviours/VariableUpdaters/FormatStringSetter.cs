using System.Collections.Generic;
using System.Globalization;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.VariableUpdaters
{
	/// <summary>Writes a formatted string into the variable referenced by VariableId, using Format as a <c>string.Format</c> template and Arguments as the substitution values.</summary>
	/// <remarks>
	/// Properties:
	///   VariableId: Reference to the destination string variable. Typically a `!ref` to a string variable.
	///   Format: A <c>string.Format</c>-style format string, e.g. "Score: {0}" or "{0}/{1}".
	///   Arguments [list]: List of value sources used as substitution arguments. May mix types (ints, floats, strings, etc.); null values are rendered as empty strings.
	/// </remarks>
	public class FormatStringSetter : GameBehaviour<FormatStringSetterData>
	{
		public override void Execute()
		{
			var formatString = Data.Format.Value;
			var args = new object[Data.Arguments.Count];
			for (var i = 0; i < Data.Arguments.Count; i++)
			{
				args[i] = Data.Arguments[i]?.Value;
			}

			Data.ValueToSet.Value = FormatString(formatString, args);
		}

		/// <summary>
		/// Formats <paramref name="format"/> with <paramref name="arguments"/>, treating
		/// nulls (both the format string and any individual argument) as empty strings.
		/// Falls back to a literal concatenation if <c>string.Format</c> throws (e.g. on a
		/// malformed format string), so a HUD never crashes a running game.
		/// </summary>
		public static string FormatString(string format, IReadOnlyList<object> arguments)
		{
			format ??= string.Empty;
			var count = arguments?.Count ?? 0;
			var args = new object[count];
			for (var i = 0; i < count; i++)
			{
				args[i] = arguments[i] ?? string.Empty;
			}

			try
			{
				return string.Format(CultureInfo.InvariantCulture, format, args);
			}
			catch (System.FormatException)
			{
				return format;
			}
		}
	}
}
