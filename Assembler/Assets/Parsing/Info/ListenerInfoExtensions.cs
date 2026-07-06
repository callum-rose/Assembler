using Assembler.Deserialisation;

namespace Assembler.Parsing.Info
{
	public static class ListenerInfoExtensions
	{
		/// <summary>
		/// A <c>" (at line L, column C)"</c> suffix when the listener's source position is known, else the
		/// empty string — so error messages cite the offending YAML line but stay clean for synthesised
		/// listeners (nested hooks, template expansions) that carry no source node.
		/// </summary>
		public static string SourceSuffix(this ListenerInfo listener) =>
			listener.Position is KnownSourcePosition position ? $" (at {position})" : string.Empty;
	}
}
