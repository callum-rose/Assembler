using Assembler.Deserialisation.Dtos;

namespace Assembler.Remote
{
	/// <summary>The outcome of <see cref="RemoteGameGuard.Validate"/>: whether the game may run, and if not, a
	/// player-facing reason.</summary>
	public sealed record GuardResult(bool Allowed, string? Reason)
	{
		public static GuardResult Ok { get; } = new(true, null);

		public static GuardResult Reject(string reason) => new(false, reason);
	}

	/// <summary>
	/// v1 gate: remote games may only use built-in/primitive renderers. A descriptor that declares a top-level
	/// <c>Assets:</c> block references custom voxel/sprite/audio assets that are NOT shipped with the player
	/// build, so it would throw mid-build when <c>AssetRegistry.LoadAll</c> fails to <c>Resources.Load</c> the
	/// missing path. Rejecting up front turns that crash into a clean "not available in this version" message.
	/// (Checking the declarations is sufficient: an <c>!asset</c> reference can only resolve to something
	/// declared here, so no declarations means no resolvable asset references.)
	/// </summary>
	public static class RemoteGameGuard
	{
		public static GuardResult Validate(GameDto dto) =>
			dto.Assets is { Count: > 0 }
				? GuardResult.Reject("This game needs assets that aren't available in this version of the app.")
				: GuardResult.Ok;
	}
}
