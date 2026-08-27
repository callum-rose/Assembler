using UnityEngine;

namespace Assembler.Shell
{
	/// <summary>
	/// The shell's editorial numbers. Decisions about how much of the catalogue a page shows are the editor's,
	/// not the compiler's, so they live in an asset rather than as constants in a presenter.
	/// </summary>
	[CreateAssetMenu(fileName = "ShellConfig", menuName = "Assembler/Shell/Config")]
	public sealed class ShellConfig : ScriptableObject
	{
		[Tooltip("How many cards the feed runs under the lead story before it sends the reader to the archive.")]
		[Min(0)]
		[SerializeField] private int feedCardCount = 6;

		/// <inheritdoc cref="feedCardCount"/>
		public int FeedCardCount => feedCardCount;
	}
}
