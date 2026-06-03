using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>
	/// Holds the reusable uGUI prefabs that the leaf UI blocks (button, label, slider) instantiate at
	/// runtime. The default asset lives at <c>Resources/UI/UiPrefabLibrary</c> and is loaded once by the
	/// Builder; a game can supply an alternative library to re-theme the whole UI without code changes.
	/// </summary>
	[CreateAssetMenu(fileName = "UiPrefabLibrary", menuName = "Assembler/UI Prefab Library")]
	public sealed class UiPrefabLibrary : ScriptableObject
	{
		/// <summary>Resources path (sans extension) the Builder loads the default library from.</summary>
		public const string DefaultResourcePath = "UI/UiPrefabLibrary";

		// Assigned in the asset (by the editor or UiPrefabGenerator). Each prefab root carries the
		// matching view component (UiButtonView / UiLabelView / UiSliderView).
		public GameObject buttonPrefab = null!;
		public GameObject labelPrefab = null!;
		public GameObject sliderPrefab = null!;
	}
}
