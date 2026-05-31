using Assembler.Parsing.Info;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// Behaviour-specific validation. Implement one per behaviour type that has required values or
	/// invariants beyond the shared base checks, then register it in
	/// <see cref="BehaviourValidatorRegistry"/>. The walker passes the matching <see cref="BehaviourInfo"/>;
	/// cast it to the concrete type and record errors via <paramref name="ctx"/>.
	/// </summary>
	public interface IBehaviourValidator
	{
		void Validate(BehaviourInfo info, ValidationContext ctx);
	}
}
