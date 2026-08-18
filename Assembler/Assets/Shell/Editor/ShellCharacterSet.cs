using System.Collections.Generic;
using System.Text;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// The characters the shell's font atlases are baked with. The app's copy is en-GB and doesn't localise, so
	/// "full character set" means Latin-1 plus the typography a newspaper actually sets: proper quotes, en and em
	/// dashes, an ellipsis, a bullet, daggers and the numero sign the folio uses.
	/// </summary>
	internal static class ShellCharacterSet
	{
		// Latin Extended-A letters that reach English and French set copy, the punctuation a serif face is
		// bought for, currency, and № for the edition number.
		private static readonly IReadOnlyList<int> Extras = new[]
		{
			0x0152, 0x0153, 0x0160, 0x0161, 0x0178, 0x017D, 0x017E, 0x0192,
			0x2013, 0x2014, 0x2018, 0x2019, 0x201A, 0x201C, 0x201D, 0x201E,
			0x2020, 0x2021, 0x2022, 0x2026, 0x2030, 0x2039, 0x203A, 0x2044,
			0x20AC, 0x2116, 0x2122
		};

		/// <summary>The character sequence to bake, as one string.</summary>
		public static string Build()
		{
			var builder = new StringBuilder();

			AppendRange(builder, 0x0020, 0x007E); // ASCII printable
			AppendRange(builder, 0x00A0, 0x00FF); // Latin-1 Supplement

			foreach (int codePoint in Extras)
			{
				builder.Append(char.ConvertFromUtf32(codePoint));
			}

			return builder.ToString();
		}

		private static void AppendRange(StringBuilder builder, int first, int last)
		{
			for (int codePoint = first; codePoint <= last; codePoint++)
			{
				builder.Append(char.ConvertFromUtf32(codePoint));
			}
		}
	}
}
