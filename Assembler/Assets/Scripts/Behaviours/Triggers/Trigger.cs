using System;
using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;
using UnityEngine.Events;

namespace AssemblerAlpha.Behaviours.Triggers
{
	public abstract class Trigger<T> : GameBehaviour<T> where T : BehaviourInfo
	{
		[SerializeField] private UnityEvent testEvent;

		public event Action Triggered;

		public override void Execute()
		{
		}
		
		protected void InvokeTrigger()
		{
			Triggered?.Invoke();
			testEvent.Invoke();
		}
	}
}