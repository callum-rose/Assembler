using System.Text;

namespace Assembler.RemoteTooling;

/// <summary>
/// Writes human-facing progress to stderr (so a command's real payload — e.g. a published game id —
/// stays alone on stdout). Optionally tees every line, un-coloured, into a buffer so the daemon can
/// quote a failed publish back onto the GitHub issue.
/// </summary>
public sealed class Logger(StringBuilder? capture = null)
{
	private static readonly char Esc = (char)0x1b;
	private static readonly string Cyan = $"{Esc}[36m";
	private static readonly string Red = $"{Esc}[31m";
	private static readonly string Reset = $"{Esc}[0m";

	public void Info(string message) => Write(Cyan, message);

	public void Err(string message) => Write(Red, message);

	/// <summary>Pass a subprocess's own output through verbatim (no colour of ours).</summary>
	public void Raw(string line)
	{
		Console.Error.WriteLine(line);
		capture?.AppendLine(line);
	}

	private void Write(string colour, string message)
	{
		Console.Error.WriteLine($"{colour}{message}{Reset}");
		capture?.AppendLine(message);
	}
}

/// <summary>An expected, user-facing failure — its message is printed without a stack trace.</summary>
public sealed class AppException(string message) : Exception(message);
