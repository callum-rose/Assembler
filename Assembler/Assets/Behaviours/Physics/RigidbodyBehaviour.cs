using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	public sealed class RigidbodyBehaviour : GameBehaviour<RigidbodyData>
	{
		private Rigidbody _rigidbody;

		protected override void OnInitialise(RigidbodyData data)
		{
			_rigidbody = gameObject.GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
			_rigidbody.linearVelocity = Vector3.zero;
			_rigidbody.angularVelocity = Vector3.zero;
			data.IsKinematic.UseIfValueExists(v => _rigidbody.isKinematic = v);
			data.UseGravity.UseIfValueExists(v => _rigidbody.useGravity = v);
		}

		public override void Execute() { }

		public override void OnDespawn()
		{
			if (_rigidbody != null)
			{
				_rigidbody.linearVelocity = Vector3.zero;
				_rigidbody.angularVelocity = Vector3.zero;
			}
		}
	}
}