using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Applies the descriptor's <c>Physics.Gravity</c> to the global <see cref="UnityEngine.Physics.gravity"/>
	/// for the game's lifetime, and restores the previous value when the game is torn down. Lives on the game
	/// root so gravity is a per-run setting rather than a mutated global that leaks between games loaded in the
	/// same process. The project ships <c>m_Gravity = (0,0,0)</c>, so a descriptor that omits <c>Physics.Gravity</c>
	/// resolves to zero and preserves the existing "manual gravity" behaviour.
	/// </summary>
	public sealed class PhysicsGravityOwner : MonoBehaviour
	{
		private Vector3 _previousGravity;
		private bool _applied;

		public void Initialise(Vector3 gravity)
		{
			_previousGravity = UnityEngine.Physics.gravity;
			_applied = true;
			UnityEngine.Physics.gravity = gravity;
		}

		private void OnDestroy()
		{
			if (_applied)
			{
				UnityEngine.Physics.gravity = _previousGravity;
			}
		}
	}
}
