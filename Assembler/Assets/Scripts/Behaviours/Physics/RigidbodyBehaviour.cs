using Assembler.Core;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	public sealed class RigidbodyBehaviour : GameBehaviour<RigidbodyData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(RigidbodyData data)
		{
			_rigidbody = gameObject.AddComponent<Rigidbody>();
			data.IsKinematic.UseIfValueExists(v => _rigidbody.isKinematic = v);
			data.UseGravity.UseIfValueExists(v => _rigidbody.useGravity = v);
		}

		public override void Execute() { }
	}
}