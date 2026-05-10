using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	public sealed class AutoAddSphereColliderBehaviour : AutoAddColliderBehaviour<SphereColliderData>
	{
		private SphereCollider _sphereCollider;
		
		protected override void OnInitialise(SphereColliderData data)
		{
			_sphereCollider = gameObject.AddComponent<SphereCollider>();
			data.Radius.UseIfValueExists(v => _sphereCollider.radius = v);
			data.IsTrigger.UseIfValueExists(v => _sphereCollider.isTrigger = v);
		}

		public override void Execute()
		{
		}
	}
}