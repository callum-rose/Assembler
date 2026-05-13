using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Core
{
	public abstract class GameBehaviour : MonoBehaviour
	{
		protected string Id { get; private set; }
		
		private IReadOnlyList<Action> _listeners;

		public abstract void Execute();

		protected void SetBase(BehaviourData behaviourData) => (Id, _listeners) = (behaviourData.Id, behaviourData.Listeners);

		protected void NotifyListeners()
		{
			foreach (var listener in _listeners)
			{
				listener.Invoke();
			}
		}
	}

	public abstract class GameBehaviour<TData> : GameBehaviour where TData : BehaviourData
	{
		protected TData Data { get; private set; }

		public void Initialise(TData data)
		{
			Data = data;

			SetBase(data);
			OnInitialise(data);
		}

		protected virtual void OnInitialise(TData data) { }
	}
}