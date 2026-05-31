namespace Assembler.Building
{
	/// <summary>
	/// Identity of the implicit entity/behaviour the Builder synthesizes to end the game. Shared so the
	/// synthesis code and the <c>!gameover</c> listener resolution agree on one <c>BehaviourDescriptor</c>.
	/// </summary>
	internal static class GameOverController
	{
		public const string EntityId = "$gameover$";
		public const string EndBehaviourId = "end";
	}
}
