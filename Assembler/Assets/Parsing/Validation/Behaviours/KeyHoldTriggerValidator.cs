using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Parsing.Validation.Behaviours
{
	public sealed class KeyHoldTriggerValidator : IBehaviourValidator
	{
		public void Validate(BehaviourInfo info, ValidationContext ctx)
		{
			var trigger = (KeyHoldTriggerInfo)info;

			ctx.RequireValue(trigger.Key, "Key",
				"Set 'Key' to the key to watch, e.g. Key: Space.");
		}
	}
}
