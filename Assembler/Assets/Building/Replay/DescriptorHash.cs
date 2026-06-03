using System.Security.Cryptography;
using System.Text;

namespace Assembler.Building.Replay
{
	/// <summary>
	/// Hashes a game descriptor so a replay can be validated against the descriptor it was recorded from. Uses the
	/// SHA-256 of the raw YAML text: whitespace/comment edits invalidate a replay (acceptable for Level 1; a
	/// normalized-AST hash is future work). See the Determinism (Level 1) section in CLAUDE.md.
	/// </summary>
	public static class DescriptorHash
	{
		public static string Compute(string yaml)
		{
			using var sha = SHA256.Create();
			var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(yaml));
			var builder = new StringBuilder(bytes.Length * 2);
			foreach (var b in bytes)
			{
				builder.Append(b.ToString("x2"));
			}

			return builder.ToString();
		}
	}
}
