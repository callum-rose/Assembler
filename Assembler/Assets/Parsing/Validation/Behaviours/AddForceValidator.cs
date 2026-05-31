using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Parsing.Validation.Behaviours
{
	public sealed class AddForceValidator : IBehaviourValidator
	{
		public void Validate(BehaviourInfo info, ValidationContext ctx)
		{
			var addForce = (AddForceInfo)info;

			ctx.RequireValue(addForce.Force, "Force",
				"Add a 'Force' property to the behaviour, e.g. Force: !vec [0, 10, 0].");
		}
	}
}
