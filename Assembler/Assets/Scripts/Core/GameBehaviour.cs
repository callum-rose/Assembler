using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Core
{
	public abstract class GameBehaviour : MonoBehaviour
	{
		public abstract void Execute();
	}

	public abstract class GameBehaviour<TData> : GameBehaviour where TData : BehaviourData
	{
		protected TData Data { get; private set; }

		public void Initialise(TData data)
		{
			Data = data;
			OnInitialise(data);
		}

		protected virtual void OnInitialise(TData data) { }
	}
}
