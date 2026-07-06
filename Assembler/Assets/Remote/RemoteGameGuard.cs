using Assembler.Deserialisation.Dtos;

namespace Assembler.Remote
{
	/// <summary>The outcome of <see cref="RemoteGameGuard.Validate"/> as a closed discriminated union: either
	/// <see cref="Allowed"/>, or <see cref="Rejected"/> carrying a player-facing reason.</summary>
	public abstract record GuardResult
	{
		public sealed record Allowed : GuardResult;

		public sealed record Rejected(string Reason) : GuardResult;
	}

	/// <summary>
	/// v1 gate: remote games may only use built-in/primitive renderers. A descriptor that declares a top-level
	/// <c>Assets:</c> block references custom voxel/sprite/audio assets that are NOT shipped with the player
	/// build, so it would throw mid-build when <c>AssetRegistry.LoadAllAsync</c> fails to load the missing path.
	/// Rejecting up front turns that crash into a clean "not available in this version" message.
	/// (Checking the declarations is sufficient: an <c>!asset</c> reference can only resolve to something
	/// declared here, so no declarations means no resolvable asset references.)
	/// </summary>
	public static class RemoteGameGuard
	{
		public static GuardResult Validate(GameDto dto) =>
			dto.Assets is { Count: > 0 }
				? new GuardResult.Rejected("This game needs assets that aren't available in this version of the app.")
				: new GuardResult.Allowed();
	}
}
