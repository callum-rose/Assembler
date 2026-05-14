using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Physics
{
	public sealed class AutoAddBoxColliderBehaviour : AutoAddColliderBehaviour<BoxColliderData>
	{
		private BoxCollider _boxCollider;
		
		protected override void OnInitialise(BoxColliderData data)
		{
			_boxCollider = gameObject.AddComponent<BoxCollider>();
			data.Size.UseIfValueExists(v => _boxCollider.size = v);
			data.IsTrigger.UseIfValueExists(v => _boxCollider.isTrigger = v);
		}

		public override void Execute()
		{
		}
	}
}