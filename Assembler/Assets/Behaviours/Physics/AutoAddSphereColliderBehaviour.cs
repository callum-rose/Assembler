using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	public sealed class AutoAddSphereColliderBehaviour : GameBehaviour<SphereColliderData>
	{
		private SphereCollider _sphereCollider;
		
		protected override void OnInitialise(SphereColliderData data)
		{
			_sphereCollider = gameObject.GetComponent<SphereCollider>() ?? gameObject.AddComponent<SphereCollider>();
			data.Radius.UseIfValueExists(v => _sphereCollider.radius = v);
			data.IsTrigger.UseIfValueExists(v => _sphereCollider.isTrigger = v);
		}

		public override void Execute()
		{
		}
	}
}