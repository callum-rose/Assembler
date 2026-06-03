using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>
	/// Typed handle to the interactive parts of the button prefab, wired in the prefab itself so the
	/// runtime block never relies on fragile child lookups. Restyle the prefab freely as long as these
	/// references stay connected.
	/// </summary>
	public sealed class UiButtonView : MonoBehaviour
	{
		// Wired in the prefab (by the editor or UiPrefabGenerator); never null at runtime.
		[SerializeField] private Button button = null!;
		[SerializeField] private TMP_Text label = null!;

		public Button Button => button;
		public TMP_Text Label => label;
	}
}
