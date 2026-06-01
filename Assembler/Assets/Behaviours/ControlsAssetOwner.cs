using UnityEngine;
using UnityEngine.InputSystem;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Owns the per-game <see cref="InputActionAsset"/> built from the descriptor's controls. Lives on the game
	/// root so the asset is enabled for the whole game's lifetime and destroyed when the game is torn down.
	/// Centralising enable/disable here (rather than per <c>InputActionTrigger</c>) means an action shared by
	/// several behaviours stays live until the whole game unloads, and the ScriptableObject asset isn't leaked
	/// across relaunches.
	/// </summary>
	public sealed class ControlsAssetOwner : MonoBehaviour
	{
		private InputActionAsset _asset;

		public void Initialise(InputActionAsset asset)
		{
			_asset = asset;
			_asset.Enable();
		}

		private void OnDestroy()
		{
			if (_asset == null)
			{
				return;
			}

			_asset.Disable();
			Destroy(_asset);
		}
	}
}
