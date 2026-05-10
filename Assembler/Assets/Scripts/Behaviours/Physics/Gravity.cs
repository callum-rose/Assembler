using Assembler.Parsing.Phase2.Parsing.Phase2.Info;

namespace AssemblerAlpha.Behaviours.Physics
{
	public class Gravity : RigidbodyBehaviour<RigidbodyInfo>
	{
		private bool _gravityEnabled;

		protected override void OnInitialise(RigidbodyInfo behaviourInfo)
		{
			_gravityEnabled = behaviourInfo.UseGravity;
		}

		public override void Execute()
		{
			Rigidbody.useGravity = _gravityEnabled;
		}
	}
}