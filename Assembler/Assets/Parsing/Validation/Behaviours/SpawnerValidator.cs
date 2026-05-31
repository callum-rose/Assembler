using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Parsing.Validation.Behaviours
{
	public sealed class SpawnerValidator : IBehaviourValidator
	{
		public void Validate(BehaviourInfo info, ValidationContext ctx)
		{
			var spawner = (SpawnerInfo)info;

			ctx.RequireValue(spawner.TemplateId, "TemplateId",
				"Set 'TemplateId' to the id of a template defined under Templates, e.g. TemplateId: bullet.");
		}
	}
}
