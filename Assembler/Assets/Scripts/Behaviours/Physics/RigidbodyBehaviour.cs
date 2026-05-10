using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Physics
{
	[RequireComponent(typeof(Rigidbody))]
	public abstract class RigidbodyBehaviour<T> : GameBehaviour<T> where T : BehaviourInfo
	{
		protected Rigidbody Rigidbody { get; private set; }

		private void Awake()
		{
			Rigidbody = GetComponent<Rigidbody>();
		}
	}

}