using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Core
{
	public abstract class GameBehaviour : MonoBehaviour
	{
		public abstract void Execute();
	}

	public abstract class GameBehaviour<T> : GameBehaviour where T : BehaviourInfo
	{
		public void Initialise(T behaviourInfo)
		{
			OnInitialise(behaviourInfo);
		}

		protected virtual void OnInitialise(T behaviourInfo) { }
	}
}