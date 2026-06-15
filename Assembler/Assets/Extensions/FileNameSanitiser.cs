using System;
using System.IO;
using System.Text;

namespace Assembler.Extensions
{
	/// <summary>
	/// Turns an arbitrary, possibly-untrusted string (a game title, a remote game id) into a safe
	/// single path segment: invalid filename characters, path separators and whitespace runs collapse
	/// to single dashes, and leading/trailing separators are trimmed. Because it strips every
	/// <c>/</c>, <c>\</c> and <c>:</c>, the result can never escape its parent directory, so it is
	/// safe to use on identifiers that arrive from a remote manifest.
	/// </summary>
	public static class FileNameSanitiser
	{
		public static string Sanitise(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return string.Empty;
			}

			var invalid = Path.GetInvalidFileNameChars();
			var sb = new StringBuilder(value.Length);
			var prevWasSeparator = false;

			foreach (var c in value)
			{
				if (Array.IndexOf(invalid, c) >= 0 || c == '/' || c == '\\' || c == ':' || char.IsWhiteSpace(c))
				{
					if (!prevWasSeparator && sb.Length > 0)
					{
						sb.Append('-');
						prevWasSeparator = true;
					}

					continue;
				}

				sb.Append(c);
				prevWasSeparator = false;
			}

			return sb.ToString().Trim('-', '.', ' ');
		}
	}
}
