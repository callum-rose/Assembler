using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Owns the per-game <see cref="AssetRegistry"/> and releases its loaded assets when the game is torn down.
	/// Lives on the game root (alongside <see cref="ControlsAssetOwner"/>) so destroying the root releases every
	/// Addressables handle the registry produced — matching how the rest of the game unloads (and how the sandbox
	/// validator's <c>DestroyImmediate</c> teardown frees a game). Resources-sourced assets need no release; the
	/// registry's Dispose is a no-op for them.
	/// </summary>
	public sealed class AssetRegistryOwner : MonoBehaviour
	{
		// Null until Initialise runs — OnDestroy can fire first if the game tears down before wiring, hence the
		// null guard there.
		private AssetRegistry? _registry;

		public void Initialise(AssetRegistry registry) => _registry = registry;

		private void OnDestroy() => _registry?.Dispose();
	}
}
