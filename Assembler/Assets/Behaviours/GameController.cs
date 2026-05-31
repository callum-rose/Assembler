using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>Root of a running game. Parents every entity so the whole game can be torn down at once.</summary>
	public sealed class GameController : MonoBehaviour
	{
		private bool _ended;

		public void EndGame()
		{
			if (_ended)
			{
				return;
			}

			_ended = true;
			Debug.Log("Game Over");
			Destroy(gameObject);
		}
	}
}
