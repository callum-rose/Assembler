namespace Assembler.Core
{
	/// <summary>
	/// Identifiers the pipeline reserves for entities/behaviours it synthesises or mints at runtime. They
	/// live here — in the assembly both the Building synthesis code and the Parsing reference-validation
	/// pass depend on — so the two sides agree on one spelling and cannot drift out of sync.
	/// </summary>
	public static class ReservedIds
	{
		/// <summary>The implicit entity the Builder synthesises to end the game.</summary>
		public const string GameOverEntityId = "$gameover$";

		/// <summary>The sole behaviour exposed by the game-over entity.</summary>
		public const string GameOverBehaviourId = "end";

		/// <summary>Prefix on ids minted for runtime-spawned entities (<c>$spawn$&lt;template&gt;_&lt;n&gt;</c>).</summary>
		public const string SpawnedIdPrefix = "$spawn$";
	}
}
