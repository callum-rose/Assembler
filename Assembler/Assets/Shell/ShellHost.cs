using Assembler.Shell.Layout;
using UnityEngine;

namespace Assembler.Shell
{
	/// <summary>
	/// One layer of the shell: a full-bleed rect carrying its own nested <see cref="UnityEngine.Canvas"/>, with a
	/// safe-area child for the content that must clear the notch. The nested canvas is the point — it isolates
	/// this layer's rebuilds from the others', and makes showing or hiding the layer a single cheap toggle.
	/// </summary>
	/// <remarks>
	/// Decoration that should bleed to the screen edge (the paper ground, an ink-dark header block, the game
	/// strip's field) parents to <see cref="Rect"/>; anything the user has to read or hit parents to
	/// <see cref="SafeArea"/>.
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[RequireComponent(typeof(Canvas))]
	[AddComponentMenu("Assembler/Shell/Shell Host")]
	public sealed class ShellHost : MonoBehaviour
	{
		[SerializeField] private Canvas canvas = null!;
		[SerializeField] private RectTransform rect = null!;
		[SerializeField] private SafeAreaPanel safeArea = null!;

		/// <summary>This layer's nested canvas.</summary>
		public Canvas Canvas => canvas;

		/// <summary>The full-bleed rect, screen edge to screen edge.</summary>
		public RectTransform Rect => rect;

		/// <summary>The safe-area rect inside it.</summary>
		public RectTransform SafeArea => (RectTransform)safeArea.transform;
	}
}
