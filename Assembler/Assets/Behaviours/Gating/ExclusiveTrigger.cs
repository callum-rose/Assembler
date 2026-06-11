using Assembler.Behaviours.Triggers;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;

namespace Assembler.Behaviours.Gating
{
	/// <summary>Forwards an upstream trigger to listeners only if no other trigger sharing the same Group has already fired this frame.</summary>
	/// <remarks>
	/// Properties:
	///   Group: Name identifying the exclusion group; only the first trigger in this group to fire each frame propagates.
	/// </remarks>
	public class ExclusiveTrigger : Trigger<ExclusiveTriggerData>, IAmExecutable
	{
		public ExclusiveGroupRegistry Registry { get; set; } = null!;

		public void Execute(TriggerContext ctx)
		{
			if (Registry.TryClaim(Data.Group.Get(ctx)))
			{
				NotifyListeners(ctx);
			}
		}
	}
}
