using Assembler.Core;

namespace Assembler.Building
{
	/// <summary>
	/// Identity of the implicit entity/behaviour the Builder synthesizes to end the game. Shared so the
	/// synthesis code and the <c>!gameover</c> listener resolution agree on one <c>BehaviourDescriptor</c>.
	/// Aliases <see cref="ReservedIds"/> so the spelling has a single source of truth.
	/// </summary>
	internal static class GameOverController
	{
		public const string EntityId = ReservedIds.GameOverEntityId;
		public const string EndBehaviourId = ReservedIds.GameOverBehaviourId;
	}
}
