using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Applies the descriptor's <see cref="PhysicsInfo"/> to Unity's global physics settings for the game's
	/// lifetime, and restores the previous values when the game is torn down. Lives on the game root so physics
	/// is a per-run setting rather than a mutated global that leaks between games loaded in the same process.
	/// <para>
	/// Today <see cref="PhysicsInfo"/> only carries gravity, but this is the single place global physics params
	/// are applied and reverted: as more are added (fixed timestep, default bounce/friction combine, solver
	/// iterations, sleep thresholds, …), snapshot the old value and apply the new one in <see cref="Initialise"/>
	/// and mirror the restore in <see cref="OnDestroy"/>. The project ships <c>m_Gravity = (0,0,0)</c>, so a
	/// descriptor that omits gravity resolves to zero and preserves the existing "manual gravity" behaviour.
	/// </para>
	/// </summary>
	public sealed class PhysicsSettingsOwner : MonoBehaviour
	{
		private Vector3 _previousGravity;
		private bool _applied;

		public void Initialise(PhysicsInfo physics)
		{
			// Snapshot every global we're about to touch so teardown can restore it exactly.
			_previousGravity = UnityEngine.Physics.gravity;
			_applied = true;

			UnityEngine.Physics.gravity = physics.Gravity;
		}

		private void OnDestroy()
		{
			if (!_applied)
			{
				return;
			}

			UnityEngine.Physics.gravity = _previousGravity;
		}
	}
}
