using System;
using Core;
using UnityEngine;
using UnityEngine.Events;

namespace Behaviours.Triggers
{
	public abstract class Trigger : GameBehaviour
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